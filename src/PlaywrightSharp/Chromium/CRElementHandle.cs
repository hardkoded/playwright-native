/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
#pragma warning disable SA1201
#pragma warning disable CA2000
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.Input;

namespace PlaywrightSharp.Chromium
{
    /// <summary>
    /// Handle to a DOM node in the browser. Exposes DOM-specific operations like
    /// <see cref="FocusAsync"/> and <see cref="BoundingBoxAsync"/>. Produced by
    /// <c>CRPage.QuerySelectorAsync</c> (added in a later task).
    /// </summary>
    internal class CRElementHandle : CRJSHandle
    {
        private readonly CRPage _page;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRElementHandle"/> class.
        /// </summary>
        /// <param name="page">The owning page (needed for future Mouse/Keyboard access in click/fill).</param>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The CDP remote object ID.</param>
        /// <param name="preview">Initial official preview. Defaults to <c>JSHandle@node</c>.</param>
        public CRElementHandle(CRPage page, CRExecutionContext context, string objectId, string preview = null)
            : base(context, objectId, preview ?? "JSHandle@node")
        {
            _page = page;
            _ = InitializePreviewAsync();
        }

        /// <summary>
        /// Gets the owning page.
        /// </summary>
        internal CRPage Page => _page;

        /// <summary>
        /// The page keyboard, used to hold modifiers during clicks.
        /// </summary>
        internal Input.Keyboard Keyboard => _page.Keyboard;

        /// <summary>
        /// Focuses the element via <c>node.focus()</c> in the browser.
        /// </summary>
        /// <returns>A task that completes when the element has been focused.</returns>
        internal Task FocusAsync(bool preventScroll = false)
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<bool>(ElementStateScript.FocusFunction, preventScroll);
        }

        /// <summary>
        /// Returns the element's bounding box relative to the main-frame viewport,
        /// or <c>null</c> if the element has no layout (detached, display:none).
        /// Uses <c>getBoundingClientRect</c> (layout flush, SVG, inline overflow)
        /// plus parent-iframe offset. CDP <c>DOM.getBoxModel</c> is the HTML
        /// fallback when the client rect is unavailable.
        /// </summary>
        /// <returns>The <see cref="BoundingBox"/>, or <c>null</c>.</returns>
        internal async Task<BoundingBox?> BoundingBoxAsync()
        {
            EnsureNotDisposed();

            BoundingBox? local = await LocalBoundingBoxAsync().ConfigureAwait(false);
            if (local == null)
            {
                return null;
            }

            BoundingBox box = local.Value;
            IFrame owner = await OwnerFrameAsync().ConfigureAwait(false);
            (double offsetX, double offsetY) = await BoundingBoxHelper.OwnerFrameOffsetAsync(owner).ConfigureAwait(false);
            return new BoundingBox(box.X + offsetX, box.Y + offsetY, box.Width, box.Height);
        }

