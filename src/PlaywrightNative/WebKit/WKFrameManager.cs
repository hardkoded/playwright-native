/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Manages the WebKit frame tree from <c>Page.getResourceTree</c> and
    /// <c>Page.frameAttached</c> / <c>frameDetached</c> / <c>frameNavigated</c>.
    /// </summary>
    internal sealed class WKFrameManager
    {
        private readonly ConcurrentDictionary<string, WKFrame> _frames = new();
        private readonly ActionSignalHubState _signals = new();

        /// <summary>
        /// Initializes a new instance of the <see cref="WKFrameManager"/> class.
        /// </summary>
        /// <param name="mainFrameId">Initial main-frame id (updated from the resource tree).</param>
        public WKFrameManager(string mainFrameId)
        {
            MainFrame = new WKFrame(mainFrameId, parentFrame: null);
            _frames.TryAdd(mainFrameId, MainFrame);
        }

        /// <summary>
        /// Occurs when a child frame is attached.
        /// </summary>
        internal event Action<WKFrame> FrameAttached;

        /// <summary>
        /// Occurs when a frame is detached.
        /// </summary>
        internal event Action<WKFrame> FrameDetached;

        /// <summary>
        /// Occurs when a frame commits navigation.
        /// </summary>
        internal event Action<WKFrame> FrameNavigated;

        /// <summary>
        /// Gets the main frame.
        /// </summary>
        internal WKFrame MainFrame { get; }

        /// <summary>
        /// Gets all tracked frames.
        /// </summary>
        internal IReadOnlyCollection<WKFrame> Frames => _frames.Values.ToArray();

        /// <summary>
        /// Gets the click auto-wait barrier hub.
        /// </summary>
        internal ActionSignalHubState Signals => _signals;

        /// <summary>
        /// Returns the frame with the given id, or <see langword="null"/>.
        /// </summary>
        /// <param name="frameId">The protocol frame id.</param>
        /// <returns>The frame, or <see langword="null"/>.</returns>
        internal WKFrame FrameById(string frameId)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return null;
            }

            _frames.TryGetValue(frameId, out WKFrame frame);
            return frame;
        }

        /// <summary>
        /// Adopts the <c>Page.getResourceTree</c> payload without firing public events.
        /// </summary>
        /// <param name="frameTree">The <c>frameTree</c> object.</param>
        internal void AdoptResourceTree(JsonElement frameTree)
        {
            AdoptTreeNode(frameTree, parent: null, fireEvents: false);
        }

        /// <summary>
        /// Called when <c>Page.frameAttached</c> fires.
        /// </summary>
        /// <param name="frameId">The new frame id.</param>
        /// <param name="parentFrameId">The parent frame id.</param>
        /// <param name="fireEvent">Whether to raise <see cref="FrameAttached"/>.</param>
        internal void FrameAttachedToTarget(string frameId, string parentFrameId, bool fireEvent = true)
        {
            if (string.IsNullOrEmpty(frameId) || _frames.ContainsKey(frameId))
            {
                return;
            }

            WKFrame parentFrame = FrameById(parentFrameId);
            if (parentFrame == null)
            {
                return;
            }

            WKFrame frame = new(frameId, parentFrame);
            parentFrame.AddChildFrame(frame);
            _frames.TryAdd(frameId, frame);

            if (fireEvent)
            {
                FrameAttached?.Invoke(frame);
            }
        }

        /// <summary>
        /// Called when <c>Page.frameNavigated</c> fires.
        /// </summary>
        /// <param name="frameId">The frame that navigated.</param>
        /// <param name="url">The new URL.</param>
        /// <param name="name">The new name.</param>
        /// <param name="parentFrameId">Optional parent id when the frame is not yet tracked.</param>
        /// <param name="fireEvent">Whether to raise <see cref="FrameNavigated"/>.</param>
        internal void FrameCommittedNavigation(string frameId, string url, string name, string parentFrameId = null, bool fireEvent = true)
        {
            if (string.IsNullOrEmpty(frameId))
            {
                return;
            }

            WKFrame frame = FrameById(frameId);
            if (frame == null)
            {
                if (!string.IsNullOrEmpty(parentFrameId))
                {
                    FrameAttachedToTarget(frameId, parentFrameId, fireEvent);
                    frame = FrameById(frameId);
                }
                else if (frameId != MainFrame.FrameId && string.IsNullOrEmpty(parentFrameId))
                {
                    UpdateMainFrameId(frameId);
                    frame = MainFrame;
                }
                else
                {
                    frame = MainFrame;
                }
            }

            if (frame == null)
            {
                return;
            }

            RemoveChildFramesRecursively(frame);

            frame.Url = url ?? string.Empty;
            frame.Name = name ?? string.Empty;
            frame.ClearLifecycleEvents();

            if (fireEvent)
            {
                FrameNavigated?.Invoke(frame);
                if (frame.ParentFrame == null)
                {
                    _signals.OnMainFrameNavigated();
                }
            }
        }

        /// <summary>
        /// Called when <c>Page.willCheckNavigationPolicy</c> fires.
        /// Click auto-wait retains until this frame commits.
        /// </summary>
        /// <param name="frameId">The frame that requested navigation.</param>
        internal void FrameRequestedNavigation(string frameId)
        {
            WKFrame frame = FrameById(frameId);
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
        /// Called when <c>Page.navigatedWithinDocument</c> fires (hash / history).
        /// Updates the frame URL without replacing the document.
        /// </summary>
        /// <param name="frameId">The frame that navigated.</param>
        /// <param name="url">The new URL, including fragment.</param>
        internal void FrameCommittedSameDocumentNavigation(string frameId, string url)
        {
            WKFrame frame = FrameById(frameId);
            if (frame == null)
            {
                if (frameId == MainFrame.FrameId || string.IsNullOrEmpty(frameId))
                {
                    frame = MainFrame;
                }
                else
                {
                    return;
                }
            }

            frame.Url = url ?? string.Empty;
            FrameNavigated?.Invoke(frame);
            if (frame.ParentFrame == null)
            {
                _signals.OnMainFrameNavigated();
            }
        }

        /// <summary>
        /// Called when <c>Page.frameDetached</c> fires.
        /// </summary>
        /// <param name="frameId">The detached frame id.</param>
        internal void FrameDetachedFromTarget(string frameId)
        {
            WKFrame frame = FrameById(frameId);
            if (frame == null || frame == MainFrame)
            {
                return;
            }

            RemoveFrameRecursively(frame);
        }

        /// <summary>
        /// Updates the main frame id after the resource tree reports a different id.
        /// </summary>
        /// <param name="newFrameId">The corrected frame id.</param>
        internal void UpdateMainFrameId(string newFrameId)
        {
            if (string.IsNullOrEmpty(newFrameId) || MainFrame.FrameId == newFrameId)
            {
                return;
            }

            _frames.TryRemove(MainFrame.FrameId, out _);
            MainFrame.FrameId = newFrameId;
            _frames.TryAdd(newFrameId, MainFrame);
        }

        private void AdoptTreeNode(JsonElement tree, WKFrame parent, bool fireEvents)
        {
            if (tree.ValueKind != JsonValueKind.Object
                || !tree.TryGetProperty("frame", out JsonElement frameEl)
                || frameEl.ValueKind != JsonValueKind.Object)
            {
                return;
            }

            string id = frameEl.TryGetProperty("id", out JsonElement idEl) && idEl.ValueKind == JsonValueKind.String
                ? idEl.GetString()
                : null;
            string url = frameEl.TryGetProperty("url", out JsonElement urlEl) && urlEl.ValueKind == JsonValueKind.String
                ? urlEl.GetString()
                : string.Empty;
            string name = frameEl.TryGetProperty("name", out JsonElement nameEl) && nameEl.ValueKind == JsonValueKind.String
                ? nameEl.GetString()
                : string.Empty;

            if (string.IsNullOrEmpty(id))
            {
                return;
            }

            WKFrame current;
            if (parent == null)
            {
                UpdateMainFrameId(id);
                MainFrame.Url = url;
                MainFrame.Name = name;
                current = MainFrame;
            }
            else
            {
                FrameAttachedToTarget(id, parent.FrameId, fireEvents);
                current = FrameById(id);
                if (current != null)
                {
                    current.Url = url;
                    current.Name = name;
                }
            }

            if (current == null)
            {
                return;
            }

            if (tree.TryGetProperty("childFrames", out JsonElement children) && children.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement child in children.EnumerateArray())
                {
                    AdoptTreeNode(child, current, fireEvents);
                }
            }
        }

        private void RemoveChildFramesRecursively(WKFrame frame)
        {
            List<WKFrame> children = frame.ChildFrames.ToList();
            foreach (WKFrame child in children)
            {
                RemoveFrameRecursively(child);
            }
        }

        private void RemoveFrameRecursively(WKFrame frame)
        {
            List<WKFrame> children = frame.ChildFrames.ToList();
            foreach (WKFrame child in children)
            {
                RemoveFrameRecursively(child);
            }

            frame.ParentFrame?.RemoveChildFrame(frame);
            _frames.TryRemove(frame.FrameId, out _);
            frame.MarkDetached();
            FrameDetached?.Invoke(frame);
            MainFrame.RecalculateNetworkIdle();
        }
    }
}
