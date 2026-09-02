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
#pragma warning disable SA1201
#pragma warning disable CA2000
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>Public <see cref="IJSHandle"/> wrapping <see cref="CRJSHandle"/>.</summary>
    /// <remarks>
    /// Not sealed — <see cref="ChromiumElementHandle"/> inherits to gain the same
    /// <c>EvaluateAsync</c>/<c>DisposeAsync</c> behaviour while layering on DOM-node methods.
    /// </remarks>
    internal partial class ChromiumJSHandle : IJSHandle
    {
        private readonly CRJSHandle _crHandle;
        private readonly CRPage _page;

        internal ChromiumJSHandle(CRJSHandle crHandle, CRPage page = null)
        {
            _crHandle = crHandle ?? throw new ArgumentNullException(nameof(crHandle));
            _page = page ?? (crHandle as CRElementHandle)?.Page;
        }

        /// <inheritdoc/>
        public virtual IElementHandle AsElement() => null;

        /// <summary>Gets the CDP remote object id for this handle.</summary>
        internal string ObjectId => _crHandle.ObjectId;

        /// <summary>Gets the execution context that owns this handle.</summary>
        internal CRExecutionContext ExecutionContext => _crHandle.ExecutionContext;

        /// <summary>Gets the owning Chromium page, when known.</summary>
        internal CRPage CrPage => _page;

        /// <summary>Gets the wrapped CR handle.</summary>
        protected CRJSHandle CrHandle => _crHandle;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
            => await _crHandle.DisposeAsync().ConfigureAwait(false);

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = null)
            => EvaluateAsync<JsonElement?>(expression, arg);

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
        {
            string functionDeclaration = EvaluateWithArg.AsFunction(expression);
            if (arg != null)
            {
                return _crHandle.EvaluateFunctionAsync<T>(functionDeclaration, arg);
            }

            return _crHandle.EvaluateFunctionAsync<T>(functionDeclaration);
        }

        /// <inheritdoc/>
        public async Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
        {
            JsonElement? remote = arg != null
                ? await _crHandle.EvaluateHandleRawAsync(expression, arg).ConfigureAwait(false)
                : await _crHandle.EvaluateHandleRawAsync(expression).ConfigureAwait(false);
            return WrapRemote(remote);
        }

        /// <inheritdoc/>
        public async Task<Dictionary<string, IJSHandle>> GetPropertiesAsync()
        {
            Dictionary<string, IJSHandle> result = new Dictionary<string, IJSHandle>(StringComparer.Ordinal);

            // page.evaluateHandle primitives are CRJSHandle with a null objectId.
            if (string.IsNullOrEmpty(ObjectId))
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
        public async Task<IJSHandle> GetPropertyAsync(string propertyName)
        {
            try
            {
                JsonElement? remote = await _crHandle.ExecutionContext
                    .GetPropertyOnHandleAsync(_crHandle.ObjectId, propertyName)
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
            JsonElement serialized = await _crHandle
                .EvaluateFunctionAsync<JsonElement>(JsonValueHelper.SerializeFunction)
                .ConfigureAwait(false);
            return JsonValueHelper.Parse<T>(serialized);
        }

        /// <summary>Returns the official protocol preview.</summary>
        /// <returns>The preview string.</returns>
        public override string ToString() => _crHandle.Preview;

        /// <summary>
        /// Unboxes a primitive handle (no <c>objectId</c>) as a CDP call argument.
        /// </summary>
        /// <returns>A protocol argument payload.</returns>
        internal object ToCallArgument()
        {
            if (_crHandle.ToImmediateHandle() is ImmediateJSHandle immediate)
            {
                return immediate.ToCallArgument();
            }

            return new { value = (object)null };
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

            // Ownership of the inner CRJSHandle transfers to the instance (DisposeAsync).
#pragma warning disable CA2000
            if (RemoteObject.IsNode(remote) && _page != null)
            {
                return new ChromiumElementHandle(new CRElementHandle(_page, _crHandle.ExecutionContext, objectId, "JSHandle@node"));
            }

            return new ChromiumJSHandle(new CRJSHandle(_crHandle.ExecutionContext, objectId, preview), _page);
#pragma warning restore CA2000
        }
    }
}