        /// <summary>
        /// Clicks the element using the page's mouse. Defaults to the bounding-box
        /// center. When <paramref name="position"/> is set, clicks that offset from
        /// the top-left of the box. No retry, no scroll-into-view, no visibility
        /// waits — minimal port.
        /// Throws <see cref="PlaywrightSharpException"/> if the element has no layout.
        /// </summary>
        /// <param name="button">Which mouse button to use.</param>
        /// <param name="clickCount">Number of consecutive clicks (1 = click, 2 = dblclick).</param>
        /// <param name="delayMs">Delay between mousedown and mouseup.</param>
        /// <param name="position">Optional offset from the element's top-left corner.</param>
        /// <param name="steps">Intermediate <c>mousemove</c> segments. Defaults to 1.</param>
        internal async Task ClickAsync(Input.MouseButton button = Input.MouseButton.Left, int clickCount = 1, int delayMs = 0, Position position = null, int steps = 1)
        {
            EnsureNotDisposed();

            BoundingBox? box = await BoundingBoxAsync().ConfigureAwait(false);
            if (box == null)
            {
                throw new PlaywrightSharpException("Element is not visible or has no layout.");
            }

            BoundingBox b = box.Value;
            double x = b.X + (position != null ? position.X : b.Width / 2);
            double y = b.Y + (position != null ? position.Y : b.Height / 2);

            await _page.Mouse.ClickAsync(x, y, button, clickCount, delayMs, steps).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the element's value as if the user typed it. Works on &lt;input&gt; (text,
        /// email, number, etc.) and &lt;textarea&gt;. Clears the existing value first, then
        /// inserts the new text via <c>Input.insertText</c> (fast path — no per-character
        /// keystrokes).
        /// </summary>
        /// <param name="value">The new value. Empty string clears the field.</param>
        /// <param name="preventScroll">When <see langword="true"/>, focus without scrolling.</param>
        internal Task FillAsync(string value, bool preventScroll = false)
        {
            EnsureNotDisposed();

            // Direct DOM fill so force can write hidden inputs. Keyboard insert
            // is TypeAsync; Playwright fill sets the value and fires input/change.
            return EvaluateFunctionAsync<bool>(ElementStateScript.FillFunction, value, preventScroll);
        }

        /// <summary>
        /// Focuses the element and selects its text. Matches upstream injected
        /// <c>selectText</c>: input/textarea value selection, otherwise a range
        /// over the node's contents (plain DOM and contenteditable).
        /// </summary>
        /// <param name="preventScroll">When <see langword="true"/>, focus without scrolling.</param>
        /// <returns>A task that completes when the selection has been applied.</returns>
        internal Task SelectTextAsync(bool preventScroll = false)
        {
            EnsureNotDisposed();
            return EvaluateFunctionAsync<bool>(ElementStateScript.SelectTextFunction, preventScroll);
        }

        /// <summary>
        /// Selects an option in a <c>&lt;select&gt;</c> by value. Shortcut for a single-value
        /// <see cref="SelectOptionAsync(SelectOption[])"/> call.
        /// </summary>
        /// <param name="value">The option value to select.</param>
        /// <returns>Array of <c>.value</c> strings for all selected options after the operation.</returns>
        internal Task<string[]> SelectOptionAsync(string value)
            => SelectOptionAsync(new[] { new SelectOption { Value = value } });

        /// <summary>
        /// Selects options by their values.
        /// </summary>
        /// <param name="values">The option values to select.</param>
        /// <returns>Array of <c>.value</c> strings for all selected options.</returns>
        internal Task<string[]> SelectOptionAsync(string[] values)
        {
            if (values == null)
            {
                return SelectOptionAsync(System.Array.Empty<SelectOption>());
            }

            SelectOption[] options = new SelectOption[values.Length];
            for (int i = 0; i < values.Length; i++)
            {
                options[i] = new SelectOption { Value = values[i] };
            }

            return SelectOptionAsync(options);
        }

        /// <summary>
        /// Selects options matching any of the given <see cref="SelectOption"/> descriptors.
        /// Fires <c>input</c> and <c>change</c> events.
        /// </summary>
        /// <param name="options">Option descriptors to match against.</param>
        /// <returns>Array of <c>.value</c> strings for all selected options after the operation.</returns>
        internal async Task<string[]> SelectOptionAsync(SelectOption[] options)
        {
            EnsureNotDisposed();

            object[] jsOptions;
            if (options == null || options.Length == 0)
            {
                jsOptions = System.Array.Empty<object>();
            }
            else
            {
                jsOptions = new object[options.Length];
                for (int i = 0; i < options.Length; i++)
                {
                    SelectOption o = options[i];
                    jsOptions[i] = new
                    {
                        value = o?.Value,
                        label = o?.Label,
                        index = o?.Index,
                    };
                }
            }

            // Cast to object so the params overload treats the descriptor array as a
            // single argument (otherwise the elements would be spread as separate args).
            string[] result = await EvaluateFunctionAsync<string[]>(
                @"(node, descriptors) => {
                    if (node.nodeName.toLowerCase() !== 'select') {
                        throw new Error('Element is not a <select>');
                    }
                    const optionNodes = Array.from(node.options);
                    node.value = undefined;
                    const selected = [];
                    for (const desc of descriptors) {
                        for (const option of optionNodes) {
                            const valueMatches = desc.value == null || option.value === desc.value;
                            const labelMatches = desc.label == null || option.label === desc.label;
                            const indexMatches = desc.index == null || option.index === desc.index;
                            if (valueMatches && labelMatches && indexMatches) {
                                if (option.disabled) {
                                    throw new Error('Option is disabled: ' + (option.value || option.label));
                                }
                                option.selected = true;
                                selected.push(option);
                                if (!node.multiple) break;
                            }
                        }
                        if (selected.length && !node.multiple) break;
                    }
                    node.dispatchEvent(new Event('input', { bubbles: true, composed: true }));
                    node.dispatchEvent(new Event('change', { bubbles: true }));
                    return selected.map(o => o.value);
                }",
                (object)jsOptions).ConfigureAwait(false);

            return result ?? System.Array.Empty<string>();
        }

