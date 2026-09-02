/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
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
#pragma warning disable SA1201
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.WebKit
{
    /// <summary>
    /// Handle to a JavaScript value in WebKit — backed by a WIP remote object
    /// (<c>objectId</c>). Implements <see cref="IJSHandle"/> directly;
    /// a specialized subclass (<see cref="WKElementHandle"/>) is used when the remote object
    /// is a DOM node (<c>subtype == "node"</c>). Disposed via <c>Runtime.releaseObject</c>.
    /// </summary>
    internal partial class WKJSHandle : IJSHandle
    {
        private readonly WKExecutionContext _context;
        private readonly string _objectId;
        private readonly WKPage _page;
        private string _preview;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="WKJSHandle"/> class.
        /// </summary>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The WIP remote object id.</param>
        /// <param name="page">
        /// Optional owning page, used when wrapping a nested DOM node as an
        /// <see cref="IElementHandle"/>.
        /// </param>
        /// <param name="preview">Official handle preview from the remote object.</param>
        public WKJSHandle(WKExecutionContext context, string objectId, WKPage page = null, string preview = null)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _objectId = objectId ?? throw new ArgumentNullException(nameof(objectId));
            _page = page;
            _preview = preview ?? "JSHandle@object";
        }

        /// <inheritdoc/>
        public virtual IElementHandle AsElement() => null;

        /// <summary>Gets the WIP remote object identifier for this handle.</summary>
        internal string ObjectId => _objectId;

        /// <summary>Gets the owning WebKit page, when known.</summary>
        internal WKPage OwnerPage => _page;

        /// <summary>Gets the execution context that owns this handle.</summary>
        internal WKExecutionContext ExecutionContext => _context;

        /// <summary>Gets a value indicating whether this handle has been disposed.</summary>
        internal bool IsDisposed => _disposed;

        /// <summary>Gets the official JSHandle preview.</summary>
        internal string Preview => _preview;

        /// <summary>Gets the execution context that owns this handle.</summary>
        protected WKExecutionContext Context => _context;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            await _context.ReleaseHandleAsync(_objectId).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = null)
            => EvaluateAsync<JsonElement?>(expression, arg);

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
        {
            string functionDeclaration = EvaluateWithArg.AsFunction(expression);
            if (arg != null)
            {
                return EvaluateFunctionAsync<T>(functionDeclaration, arg);
            }

            return EvaluateFunctionAsync<T>(functionDeclaration);
        }

        /// <inheritdoc/>
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            EnsureNotDisposed();
            JsonElement? remote = arg != null
                ? await _context.EvaluateHandleOnHandleAsync(_objectId, expression, arg).ConfigureAwait(false)
                : await _context.EvaluateHandleOnHandleAsync(_objectId, expression).ConfigureAwait(false);
            return WrapRemote(remote);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, IJSHandle>> GetPropertiesAsync()
        {
            Dictionary<string, IJSHandle> result = new Dictionary<string, IJSHandle>(StringComparer.Ordinal);
            if (string.IsNullOrEmpty(_objectId))
            {
                return result;
            }

            string[] names;
            try
            {
                names = await EvaluateAsync<string[]>(JsonValueHelper.EnumerablePropertyNamesFunction).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException)
            {
                return result;
            }

            if (names == null)
            {
                return result;
            }

            foreach (string name in names)
            {
                IJSHandle property = await GetPropertyAsync(name).ConfigureAwait(false);
                if (property != null)
                {
                    result[name] = property;
                }
            }

            return result;
        }

        /// <inheritdoc/>
        public async Task<IJSHandle> GetPropertyAsync(string propertyName)
        {
            try
            {
                JsonElement? remote = await _context
                    .GetPropertyOnHandleAsync(_objectId, propertyName)
                    .ConfigureAwait(false);
                return WrapRemote(remote);
            }
            catch (Exception ex) when (ClosedTarget.IsClosed(ex))
            {
                throw ClosedTarget.WrapGetProperty(ex);
            }
        }

        /// <inheritdoc/>
        public async Task<T> JsonValueAsync<T>()
        {
            JsonElement serialized = await EvaluateFunctionAsync<JsonElement>(JsonValueHelper.SerializeFunction)
                .ConfigureAwait(false);
            return JsonValueHelper.Parse<T>(serialized);
        }

        /// <summary>Returns the official protocol preview.</summary>
        /// <returns>The preview string.</returns>
        public override string ToString() => _preview;

        /// <summary>Updates the official preview (used for async node descriptions).</summary>
        /// <param name="preview">The new preview string.</param>
        internal void SetPreview(string preview)
        {
            if (!string.IsNullOrEmpty(preview))
            {
                _preview = preview;
            }
        }

        /// <summary>
        /// Evaluates a JavaScript function with this handle bound as the first argument
        /// (via <c>Runtime.callFunctionOn</c> with this handle's <c>objectId</c>).
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="functionDeclaration">A function declaration; the handle is passed as the first argument.</param>
        /// <param name="args">Additional primitive arguments beyond the handle.</param>
        /// <returns>The deserialized result of the function call.</returns>
        internal Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            EnsureNotDisposed();
            return _context.EvaluateFunctionOnHandleAsync<T>(_objectId, functionDeclaration, args);
        }

        /// <summary>Guards against use after <see cref="DisposeAsync"/>.</summary>
        protected void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new PlaywrightSharpException(EvaluateSerialization.DisposedHandleMessage);
            }
        }

        private IJSHandle WrapRemote(JsonElement? remote)
        {
            if (remote != null
                && JsonValueHelper.TryReadUnserializableToken(remote.Value, out string token))
            {
                return new ImmediateJSHandle(JsonSerializer.SerializeToElement(token), token);
            }

            string objectId = RemoteObject.GetObjectId(remote);
            if (objectId == null)
            {
                return remote == null ? null : RemoteObject.WrapPrimitive(remote.Value);
            }

            string preview = RemoteObject.HandlePreview(remote);
            if (RemoteObject.IsNode(remote) && _page != null)
            {
                return new WKElementHandle(_context, objectId, _page, "JSHandle@node");
            }

            return new WKJSHandle(_context, objectId, _page, preview);
        }
    }
}
