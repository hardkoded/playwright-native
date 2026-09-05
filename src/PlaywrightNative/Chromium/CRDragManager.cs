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
            MouseButton button,
            IReadOnlyCollection<KeyboardModifier> modifiers,
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

            if (button != MouseButton.Left)
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
                }
                finally
                {
                    client.MessageReceived -= OnDragIntercepted;
                    await client.SendAsync("Input.setInterceptDrags", new { enabled = false }).ConfigureAwait(false);
                }

                _dragState = expectingDrag
                    ? await dragInterceptedTcs.Task.ConfigureAwait(false)
                    : null;
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
        internal async Task DropAsync(double x, double y, IReadOnlyCollection<KeyboardModifier> modifiers)
        {
            if (!_dragState.HasValue)
            {
                throw new PlaywrightNativeException("missing drag state");
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
        }

        private async Task EvaluateInAllFramesAsync(string expression)
        {
            foreach (Frame frame in _page.FrameManager.Frames)
            {
                try
                {
                    CRExecutionContext context = frame.ExecutionContext;
                    if (context == null)
                    {
                        continue;
                    }

                    await context.EvaluateAsync<object>(expression)
                        .WaitAsync(TimeSpan.FromSeconds(2))
                        .ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
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
                    CRExecutionContext context = frame.ExecutionContext;
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
                catch (PlaywrightNativeException)
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
    }
}