        /// <summary>
        /// Returns whether the element (an <c>&lt;input type="checkbox"&gt;</c> or
        /// <c>&lt;input type="radio"&gt;</c>) is currently checked. Throws if the element is neither.
        /// </summary>
        /// <returns>A task that resolves to <c>true</c> if checked, <c>false</c> otherwise.</returns>
        internal async Task<bool> IsCheckedAsync()
        {
            EnsureNotDisposed();

            return await EvaluateFunctionAsync<bool>(ElementStateScript.IsCheckedFunction).ConfigureAwait(false);
        }

        /// <summary>
        /// Ensures the checkbox or radio is checked. If already checked, does nothing.
        /// Clicks the element to transition (matches upstream — triggers native form handlers).
        /// </summary>
        /// <param name="position">Optional offset from the element's top-left corner.</param>
        /// <returns>A task that completes when the element is checked.</returns>
        internal async Task CheckAsync(Position position = null)
        {
            EnsureNotDisposed();

            if (await IsCheckedAsync().ConfigureAwait(false))
            {
                return;
            }

            await ClickAsync(position: position).ConfigureAwait(false);

            if (!await IsCheckedAsync().ConfigureAwait(false))
            {
                throw new PlaywrightSharpException("Clicking the element did not check it.");
            }
        }

        /// <summary>
        /// Ensures the checkbox is unchecked. Clicks the element to transition. Throws for
        /// radios (they cannot be unchecked by clicking).
        /// </summary>
        /// <param name="position">Optional offset from the element's top-left corner.</param>
        /// <returns>A task that completes when the element is unchecked.</returns>
        internal async Task UncheckAsync(Position position = null)
        {
            EnsureNotDisposed();

            if (await EvaluateFunctionAsync<bool>(ElementStateScript.IsNativeRadioFunction).ConfigureAwait(false))
            {
                throw new PlaywrightSharpException("Cannot uncheck radio button");
            }

            if (!await IsCheckedAsync().ConfigureAwait(false))
            {
                return;
            }

            await ClickAsync(position: position).ConfigureAwait(false);

            if (await IsCheckedAsync().ConfigureAwait(false))
            {
                throw new PlaywrightSharpException("Clicking the element did not uncheck it.");
            }
        }

        /// <summary>
        /// Taps the center of the element using the page's touchscreen, or
        /// <paramref name="position"/> relative to the top-left of the box.
        /// Throws if the element has no layout.
        /// </summary>
        /// <param name="position">Optional offset from the element's top-left corner.</param>
        internal async Task TapAsync(Position position = null)
        {
            EnsureNotDisposed();

            BoundingBox? box = await BoundingBoxAsync().ConfigureAwait(false);
            if (box == null)
            {
                throw new PlaywrightSharpException("Element is not visible or has no layout.");
            }

            BoundingBox b = box.Value;
            double x = b.X + (position != null ? position.X : b.Width / 2);
            double y = b.Y + (position != null ? position.Y : b.Height / 2);

            await _page.Touchscreen.TapAsync(x, y).ConfigureAwait(false);
        }

