/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
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
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Lightweight internal frame for the WebKit Inspector Protocol page.
    /// Tracks identity, URL, name, the parent/child tree, and <c>networkidle</c>.
    /// </summary>
    [SuppressMessage(
        "Microsoft.Usage",
        "CA1001:Types that own disposable fields should be disposable",
        Justification = "Timer lifetime is managed by MarkDetached / DisposeNetworkIdleTimer.")]
    internal sealed class WKFrame
    {
        private readonly WKFrame _parentFrame;
        private readonly List<WKFrame> _childFrames = new();
        private readonly HashSet<string> _lifecycleEvents = new();
        private readonly HashSet<string> _inflightRequestIds = new();
        private readonly object _inflightLock = new();
        private string _frameId;
        private string _url;
        private string _name;
        private bool _isDetached;
        private Timer _networkIdleTimer;
        private bool _firedNetworkIdleSelf;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKFrame"/> class.
        /// </summary>
        /// <param name="frameId">The protocol frame identifier.</param>
        /// <param name="parentFrame">The parent frame, or <c>null</c> for the main frame.</param>
        /// <param name="url">The initial URL.</param>
        /// <param name="name">The initial name.</param>
        public WKFrame(string frameId, WKFrame parentFrame, string url = "", string name = "")
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
        /// Occurs when a lifecycle event (including <c>networkidle</c>) is recorded.
        /// </summary>
        internal event Action<string> LifecycleChanged;

        /// <summary>
        /// Gets or sets the protocol frame identifier.
        /// </summary>
        internal string FrameId { get => _frameId; set => _frameId = value; }

        /// <summary>
        /// Gets the parent frame, or <c>null</c> for the main frame.
        /// </summary>
        internal WKFrame ParentFrame => _parentFrame;

        /// <summary>
        /// Gets or sets the current URL.
        /// </summary>
        internal string Url { get => _url; set => _url = value ?? string.Empty; }

        /// <summary>
        /// Gets or sets the frame name.
        /// </summary>
        internal string Name { get => _name; set => _name = value ?? string.Empty; }

        /// <summary>
        /// Gets a value indicating whether this frame has been detached.
        /// </summary>
        internal bool IsDetached => _isDetached;

        /// <summary>
        /// Gets the child frames.
        /// </summary>
        internal IReadOnlyList<WKFrame> ChildFrames => _childFrames;

        /// <summary>
        /// Gets a snapshot of recorded lifecycle events.
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
        /// Adds a child frame.
        /// </summary>
        /// <param name="frame">The child to add.</param>
        internal void AddChildFrame(WKFrame frame) => _childFrames.Add(frame);

        /// <summary>
        /// Removes a child frame.
        /// </summary>
        /// <param name="frame">The child to remove.</param>
        internal void RemoveChildFrame(WKFrame frame) => _childFrames.Remove(frame);

        /// <summary>
        /// Marks this frame detached.
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
        /// Disposes the network-idle timer.
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
        /// Clears remaining inflight IDs when this frame is being detached.
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
        /// Clears lifecycle and inflight state for a new document.
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
        /// Records a lifecycle event.
        /// </summary>
        /// <param name="name">The event name.</param>
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
        /// Called when a non-excluded request starts on this frame.
        /// </summary>
        /// <param name="requestId">The protocol request identifier.</param>
        internal void OnInflightRequestStarted(string requestId)
        {
            if (string.IsNullOrEmpty(requestId))
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
        /// Called when a request on this frame finishes or fails.
        /// </summary>
        /// <param name="requestId">The protocol request identifier.</param>
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
        /// Walks this frame and its descendants and records <c>networkidle</c> when
        /// the whole subtree is idle.
        /// </summary>
        /// <param name="allowRemove">Frame whose ancestors may drop a stale <c>networkidle</c>.</param>
        internal void RecalculateNetworkIdle(WKFrame allowRemove = null)
        {
            bool selfIdle;
            lock (_inflightLock)
            {
                selfIdle = _firedNetworkIdleSelf && !_isDetached;
            }

            bool isNetworkIdle = selfIdle;
            WKFrame[] children = _childFrames.ToArray();
            foreach (WKFrame child in children)
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
        /// Waits until this frame has reached <paramref name="state"/>.
        /// </summary>
        /// <param name="state">The load state to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds.</param>
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

        private WKFrame RootFrame()
        {
            WKFrame frame = this;
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
                static state => ((WKFrame)state).OnNetworkIdleTimerFired(),
                this,
                NetworkIdleRules.QuietPeriodMs,
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
