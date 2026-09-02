/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
#pragma warning disable SA1201
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Handle to a JavaScript value in Firefox, backed by a Juggler remote object.
    /// </summary>
    internal partial class FFJSHandle : IJSHandle
    {
        private readonly FFExecutionContext _context;
        private readonly string _objectId;
        private readonly string _preview;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFJSHandle"/> class.
        /// </summary>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The Juggler remote object id. May be <see langword="null"/> for a dummy handle.</param>
        /// <param name="preview">Official handle preview from the remote object.</param>
        public FFJSHandle(FFExecutionContext context, string objectId, string preview = null)
        {
            _context = context;
            _objectId = objectId;
            _preview = preview ?? (string.IsNullOrEmpty(objectId) ? "undefined" : "JSHandle@object");
        }

        /// <inheritdoc/>
        public virtual IElementHandle AsElement() => null;

        /// <summary>Gets the Juggler remote object identifier for this handle.</summary>
        internal string ObjectId => _objectId;

        /// <summary>Gets the execution context that owns this handle.</summary>
        internal FFExecutionContext ExecutionContext => _context;

        /// <summary>Gets the execution context that owns this handle.</summary>
        protected FFExecutionContext Context => _context;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (_context != null && !string.IsNullOrEmpty(_objectId))
            {
                await _context.ReleaseHandleAsync(_objectId).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = null)
            => EvaluateAsync<JsonElement?>(expression, arg);

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
        {
            if (_context == null || string.IsNullOrEmpty(_objectId))
            {
                throw new PlaywrightNativeException("Handle is disposed.");
            }

            string functionDeclaration = EvaluateWithArg.AsFunction(expression);
            if (arg != null)
            {
                return _context.EvaluateFunctionOnHandleAsync<T>(_objectId, functionDeclaration, arg);
            }

            return _context.EvaluateFunctionOnHandleAsync<T>(_objectId, functionDeclaration);
        }

        /// <inheritdoc/>
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            if (_context == null || string.IsNullOrEmpty(_objectId))
            {
                throw new PlaywrightNativeException("Handle is disposed.");
            }

            JsonElement? remote = arg != null
                ? await _context.EvaluateFunctionOnHandleAsync(_objectId, expression, arg).ConfigureAwait(false)
                : await _context.EvaluateFunctionOnHandleAsync(_objectId, expression).ConfigureAwait(false);
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
            catch (PlaywrightNativeException)
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
        public Task<IJSHandle> GetPropertyAsync(string propertyName)
            => EvaluateHandleAsync("(object, name) => object[name]", propertyName);

        /// <inheritdoc/>
        public async Task<T> JsonValueAsync<T>()
        {
            JsonElement serialized = await EvaluateAsync<JsonElement>(JsonValueHelper.SerializeFunction)
                .ConfigureAwait(false);
            return JsonValueHelper.Parse<T>(serialized);
        }

        /// <summary>Returns the official protocol preview.</summary>
        /// <returns>The preview string.</returns>
        public override string ToString() => _preview;

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
            if (RemoteObject.IsNode(remote))
            {
                return new FFElementHandle(_context, objectId);
            }

            return new FFJSHandle(_context, objectId, preview);
        }
    }
}