        /// <summary>
        /// Drags this element to another element via mouse primitives. Moves to this
        /// element's bounding-box center, presses the left button, moves to
        /// <paramref name="target"/>'s bounding-box center, releases. Throws if either
        /// element has no layout.
        /// </summary>
        /// <param name="target">The drop target.</param>
        /// <param name="steps">Number of intermediate moves. Default 10.</param>
        internal async Task DragToAsync(CRElementHandle target, int steps = 10)
        {
            EnsureNotDisposed();
            if (target == null)
            {
                throw new System.ArgumentNullException(nameof(target));
            }

            target.EnsureNotDisposed();

            BoundingBox? sourceBox = await BoundingBoxAsync().ConfigureAwait(false);
            if (sourceBox == null)
            {
                throw new PlaywrightSharpException("Source element is not visible or has no layout.");
            }

            BoundingBox? targetBox = await target.BoundingBoxAsync().ConfigureAwait(false);
            if (targetBox == null)
            {
                throw new PlaywrightSharpException("Target element is not visible or has no layout.");
            }

            BoundingBox s = sourceBox.Value;
            BoundingBox t = targetBox.Value;
            double fromX = s.X + (s.Width / 2);
            double fromY = s.Y + (s.Height / 2);
            double toX = t.X + (t.Width / 2);
            double toY = t.Y + (t.Height / 2);

            await _page.Mouse.DragToAsync(fromX, fromY, toX, toY, steps).ConfigureAwait(false);
        }

        /// <summary>
        /// Sets the files on an <c>&lt;input type="file"&gt;</c> element. Each <see cref="FilePayload"/>
        /// is reconstructed into a <c>File</c> object in the browser (via <c>DataTransfer</c>)
        /// and assigned to <c>input.files</c>. Fires <c>input</c> and <c>change</c> events.
        /// </summary>
        /// <param name="files">The files to upload. Buffer contents are base64-encoded over the wire.</param>
        internal async Task SetInputFilesAsync(PlaywrightFilePayload[] files)
        {
            EnsureNotDisposed();
            if (files == null)
            {
                files = System.Array.Empty<PlaywrightFilePayload>();
            }

            object[] jsFiles = new object[files.Length];
            for (int i = 0; i < files.Length; i++)
            {
                PlaywrightFilePayload f = files[i];
                jsFiles[i] = new
                {
                    name = f?.Name ?? string.Empty,
                    mimeType = f?.MimeType ?? "application/octet-stream",
                    buffer = Convert.ToBase64String(f?.Buffer ?? System.Array.Empty<byte>()),
                    lastModified = f?.LastModified,
                    webkitRelativePath = f?.WebkitRelativePath,
                };
            }

            await EvaluateFunctionAsync<bool>(
                ElementStateScript.AssignInputFilesFromDataFunction,
                (object)jsFiles).ConfigureAwait(false);
        }

        /// <summary>
        /// Convenience overload for a single file.
        /// </summary>
        /// <param name="file">The file to upload.</param>
        internal Task SetInputFilesAsync(PlaywrightFilePayload file)
            => SetInputFilesAsync(file == null ? System.Array.Empty<PlaywrightFilePayload>() : new[] { file });

        /// <summary>Uploads a single official file payload.</summary>
        /// <param name="file">The file to upload.</param>
        internal Task SetInputFilesAsync(FilePayload file)
            => SetInputFilesAsync(PlaywrightFilePayload.FromOfficial(file));

        /// <summary>Uploads official file payloads.</summary>
        /// <param name="files">Files to upload.</param>
        internal Task SetInputFilesAsync(IEnumerable<FilePayload> files)
            => SetInputFilesAsync(System.Linq.Enumerable.ToArray(PlaywrightFilePayload.FromOfficial(files)));

