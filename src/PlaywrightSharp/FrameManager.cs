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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp
{
    /// <summary>
    /// Manages the frame tree for a page. Handles frame attachment,
    /// detachment, navigation, and lifecycle events from the Chrome DevTools Protocol.
    /// Mirrors the upstream <c>FrameManager</c> from <c>packages/playwright-core/src/server/frames.ts</c>.
    /// </summary>
    internal class FrameManager
    {
        private readonly ConcurrentDictionary<string, Frame> _frames = new();
        private readonly ConcurrentDictionary<string, CRExecutionContext> _pendingDefaultContexts = new();
        private readonly ActionSignalHubState _signals = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="FrameManager"/> class.
        /// </summary>
        /// <param name="mainFrameId">The frame ID for the main frame (usually the target ID).</param>
        public FrameManager(string mainFrameId)
        {
            MainFrame = new Frame(mainFrameId, parentFrame: null, url: "about:blank");
            _frames.TryAdd(mainFrameId, MainFrame);
        }

        /// <summary>
        /// Occurs when a child frame is attached to the page.
        /// </summary>
        internal event Action<Frame> FrameAttached;

        /// <summary>
        /// Occurs when a frame is detached from the page.
        /// </summary>
        internal event Action<Frame> FrameDetached;

        /// <summary>
        /// Occurs when a frame commits a new-document navigation.
        /// The string parameter is the document ID (loader ID).
        /// </summary>
        internal event Action<Frame, string> FrameNavigated;

        /// <summary>
        /// Occurs when a frame commits a new document (not same-document).
        /// Used for <c>page.consoleMessages({ filter: 'since-navigation' })</c>.
        /// </summary>
        internal event Action<Frame> FrameNavigatedToNewDocument;

        /// <summary>
        /// Gets the main frame.
        /// </summary>
        internal Frame MainFrame { get; }

        /// <summary>
        /// Gets all frames currently tracked by this manager.
        /// </summary>
        internal IReadOnlyCollection<Frame> Frames => _frames.Values.ToArray();

        /// <summary>
        /// Gets the click auto-wait barrier hub.
        /// </summary>
        internal ActionSignalHubState Signals => _signals;

        /// <summary>
        /// Returns the frame with the given ID, or null.
        /// </summary>
        /// <param name="frameId">The CDP frame ID.</param>
        /// <returns>The <see cref="Frame"/>, or null if not found.</returns>
        internal Frame FrameById(string frameId)
        {
            _frames.TryGetValue(frameId, out Frame frame);
            return frame;
        }

        /// <summary>
        /// Assigns a default execution context now, or when the frame attaches.
        /// OOPIF <c>Runtime.executionContextCreated</c> can race <c>Page.frameAttached</c>.
        /// </summary>
        /// <param name="frameId">The CDP frame / target id.</param>
        /// <param name="context">The default world for that frame.</param>
        internal void RememberDefaultContext(string frameId, CRExecutionContext context)
        {
            if (string.IsNullOrEmpty(frameId) || context == null)
            {
                return;
            }

            Frame frame = FrameById(frameId);
            if (frame != null)
            {
                frame.ExecutionContext = context;
                return;
            }

            _pendingDefaultContexts[frameId] = context;
        }

        /// <summary>
        /// Called when CDP <c>Page.frameAttached</c> fires.
        /// Creates a new child frame and adds it to the tree.
        /// </summary>
        /// <param name="frameId">The new frame's ID.</param>
        /// <param name="parentFrameId">The parent frame's ID.</param>
        internal void FrameAttachedToTarget(string frameId, string parentFrameId)
        {
            if (_frames.ContainsKey(frameId))
            {
                return;
            }

            Frame parentFrame = FrameById(parentFrameId) ?? MainFrame;
            if (parentFrame == null)
            {
                return;
            }

            Frame frame = new(frameId, parentFrame);
            parentFrame.AddChildFrame(frame);
            _frames.TryAdd(frameId, frame);
            if (_pendingDefaultContexts.TryRemove(frameId, out CRExecutionContext pending))
            {
                frame.ExecutionContext = pending;
            }

            FrameAttached?.Invoke(frame);
        }

        /// <summary>
        /// Called when CDP <c>Page.frameNavigated</c> fires for a new-document navigation.
        /// Updates the frame's URL, name, and document ID. Removes child frames.
        /// </summary>
        /// <param name="frameId">The frame that navigated.</param>
        /// <param name="url">The new URL.</param>
        /// <param name="name">The new frame name.</param>
        /// <param name="documentId">The loader ID (document ID) for this navigation.</param>
        internal void FrameCommittedNewDocumentNavigation(string frameId, string url, string name, string documentId)
        {
            Frame frame = FrameById(frameId);
            if (frame == null)
            {
                // For main frame, the frameId may change on cross-process navigation.
                if (frameId != MainFrame.FrameId)
                {
                    _frames.TryRemove(MainFrame.FrameId, out _);
                    MainFrame.FrameId = frameId;
                    _frames.TryAdd(frameId, MainFrame);
                }

                frame = MainFrame;
            }

            if (!string.IsNullOrEmpty(documentId)
                && !string.IsNullOrEmpty(frame.DocumentId)
                && string.Equals(documentId, frame.DocumentId, StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(frame.Url) && !string.IsNullOrEmpty(url))
                {
                    frame.Url = url;
                }

                return;
            }

            // Remove all child frames on new-document navigation (upstream behavior).
            RemoveChildFramesRecursively(frame);

            frame.OnNavigated(url, name, documentId);

            FrameNavigated?.Invoke(frame, documentId);
            FrameNavigatedToNewDocument?.Invoke(frame);
            if (frame.ParentFrame == null)
            {
                _signals.OnMainFrameNavigated();
            }
        }

        /// <summary>
        /// Called when CDP <c>Page.frameRequestedNavigation</c> fires for the
        /// current tab. Click auto-wait retains until this frame commits.
        /// </summary>
        /// <param name="frameId">The frame that requested navigation.</param>
        /// <param name="url">Optional requested URL (javascript: is ignored).</param>
        internal void FrameRequestedNavigation(string frameId, string url)
        {
            if (!string.IsNullOrEmpty(url)
                && url.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
            {
                return;
            }

            Frame frame = FrameById(frameId);
            if (frame == null && (string.IsNullOrEmpty(frameId) || frameId == MainFrame.FrameId))
            {
                frame = MainFrame;
            }

            if (frame == null || frame.ParentFrame != null)
            {
                return;
            }

            _signals.ExpectMainFrameNavigation();
        }

        /// <summary>
        /// Called when CDP <c>Page.navigatedWithinDocument</c> fires.
        /// Updates the frame URL without changing the document.
        /// </summary>
        /// <param name="frameId">The frame that navigated.</param>
        /// <param name="url">The new URL.</param>
        internal void FrameCommittedSameDocumentNavigation(string frameId, string url)
        {
            Frame frame = FrameById(frameId);
            if (frame == null)
            {
                return;
            }

            frame.Url = url;
            frame.OnNavigated();
            FrameNavigated?.Invoke(frame, frame.DocumentId);
            if (frame.ParentFrame == null)
            {
                _signals.OnMainFrameNavigated();
            }
        }

        /// <summary>
        /// Called when CDP <c>Page.frameDetached</c> fires.
        /// Removes the frame and all its children from the tree.
        /// </summary>
        /// <param name="frameId">The detached frame's ID.</param>
        internal void FrameDetachedFromTarget(string frameId)
        {
            Frame frame = FrameById(frameId);
            if (frame == null || frame == MainFrame)
            {
                return;
            }

            RemoveFrameRecursively(frame);
            MainFrame.RecalculateNetworkIdle();
        }

        /// <summary>
        /// Called when CDP <c>Page.lifecycleEvent</c> fires.
        /// Records the lifecycle event on the appropriate frame.
        /// </summary>
        /// <param name="frameId">The frame that received the lifecycle event.</param>
        /// <param name="name">The lifecycle event name (e.g. "load", "DOMContentLoaded").</param>
        internal void FrameLifecycleEvent(string frameId, string name)
        {
            Frame frame = FrameById(frameId);
            if (frame == null)
            {
                return;
            }

            if (name == "load" || name == "DOMContentLoaded")
            {
                frame.OnLifecycleEvent(name);
            }
        }

        /// <summary>
        /// Updates the main frame ID. Called when the first execution context
        /// reports a different frame ID than the initial target ID.
        /// </summary>
        /// <param name="newFrameId">The corrected frame ID.</param>
        internal void UpdateMainFrameId(string newFrameId)
        {
            if (MainFrame.FrameId == newFrameId)
            {
                return;
            }

            _frames.TryRemove(MainFrame.FrameId, out _);
            MainFrame.FrameId = newFrameId;
            _frames.TryAdd(newFrameId, MainFrame);
        }

        /// <summary>
        /// Official <c>removeChildFramesRecursively</c> for local ↔ remote swaps.
        /// </summary>
        /// <param name="frame">The frame whose children should be removed.</param>
        internal void RemoveChildFrames(Frame frame)
        {
            if (frame != null)
            {
                RemoveChildFramesRecursively(frame);
            }
        }

        private void RemoveChildFramesRecursively(Frame frame)
        {
            // Copy to avoid modifying collection during enumeration.
            List<Frame> children = frame.ChildFrames.ToList();
            foreach (Frame child in children)
            {
                RemoveFrameRecursively(child);
            }
        }

        private void RemoveFrameRecursively(Frame frame)
        {
            // Remove children first.
            List<Frame> children = frame.ChildFrames.ToList();
            foreach (Frame child in children)
            {
                RemoveFrameRecursively(child);
            }

            // Remove from parent.
            frame.ParentFrame?.RemoveChildFrame(frame);

            // Remove from registry.
            _frames.TryRemove(frame.FrameId, out _);

            frame.MarkDetached();
            FrameDetached?.Invoke(frame);
        }
    }
}
