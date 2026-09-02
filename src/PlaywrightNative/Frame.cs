/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Copyright (c) 2020 Meir Blachman
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlaywrightNative.Chromium;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Browser-agnostic internal frame. Chromium drives it today; WebKit/Firefox can reuse it.
    /// Tracks frame state including lifecycle events, child frames, and execution contexts
    /// without going through the channel-based transport.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Usage",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "Timer lifetime is managed explicitly by CRPage teardown via DisposeNetworkIdleTimer; exposing IDisposable on an internal type used across event callbacks would invite premature disposal.")]
    internal class Frame
    {
        /// <summary>
        /// Quiet-period threshold (milliseconds) after which a frame with zero inflight
        /// non-favicon requests is considered network-idle. Mirrors Playwright's upstream
        /// 500 ms window.
        /// </summary>
        private const int NetworkIdleQuietPeriodMs = NetworkIdleRules.QuietPeriodMs;

        private readonly Frame _parentFrame;
        private readonly List<Frame> _childFrames = new();
        private readonly HashSet<string> _lifecycleEvents = new();
        private readonly HashSet<string> _inflightRequestIds = new();
        private readonly object _inflightLock = new();

        private string _frameId;
        private string _url;
        private string _name;
        private string _documentId = string.Empty;
        private CRExecutionContext _executionContext;
        private Timer _networkIdleTimer;
        private bool _firedNetworkIdleSelf;
        private bool _isDetached;

        /// <summary>
        /// Initializes a new instance of the <see cref="Frame"/> class.
        /// </summary>
        /// <param name="frameId">The CDP frame identifier.</param>
        /// <param name="parentFrame">
        /// The parent <see cref="Frame"/>, or <c>null</c> for the main frame.
        /// </param>
        /// <param name="url">The initial URL of the frame.</param>
        /// <param name="name">The initial name of the frame.</param>
        public Frame(string frameId, Frame parentFrame, string url = "", string name = "")
        {
            _frameId = frameId ?? throw new ArgumentNullException(nameof(frameId));
            _parentFrame = parentFrame;
            _url = url ?? string.Empty;
            _name = name ?? string.Empty;

            // javascript:/blob: iframes may attach without a committed navigation or
            // network request. Start the quiet period so they can still reach
            // networkidle and unblock the parent waitForLoadState.
            StartNetworkIdleTimerLocked();
        }

        /// <summary>
        /// Occurs when a lifecycle event (e.g. "load", "DOMContentLoaded") fires on this frame.
        /// The parameter is the lifecycle event name.
        /// </summary>
        internal event Action<string> LifecycleChanged;

        /// <summary>
        /// Occurs when the frame navigates to a new document.
        /// </summary>
        internal event Action Navigated;

        /// <summary>
        /// Gets the CDP frame identifier.
        /// </summary>
        internal string FrameId { get => _frameId; set => _frameId = value; }

        /// <summary>
        /// Gets the parent frame, or <c>null</c> for the main frame.
        /// </summary>
        internal Frame ParentFrame => _parentFrame;

        /// <summary>
        /// Gets or sets the current URL of this frame.
        /// </summary>
        internal string Url
        {
            get => _url;
            set => _url = value;
        }

        /// <summary>
        /// Gets or sets the name of this frame.
        /// </summary>
        internal string Name
        {
            get => _name;
            set => _name = value;
        }

        /// <summary>
        /// Gets or sets the current document ID (loader ID) for this frame.
        /// Updated when the frame commits a new-document navigation.
        /// </summary>
        internal string DocumentId
        {
            get => _documentId;
            set => _documentId = value;
        }

        /// <summary>
        /// Gets a value indicating whether this frame has been detached from the page.
        /// </summary>
        internal bool IsDetached => _isDetached;

        /// <summary>
        /// Gets the child frames of this frame.
        /// </summary>
        internal IReadOnlyList<Frame> ChildFrames => _childFrames;

        /// <summary>
        /// Gets a snapshot of the lifecycle events that have fired on this frame.
        /// The snapshot is taken under the inflight lock so callers observe a consistent
        /// view even while the dispatcher thread or the network-idle timer callback
        /// mutates the underlying set.
        /// </summary>
        internal IReadOnlyCollection<string> LifecycleEvents
        {
            get
            {
                lock (_inflightLock)
                {
                    return _lifecycleEvents.ToArray();
                }
            }
        }

        /// <summary>
        /// Gets or sets the main world execution context for this frame.
        /// </summary>
        internal CRExecutionContext ExecutionContext
        {
            get => _executionContext;
            set => _executionContext = value;
        }

        /// <summary>
        /// Adds a child frame to this frame.
        /// </summary>
        /// <param name="frame">The child <see cref="Frame"/> to add.</param>
        internal void AddChildFrame(Frame frame)
        {
            _childFrames.Add(frame);
        }

        /// <summary>
        /// Removes a child frame from this frame.
        /// </summary>
        /// <param name="frame">The child <see cref="Frame"/> to remove.</param>
        internal void RemoveChildFrame(Frame frame)
        {
            _childFrames.Remove(frame);
        }

        /// <summary>
        /// Marks this frame detached. Called by <see cref="FrameManager"/> before
        /// raising <c>FrameDetached</c>.
        /// </summary>
        internal void MarkDetached()
        {
            lock (_inflightLock)
            {
                _isDetached = true;
                _networkIdleTimer?.Dispose();
                _networkIdleTimer = null;
                _firedNetworkIdleSelf = false;
            }
        }

        /// <summary>
        /// Records a lifecycle event and notifies listeners. The mutation of the backing
        /// set is performed under the inflight lock so that reads from <see cref="LifecycleEvents"/>
        /// and the network-idle timer callback observe a consistent state.
        /// </summary>
        /// <param name="name">The lifecycle event name (e.g. "load", "DOMContentLoaded").</param>
        internal void OnLifecycleEvent(string name)
        {
            lock (_inflightLock)
            {
                _lifecycleEvents.Add(name);
            }

            LifecycleChanged?.Invoke(name);
            RootFrame().RecalculateNetworkIdle();
        }

        /// <summary>
        /// Clears all recorded lifecycle events and resets the network-idle state.
        /// Called when the frame navigates to a new document so a fresh navigation
        /// starts with no cached lifecycle signals and no pending idle timer. All
        /// mutations happen under the inflight lock so a concurrent timer callback
        /// cannot emit a stale <c>networkidle</c> onto the new document.
        /// </summary>
        internal void ClearLifecycleEvents()
        {
            lock (_inflightLock)
            {
                _lifecycleEvents.Clear();
                _firedNetworkIdleSelf = false;
                _networkIdleTimer?.Dispose();
                _networkIdleTimer = null;
                _inflightRequestIds.Clear();
                StartNetworkIdleTimerLocked();
            }

            RootFrame().RecalculateNetworkIdle(allowRemove: this);
        }

        /// <summary>
        /// Called when a request starts on this frame. Cancels any pending
        /// network-idle timer because the network is no longer quiet.
        /// Favicon, EventSource, and WebSocket requests are excluded.
        /// </summary>
        /// <param name="requestId">The CDP request identifier.</param>
        /// <param name="excluded">
        /// <see langword="true"/> when the request must be ignored for idle
        /// (favicon, EventSource, WebSocket); otherwise <see langword="false"/>.
        /// </param>
        internal void OnInflightRequestStarted(string requestId, bool excluded)
        {
            if (excluded || string.IsNullOrEmpty(requestId))
            {
                return;
            }

            lock (_inflightLock)
            {
                _inflightRequestIds.Add(requestId);
                StopNetworkIdleTimerLocked();
            }
        }

        /// <summary>
        /// Called when a request on this frame finishes (loaded or failed). Starts a
        /// quiet-period timer; if no new request starts before it fires, the frame
        /// emits a <c>networkidle</c> lifecycle event.
        /// </summary>
        /// <param name="requestId">The CDP request identifier.</param>
        internal void OnInflightRequestFinished(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
            {
                return;
            }

            lock (_inflightLock)
            {
                if (!_inflightRequestIds.Remove(requestId))
                {
                    return;
                }

                if (_inflightRequestIds.Count == 0)
                {
                    StartNetworkIdleTimerLocked();
                }
            }
        }

        /// <summary>
        /// Walks this frame and its descendants and records <c>networkidle</c> on a
        /// frame only when it is self-idle and every child has already fired
        /// <c>networkidle</c>. Mirrors upstream <c>Frame._recalculateNetworkIdle</c>.
        /// </summary>
        /// <param name="allowRemove">
        /// When a frame just cleared its lifecycle (new document), ancestors may
        /// drop a previously recorded <c>networkidle</c> so they wait again.
        /// </param>
        internal void RecalculateNetworkIdle(Frame allowRemove = null)
        {
            bool selfIdle;
            lock (_inflightLock)
            {
                selfIdle = _firedNetworkIdleSelf && !_isDetached;
            }

            bool isNetworkIdle = selfIdle;
            Frame[] children = _childFrames.ToArray();
            foreach (Frame child in children)
            {
                child.RecalculateNetworkIdle(allowRemove);
                if (!child.HasLifecycle("networkidle"))
                {
                    isNetworkIdle = false;
                }
            }

            if (isNetworkIdle)
            {
                bool added = false;
                lock (_inflightLock)
                {
                    added = _lifecycleEvents.Add("networkidle");
                }

                if (added)
                {
                    LifecycleChanged?.Invoke("networkidle");
                }
            }
            else if (!ReferenceEquals(allowRemove, this) && HasLifecycle("networkidle"))
            {
                lock (_inflightLock)
                {
                    _lifecycleEvents.Remove("networkidle");
                }
            }
        }

        /// <summary>
        /// Disposes the network-idle timer. Called from page-teardown paths so the
        /// timer does not keep the frame alive past its useful life.
        /// </summary>
        internal void DisposeNetworkIdleTimer()
        {
            lock (_inflightLock)
            {
                _networkIdleTimer?.Dispose();
                _networkIdleTimer = null;
            }
        }

        /// <summary>
        /// Clears remaining inflight IDs when this frame is being detached so a
        /// held iframe document cannot keep ancestors from becoming idle.
        /// </summary>
        internal void ClearInflightForDetach()
        {
            lock (_inflightLock)
            {
                _inflightRequestIds.Clear();
                _networkIdleTimer?.Dispose();
                _networkIdleTimer = null;
                _firedNetworkIdleSelf = false;
            }
        }

        /// <summary>
        /// Signals that the frame has navigated to a new document.
        /// Updates URL, name, document ID, clears lifecycle events, and fires the <see cref="Navigated"/> event.
        /// </summary>
        /// <param name="url">The new URL after navigation.</param>
        /// <param name="name">The new frame name after navigation.</param>
        /// <param name="documentId">The document ID (loader ID) for this navigation.</param>
        internal void OnNavigated(string url, string name, string documentId)
        {
            _url = url ?? string.Empty;
            _name = name ?? string.Empty;
            _documentId = documentId ?? string.Empty;
            ClearLifecycleEvents();
            OnLifecycleEvent("commit");
            Navigated?.Invoke();
        }

        /// <summary>
        /// Applies getFrameTree URL/name without clearing lifecycle. Official
        /// OOPIF FrameSession does not re-commit getFrameTree navigation.
        /// </summary>
        /// <param name="url">Frame URL from <c>Page.getFrameTree</c>.</param>
        /// <param name="name">Frame name.</param>
        /// <param name="documentId">Loader id.</param>
        internal void ApplyFrameTreeSnapshot(string url, string name, string documentId)
        {
            if (string.IsNullOrEmpty(_url) && !string.IsNullOrEmpty(url))
            {
                _url = url;
            }

            if (string.IsNullOrEmpty(_name) && !string.IsNullOrEmpty(name))
            {
                _name = name;
            }

            if (string.IsNullOrEmpty(_documentId) && !string.IsNullOrEmpty(documentId))
            {
                _documentId = documentId;
            }
        }

        /// <summary>
        /// Signals that the frame has navigated. Fires the <see cref="Navigated"/> event.
        /// </summary>
        internal void OnNavigated()
        {
            Navigated?.Invoke();
        }

        /// <summary>
        /// Waits until this frame has reached <paramref name="state"/>. Resolves immediately
        /// when the event is already recorded.
        /// </summary>
        /// <param name="state">The load state to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when the state is reached.</returns>
        internal Task WaitForLoadStateAsync(LoadState state, float? timeout)
            => WaitForLoadStateAsync(state, timeout, "page.waitForLoadState");

        /// <summary>
        /// Waits until this frame has reached <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The load state to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <param name="apiName">Name used in the timeout message.</param>
        /// <returns>A task that completes when the state is reached.</returns>
        internal Task WaitForLoadStateAsync(LoadState state, float? timeout, string apiName)
            => LifecycleWaiter.WaitAsync(
                () => LifecycleEvents,
                handler => LifecycleChanged += handler,
                handler => LifecycleChanged -= handler,
                state,
                timeout,
                apiName);

        private void OnNetworkIdleTimerFired()
        {
            lock (_inflightLock)
            {
                if (_isDetached || _inflightRequestIds.Count != 0)
                {
                    return;
                }

                _firedNetworkIdleSelf = true;
            }

            RootFrame().RecalculateNetworkIdle();
        }

        private bool HasLifecycle(string name)
        {
            lock (_inflightLock)
            {
                return _lifecycleEvents.Contains(name);
            }
        }

        private Frame RootFrame()
        {
            Frame frame = this;
            while (frame._parentFrame != null)
            {
                frame = frame._parentFrame;
            }

            return frame;
        }

        private void StartNetworkIdleTimerLocked()
        {
            if (_isDetached || _firedNetworkIdleSelf || _networkIdleTimer != null)
            {
                return;
            }

            _networkIdleTimer = new Timer(
                static state => ((Frame)state).OnNetworkIdleTimerFired(),
                this,
                NetworkIdleQuietPeriodMs,
                Timeout.Infinite);
        }

        private void StopNetworkIdleTimerLocked()
        {
            _networkIdleTimer?.Dispose();
            _networkIdleTimer = null;
            _firedNetworkIdleSelf = false;
        }
    }
}