        /// <summary>
        /// Sets filesystem paths on a file input via <c>DOM.setFileInputFiles</c>.
        /// Chrome reads the files from disk; contents are not sent over evaluate.
        /// </summary>
        /// <param name="paths">Local filesystem paths. Empty clears the input.</param>
        /// <returns>A task that completes when the protocol command finishes.</returns>
        internal Task SetFileInputFilesFromPathsAsync(IReadOnlyList<string> paths)
        {
            EnsureNotDisposed();
            string[] files;
            if (paths == null || paths.Count == 0)
            {
                files = System.Array.Empty<string>();
            }
            else
            {
                files = new string[paths.Count];
                for (int i = 0; i < paths.Count; i++)
                {
                    files[i] = paths[i];
                }
            }

            return _page.Session.SendAsync("DOM.setFileInputFiles", new
            {
                objectId = ObjectId,
                files,
            });
        }

        /// <summary>
        /// Moves the mouse to the center of the element, or to
        /// <paramref name="position"/> relative to the top-left of the box.
        /// Requires layout — throws <see cref="PlaywrightSharpException"/> if
        /// the element has no bounding box.
        /// </summary>
        /// <param name="position">Optional offset from the element's top-left corner.</param>
        internal async Task HoverAsync(Position position = null)
        {
            EnsureNotDisposed();

            BoundingBox? box = await BoundingBoxAsync().ConfigureAwait(false);
            if (box == null)
            {
                throw new PlaywrightSharpException("Element is not visible or has no layout.");
            }

            BoundingBox b = box.Value;
            double x = b.X + (position != null ? position.X : b.Width / 2);
            double y = b.Y + (position != null ? position.Y : b.Height / 2);

            await _page.Mouse.MoveAsync(x, y).ConfigureAwait(false);
        }

