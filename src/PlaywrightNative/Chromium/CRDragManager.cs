/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;
using PlaywrightNative.Input;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Chromium HTML5 drag interceptor. Mirrors upstream <c>crDragDrop.ts</c>
    /// <c>DragManager</c>: intercept native drags via <c>Input.setInterceptDrags</c>,
    /// capture <c>Input.dragIntercepted</c> payload (without Chromium's internal
    /// <c>chromium/x-drag-id</c> mime), then replay <c>Input.dispatchDragEvent</c>.
    /// </summary>
    internal sealed class CRDragManager
    {
        // Upstream setupDragListeners body, evaluated in every frame before a
        // potential drag-starting mouse move.
        private const string SetupDragListenersScript = @"(() => {
  let didStartDrag = Promise.resolve(false);
  let dragEvent = null;
  const dragListener = (event) => { dragEvent = event; };
  const mouseListener = () => {
    didStartDrag = new Promise((callback) => {
      window.addEventListener('dragstart', dragListener, { once: true, capture: true });
      setTimeout(() => callback(dragEvent ? !dragEvent.defaultPrevented : false), 0);
    });
  };
  window.addEventListener('mousemove', mouseListener, { once: true, capture: true });
  window.__cleanupDrag = async () => {
    const val = await didStartDrag;
    window.removeEventListener('mousemove', mouseListener, { capture: true });
    window.removeEventListener('dragstart', dragListener, { capture: true });
    delete window.__cleanupDrag;
    return val;
  };
})()";

        private const string CleanupDragScript = "window.__cleanupDrag?.()";

        private readonly CRPage _page;
        private JsonElement? _dragState;
        private double _lastX;
        private double _lastY;
        private double _lastDownX;
        private double _lastDownY;
        private bool _hasLastDown;

        // When the document has no [draggable=true], skip setInterceptDrags for
        // this press — mid-gesture intercept corrupts textarea selection under
        // headful Chromium suite load (ShouldSelectTheTextWithMouse).
        private bool _skipInterceptThisPress;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRDragManager"/> class.
        /// </summary>
        /// <param name="page">Owning Chromium page.</param>
        public CRDragManager(CRPage page)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
        }

        /// <summary>
        /// Whether an intercepted HTML5 drag is in progress.
        /// </summary>
        internal bool IsDragging => _dragState.HasValue;

        /// <summary>
        /// Records the last mouse-down point before <c>mousePressed</c> is sent.
        /// </summary>
        /// <param name="x">Down x.</param>
        /// <param name="y">Down y.</param>
        internal void BeginMouseDown(double x, double y)
        {
            _lastDownX = x;
            _lastDownY = y;
            _hasLastDown = true;

            // Fail closed until CompleteMouseDownAsync finishes: a held move that
            // races the probe must not enable setInterceptDrags by accident.
            _skipInterceptThisPress = true;
        }

        /// <summary>
        /// Latches whether HTML5 drag intercept is needed for this press.
        /// Call after <c>mousePressed</c> so text-selection caret placement is
        /// not delayed by CDP hit-tests under headful suite load
        /// (<c>ShouldSelectTheTextWithMouse</c>).
        /// </summary>
        /// <param name="x">Down x.</param>
        /// <param name="y">Down y.</param>
        /// <returns>A task that completes when the latch is updated.</returns>
        internal async Task CompleteMouseDownAsync(double x, double y)
        {
            if (!_hasLastDown)
            {
                return;
            }

            // Skip HTML5 intercept when the press is text selection (textarea/
            // input/contenteditable) OR the document has no [draggable=true].
            // Mid-gesture setInterceptDrags corrupts headed Chromium selection
            // under suite load (ShouldSelectTheTextWithMouse). Prefer skipping
            // on probe failure so we do not enable intercept by accident.
            bool textSelect = await IsTextSelectGestureAsync(x, y).ConfigureAwait(false);
            bool hasDraggable = !textSelect && await DocumentHasDraggableAsync().ConfigureAwait(false);
            _skipInterceptThisPress = textSelect || !hasDraggable;

            // After mousePressed, let the caret / scroll position commit before
            // the held move starts selecting. Without this, headful suite load
            // can leave the textarea scrolled so (x+2,y+2) anchors near the end
            // ("t goes." instead of the full value).
            if (_skipInterceptThisPress)
            {
                await SettleFramesAsync().ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Clears the last mouse-down point after mouse-up / cancelled drag.
        /// </summary>
        internal void NoteMouseUp()
        {
            _hasLastDown = false;
            _skipInterceptThisPress = false;
        }

        /// <summary>
        /// Cancels an in-flight intercepted drag (Escape). Returns
        /// <see langword="true"/> when a drag was cancelled.
        /// </summary>
        /// <returns>Whether a drag was cancelled.</returns>
        internal async Task<bool> CancelDragAsync()
        {
            if (!_dragState.HasValue)
            {
                return false;
            }

            await _page.Session.SendAsync("Input.dispatchDragEvent", new
            {
                type = "dragCancel",
                x = _lastX,
                y = _lastY,
                data = new
                {
                    items = Array.Empty<object>(),
                    dragOperationsMask = 65535,
                },
            }).ConfigureAwait(false);
            _dragState = null;
            _hasLastDown = false;
            _skipInterceptThisPress = false;
            return true;
        }

        /// <summary>
        /// Intercepts a drag that may start from this left-button move, or
        /// dispatches <c>dragOver</c> when already dragging. Otherwise runs
        /// <paramref name="moveCallback"/> as a normal mouse move.
        /// </summary>
        /// <param name="x">Target x.</param>
        /// <param name="y">Target y.</param>
        /// <param name="button">Active button for the move.</param>
        /// <param name="modifiers">Keyboard modifiers.</param>
        /// <param name="moveCallback">Underlying <c>mouseMoved</c> dispatch.</param>
        /// <returns>A task that completes when interception / move finishes.</returns>
        internal async Task InterceptDragCausedByMoveAsync(
            double x,
            double y,
            Input.MouseButton button,
            IReadOnlyCollection<Input.KeyboardModifier> modifiers,
            Func<Task> moveCallback)
        {
            _lastX = x;
            _lastY = y;

            if (_dragState.HasValue)
            {
                await _page.Session.SendAsync("Input.dispatchDragEvent", new
                {
                    type = "dragOver",
                    x,
                    y,
                    data = _dragState.Value,
                    modifiers = modifiers.ToCdpMask(),
                }).ConfigureAwait(false);
                return;
            }

            if (button != Input.MouseButton.Left)
            {
                await moveCallback().ConfigureAwait(false);
                return;
            }

            // No [draggable=true] / text select / no recorded down: skip
            // setInterceptDrags so textarea selection is not corrupted under
            // headful suite load. Keep a single mouseMoved (no auto-steps) —
            // multi-step breaks event-list parity for non-drag held moves.
            if (!_hasLastDown || _skipInterceptThisPress)
            {
                await moveCallback().ConfigureAwait(false);
                return;
            }

            CRSession client = _page.Session;
            TaskCompletionSource<JsonElement> dragInterceptedTcs =
                new TaskCompletionSource<JsonElement>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnDragIntercepted(string method, JsonElement? paramsElement)
            {
                if (method != "Input.dragIntercepted" || !paramsElement.HasValue)
                {
                    return;
                }

                if (paramsElement.Value.TryGetProperty("data", out JsonElement data))
                {
                    dragInterceptedTcs.TrySetResult(data.Clone());
                }
            }

            try
            {
                await EvaluateInAllFramesAsync(SetupDragListenersScript).ConfigureAwait(false);
                client.MessageReceived += OnDragIntercepted;
                await client.SendAsync("Input.setInterceptDrags", new { enabled = true }).ConfigureAwait(false);

                bool expectingDrag;
                try
                {
                    await moveCallback().ConfigureAwait(false);
                    expectingDrag = await CleanupDragInAllFramesAsync().ConfigureAwait(false);

                    // Await dragIntercepted while the handler is still subscribed.
                    // Unsubscribing first (prior finally) dropped late Input.dragIntercepted
                    // events and hung ShouldWork forever under Windows headless suite load.
                    if (expectingDrag)
                    {
                        Task completed = await Task.WhenAny(
                                dragInterceptedTcs.Task,
                                Task.Delay(5_000))
                            .ConfigureAwait(false);
                        if (completed != dragInterceptedTcs.Task)
                        {
                            throw new PlaywrightException(
                                "Input.dragIntercepted was not received within 5s after dragstart");
                        }

                        _dragState = await dragInterceptedTcs.Task.ConfigureAwait(false);
                    }
                    else
                    {
                        _dragState = null;
                    }
                }
                finally
                {
                    client.MessageReceived -= OnDragIntercepted;
                    await client.SendAsync("Input.setInterceptDrags", new { enabled = false }).ConfigureAwait(false);
                }
            }
            catch
            {
                _ = CleanupDragInAllFramesAsync();
                throw;
            }

            if (_dragState.HasValue)
            {
                await client.SendAsync("Input.dispatchDragEvent", new
                {
                    type = "dragEnter",
                    x,
                    y,
                    data = _dragState.Value,
                    modifiers = modifiers.ToCdpMask(),
                }).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Completes the intercepted drag with a <c>drop</c> at the current point.
        /// </summary>
        /// <param name="x">Drop x.</param>
        /// <param name="y">Drop y.</param>
        /// <param name="modifiers">Keyboard modifiers.</param>
        /// <returns>A task that completes when drop is dispatched.</returns>
        internal async Task DropAsync(double x, double y, IReadOnlyCollection<Input.KeyboardModifier> modifiers)
        {
            if (!_dragState.HasValue)
            {
                throw new PlaywrightException("missing drag state");
            }

            await _page.Session.SendAsync("Input.dispatchDragEvent", new
            {
                type = "drop",
                x,
                y,
                data = _dragState.Value,
                modifiers = modifiers.ToCdpMask(),
            }).ConfigureAwait(false);
            _dragState = null;
            _hasLastDown = false;
            _skipInterceptThisPress = false;
        }

        private async Task SettleFramesAsync()
        {
            try
            {
                Frame main = _page.FrameManager.MainFrame;
                if (main == null)
                {
                    return;
                }

                CRExecutionContext context = await _page.GetUtilityWorldAsync(main)
                    .WaitAsync(TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
                if (context == null)
                {
                    return;
                }

                await context.EvaluateAsync<object>(
                        "new Promise(r => requestAnimationFrame(() => requestAnimationFrame(() => r(true))))")
                    .WaitAsync(TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }
        }

        private async Task<bool> DocumentHasDraggableAsync()
        {
            try
            {
                Frame main = _page.FrameManager.MainFrame;
                if (main == null)
                {
                    return false;
                }

                CRExecutionContext context = await _page.GetUtilityWorldAsync(main)
                    .WaitAsync(TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
                if (context == null)
                {
                    return false;
                }

                bool? has = await context.EvaluateAsync<bool?>(
                        "!!document.querySelector('[draggable=true], [draggable=\"\"]')")
                    .WaitAsync(TimeSpan.FromMilliseconds(500))
                    .ConfigureAwait(false);
                return has == true;
            }
            catch (PlaywrightException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }

            // Fail closed for intercept: under suite load a missed probe must
            // not enable setInterceptDrags and corrupt text selection.
            return false;
        }

        /// <summary>
        /// Hit-tests the mouse-down point via CDP (never main-world evaluate)
        /// and returns whether it is a text-editing target where HTML5 drag
        /// intercept would corrupt selection.
        /// </summary>
        /// <param name="x">Down x.</param>
        /// <param name="y">Down y.</param>
        /// <returns><see langword="true"/> when the target is a text field.</returns>
        private async Task<bool> IsTextSelectGestureAsync(double x, double y)
        {
            try
            {
                JsonElement? located = await _page.Session.SendAsync(
                    "DOM.getNodeForLocation",
                    new
                    {
                        x = Math.Floor(x),
                        y = Math.Floor(y),
                        includeUserAgentShadowDOM = true,
                    })
                    .WaitAsync(TimeSpan.FromMilliseconds(250))
                    .ConfigureAwait(false);
                if (!located.HasValue
                    || !located.Value.TryGetProperty("backendNodeId", out JsonElement backendEl)
                    || !backendEl.TryGetInt32(out int backendNodeId)
                    || backendNodeId == 0)
                {
                    return false;
                }

                JsonElement? described = await _page.Session.SendAsync(
                    "DOM.describeNode",
                    new { backendNodeId, depth = 0 })
                    .WaitAsync(TimeSpan.FromMilliseconds(250))
                    .ConfigureAwait(false);
                if (!described.HasValue
                    || !described.Value.TryGetProperty("node", out JsonElement node)
                    || !node.TryGetProperty("nodeName", out JsonElement nameEl))
                {
                    return false;
                }

                string nodeName = nameEl.GetString();
                if (string.IsNullOrEmpty(nodeName))
                {
                    return false;
                }

                if (nodeName.Equals("TEXTAREA", StringComparison.OrdinalIgnoreCase)
                    || nodeName.Equals("INPUT", StringComparison.OrdinalIgnoreCase))
                {
                    return true;
                }

                if (node.TryGetProperty("attributes", out JsonElement attrs)
                    && attrs.ValueKind == JsonValueKind.Array)
                {
                    string pendingName = null;
                    foreach (JsonElement item in attrs.EnumerateArray())
                    {
                        if (pendingName == null)
                        {
                            pendingName = item.GetString();
                            continue;
                        }

                        string value = item.GetString();
                        if (string.Equals(pendingName, "contenteditable", StringComparison.OrdinalIgnoreCase)
                            && !string.IsNullOrEmpty(value)
                            && !string.Equals(value, "false", StringComparison.OrdinalIgnoreCase))
                        {
                            return true;
                        }

                        pendingName = null;
                    }
                }
            }
            catch (PlaywrightException)
            {
            }
            catch (TimeoutException)
            {
            }
            catch (OperationCanceledException)
            {
            }

            return false;
        }

        private async Task EvaluateInAllFramesAsync(string expression)
        {
            // Official crDragDrop uses the utility world so setup/cleanup does not
            // run in the page main world during an in-flight mouse gesture (headful
            // Chromium otherwise drops textarea text selection).
            foreach (Frame frame in _page.FrameManager.Frames)
            {
                try
                {
                    CRExecutionContext context = await UtilityContextAsync(frame).ConfigureAwait(false);
                    if (context == null)
                    {
                        continue;
                    }

                    await context.EvaluateAsync<object>(expression)
                        .WaitAsync(TimeSpan.FromSeconds(2))
                        .ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
                catch (OperationCanceledException)
                {
                }
            }
        }

        private async Task<bool> CleanupDragInAllFramesAsync()
        {
            bool any = false;
            foreach (Frame frame in _page.FrameManager.Frames)
            {
                try
                {
                    CRExecutionContext context = await UtilityContextAsync(frame).ConfigureAwait(false);
                    if (context == null)
                    {
                        continue;
                    }

                    bool? started = await context.EvaluateAsync<bool?>(CleanupDragScript)
                        .WaitAsync(TimeSpan.FromSeconds(2))
                        .ConfigureAwait(false);
                    if (started == true)
                    {
                        any = true;
                    }
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
                catch (OperationCanceledException)
                {
                }
            }

            return any;
        }

        private async Task<CRExecutionContext> UtilityContextAsync(Frame frame)
        {
            try
            {
                return await _page.GetUtilityWorldAsync(frame).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Never fall back to the page main world: evaluating
                // setup/cleanup there mid-gesture drops textarea text
                // selection under headful Chromium suite load
                // (ShouldSelectTheTextWithMouse). Skip the frame instead —
                // missing drag listeners only weakens HTML5 drag intercept,
                // which is preferable to corrupting selection.
                return null;
            }
            catch (TimeoutException)
            {
                return null;
            }
        }
    }
}