        /// <summary>
        /// Focuses the element, then types <paramref name="text"/> character-by-character
        /// via the page keyboard. Firing per-character key events (matches upstream
        /// <c>ElementHandle.type</c>). Use <see cref="FillAsync(string, bool)"/> for the fast
        /// non-typed path.
        /// </summary>
        /// <param name="text">The text to type.</param>
        /// <param name="delayMs">Per-keystroke delay in milliseconds.</param>
        /// <param name="preventScroll">When <see langword="true"/>, focus does not scroll.</param>
        internal async Task TypeAsync(string text, int delayMs = 0, bool preventScroll = false)
        {
            EnsureNotDisposed();
            string snapshot = null;
            if (preventScroll)
            {
                snapshot = await EvaluateFunctionAsync<string>(ElementStateScript.CaptureAncestorScrollsFunction).ConfigureAwait(false);
            }

            await EvaluateFunctionAsync<bool>(ElementStateScript.FocusForTypeFunction, preventScroll).ConfigureAwait(false);
            await _page.Keyboard.TypeAsync(text, delayMs).ConfigureAwait(false);
            if (preventScroll && snapshot != null)
            {
                await EvaluateFunctionAsync<bool>(ElementStateScript.RestoreAncestorScrollsFunction, snapshot).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Focuses the element, then dispatches a single key press (keydown + keyup) with
        /// optional delay between them.
        /// </summary>
        /// <param name="key">The key name (e.g. <c>"Enter"</c>, <c>"a"</c>, <c>"ArrowDown"</c>).</param>
        /// <param name="delayMs">Delay between keydown and keyup in milliseconds.</param>
        /// <param name="preventScroll">When <see langword="true"/>, focus and caret insert do not scroll.</param>
        internal async Task PressAsync(string key, int delayMs = 0, bool preventScroll = false)
        {
            EnsureNotDisposed();
            string snapshot = null;
            if (preventScroll)
            {
                snapshot = await EvaluateFunctionAsync<string>(ElementStateScript.CaptureAncestorScrollsFunction).ConfigureAwait(false);
            }

            await EvaluateFunctionAsync<bool>(ElementStateScript.FocusForTypeFunction, preventScroll).ConfigureAwait(false);
            await _page.Keyboard.PressAsync(key, delayMs).ConfigureAwait(false);
            if (preventScroll && snapshot != null)
            {
                await EvaluateFunctionAsync<bool>(ElementStateScript.RestoreAncestorScrollsFunction, snapshot).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Finds a descendant matching <paramref name="selector"/>.
        /// </summary>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>The matching element, or <see langword="null"/>.</returns>
        internal async Task<CRElementHandle> QuerySelectorAsync(string selector)
        {
            EnsureNotDisposed();
            if (FrameSelector.ContainsControl(selector))
            {
                IFrame owner = await OwnerFrameAsync().ConfigureAwait(false);
                IReadOnlyList<IElementHandle> matches = await FrameSelector.QueryAllAsync(
                    owner,
                    new ChromiumElementHandle(this),
                    selector).ConfigureAwait(false);
                return matches.Count > 0 ? _page.UnwrapPublicElement(matches[0]) : null;
            }

            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                JsonElement? custom = await EvaluateHandleRawAsync(call.ElementQueryFunction).ConfigureAwait(false);
                return _page.WrapElementHandle(Context, custom);
            }

            JsonElement? handleValue = await EvaluateHandleRawAsync(
                "(el, sel) => el.querySelector(sel)",
                selector).ConfigureAwait(false);
            return _page.WrapElementHandle(Context, handleValue);
        }

        /// <summary>
        /// Finds every descendant matching <paramref name="selector"/>.
        /// </summary>
        /// <param name="selector">A CSS selector.</param>
        /// <returns>Handles for the matching elements, in document order.</returns>
        internal async Task<IReadOnlyList<CRElementHandle>> QuerySelectorAllAsync(string selector)
        {
            EnsureNotDisposed();
            if (FrameSelector.ContainsControl(selector))
            {
                IFrame owner = await OwnerFrameAsync().ConfigureAwait(false);
                IReadOnlyList<IElementHandle> matches = await FrameSelector.QueryAllAsync(
                    owner,
                    new ChromiumElementHandle(this),
                    selector).ConfigureAwait(false);
                List<CRElementHandle> converted = new List<CRElementHandle>(matches.Count);
                for (int i = 0; i < matches.Count; i++)
                {
                    CRElementHandle inner = _page.UnwrapPublicElement(matches[i]);
                    if (inner != null)
                    {
                        converted.Add(inner);
                    }
                }

                return converted;
            }

            if (CustomSelectors.TryResolve(selector, out CustomSelectorCall call))
            {
                JsonElement? customArray = await EvaluateHandleRawAsync(call.ElementQueryAllFunction).ConfigureAwait(false);
                return await _page.UnwrapElementArrayAsync(Context, customArray).ConfigureAwait(false);
            }

            JsonElement? arrayRemote = await EvaluateHandleRawAsync(
                "(el, sel) => Array.from(el.querySelectorAll(sel))",
                selector).ConfigureAwait(false);
            return await _page.UnwrapElementArrayAsync(Context, arrayRemote).ConfigureAwait(false);
        }

        /// <summary>
        /// Returns the content frame for this iframe or frame element.
        /// </summary>
        /// <returns>The hosted frame, or <see langword="null"/>.</returns>
        internal async Task<IFrame> ContentFrameAsync()
        {
            EnsureNotDisposed();
            string frameId = await _page.DescribeNodeContentFrameIdAsync(Context.Session, ObjectId).ConfigureAwait(false);
            return _page.ResolvePublicFrameById(frameId);
        }

        /// <summary>
        /// Returns the frame that owns this element.
        /// </summary>
        /// <returns>The owning frame, or <see langword="null"/>.</returns>
        internal async Task<IFrame> OwnerFrameAsync()
        {
            EnsureNotDisposed();
            JsonElement? documentElement = await EvaluateHandleRawAsync(
                @"node => {
                    const doc = node;
                    if (doc.documentElement && doc.documentElement.ownerDocument === doc)
                        return doc.documentElement;
                    return node.ownerDocument ? node.ownerDocument.documentElement : null;
                }").ConfigureAwait(false);

            string documentElementId = RemoteObject.GetObjectId(documentElement);
            if (string.IsNullOrEmpty(documentElementId))
            {
                return null;
            }

            try
            {
                string frameId = await _page.DescribeNodeFrameIdAsync(Context.Session, documentElementId).ConfigureAwait(false);
                return _page.ResolvePublicFrameById(frameId);
            }
            finally
            {
                await Context.ReleaseHandleAsync(documentElementId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Scrolls this element into view and captures a screenshot of its bounding box.
        /// </summary>
        /// <param name="path">Optional file path to write the image to.</param>
        /// <param name="type">Image format. Defaults to PNG.</param>
        /// <param name="quality">JPEG quality (0-100). Ignored for PNG.</param>
        /// <param name="omitBackground">Hide the default white background (PNG only).</param>
        /// <returns>The image bytes.</returns>
        internal async Task<byte[]> ScreenshotAsync(
            string path = default,
            ScreenshotType type = default,
            int? quality = default,
            bool? omitBackground = default)
        {
            EnsureNotDisposed();
            await EvaluateFunctionAsync<bool>(ElementStateScript.ScrollIntoViewIfNeededFunction).ConfigureAwait(false);

            BoundingBox? box = await BoundingBoxAsync().ConfigureAwait(false);
            if (box == null || box.Value.Width <= 0 || box.Value.Height <= 0)
            {
                throw new PlaywrightSharpException("Node is either not visible or not an HTMLElement");
            }

            BoundingBox b = box.Value;
            byte[] bytes = await _page.ScreenshotAsync(new ScreenshotOptions
            {
                Format = ScreenshotFormat.ToProtocol(type),
                Quality = quality,
                OmitBackground = omitBackground == true,
                Clip = new ScreenshotClip
                {
                    X = b.X,
                    Y = b.Y,
                    Width = b.Width,
                    Height = b.Height,
                },
            }).ConfigureAwait(false);

            if (!string.IsNullOrEmpty(path))
            {
                PathIo.WriteBytes(path, bytes);
            }

            return bytes;
        }

        private async Task<BoundingBox?> LocalBoundingBoxAsync()
        {
            float[] rect = await EvaluateFunctionAsync<float[]>(BoundingBoxHelper.ClientRectFunction).ConfigureAwait(false);
            if (rect != null && rect.Length >= 4)
            {
                return new BoundingBox(rect[0], rect[1], rect[2], rect[3]);
            }

            return await TryGetBoxModelAsync().ConfigureAwait(false);
        }

        private async Task<BoundingBox?> TryGetBoxModelAsync()
        {
            JsonElement? response;
            try
            {
                response = await _page.Session.SendAsync("DOM.getBoxModel", new { objectId = ObjectId }).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
                return null;
            }

            if (!response.HasValue)
            {
                return null;
            }

            if (!response.Value.TryGetProperty("model", out JsonElement model))
            {
                return null;
            }

            if (!model.TryGetProperty("border", out JsonElement border) || border.GetArrayLength() < 8)
            {
                return null;
            }

            double[] quad = new double[8];
            for (int i = 0; i < 8; i++)
            {
                quad[i] = border[i].GetDouble();
            }

            double minX = System.Math.Min(System.Math.Min(quad[0], quad[2]), System.Math.Min(quad[4], quad[6]));
            double maxX = System.Math.Max(System.Math.Max(quad[0], quad[2]), System.Math.Max(quad[4], quad[6]));
            double minY = System.Math.Min(System.Math.Min(quad[1], quad[3]), System.Math.Min(quad[5], quad[7]));
            double maxY = System.Math.Max(System.Math.Max(quad[1], quad[3]), System.Math.Max(quad[5], quad[7]));

            return new BoundingBox(minX, minY, maxX - minX, maxY - minY);
        }

        private async Task InitializePreviewAsync()
        {
            try
            {
                string nodePreview = await EvaluateFunctionAsync<string>(RemoteObject.PreviewNodeFunction)
                    .ConfigureAwait(false);
                if (!string.IsNullOrEmpty(nodePreview))
                {
                    SetPreview("JSHandle@" + nodePreview);
                }
            }
            catch (PlaywrightSharpException)
            {
                // Best-effort preview, matching upstream ElementHandle._initializePreview.
            }
        }
    }
}
