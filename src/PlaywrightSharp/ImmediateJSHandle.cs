/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp
{
    /// <summary>
    /// In-process <see cref="IJSHandle"/> for protocol values that have no remote object id
    /// (console primitives).
    /// </summary>
    internal sealed partial class ImmediateJSHandle : IJSHandle
    {
        private readonly JsonElement _value;
        private readonly string _preview;

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmediateJSHandle"/> class.
        /// </summary>
        /// <param name="value">Cloned JSON value.</param>
        internal ImmediateJSHandle(JsonElement value)
            : this(value, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="ImmediateJSHandle"/> class.
        /// </summary>
        /// <param name="value">Cloned JSON value.</param>
        /// <param name="preview">Official handle preview.</param>
        internal ImmediateJSHandle(JsonElement value, string preview)
        {
            _value = value;
            _preview = preview ?? value.ValueKind switch
            {
                JsonValueKind.String => value.GetString() ?? string.Empty,
                JsonValueKind.Null => "null",
                JsonValueKind.True => "true",
                JsonValueKind.False => "false",
                JsonValueKind.Number => value.GetRawText(),
                JsonValueKind.Undefined => "undefined",
                _ => value.GetRawText(),
            };
        }

        /// <inheritdoc/>
        public IElementHandle AsElement() => null;

        /// <inheritdoc/>
        public ValueTask DisposeAsync() => default;

        /// <inheritdoc/>
        public Task<JsonElement?> EvaluateAsync(string expression, object arg = null)
            => throw new PlaywrightSharpException("Immediate handles do not support evaluation.");

        /// <inheritdoc/>
        public Task<T> EvaluateAsync<T>(string expression, object arg = default)
            => throw new PlaywrightSharpException("Immediate handles do not support evaluation.");

        /// <inheritdoc/>
        public Task<IJSHandle> EvaluateHandleAsync(string expression, object arg = default)
            => throw new PlaywrightSharpException("Immediate handles do not support evaluation.");

        /// <inheritdoc/>
        public Task<Dictionary<string, IJSHandle>> GetPropertiesAsync()
            => Task.FromResult(new Dictionary<string, IJSHandle>(StringComparer.Ordinal));

        /// <inheritdoc/>
        public Task<IJSHandle> GetPropertyAsync(string propertyName)
            => Task.FromResult<IJSHandle>(null);

        /// <inheritdoc/>
        public Task<T> JsonValueAsync<T>()
        {
            if (typeof(T) == typeof(JsonElement))
            {
                return Task.FromResult((T)(object)_value);
            }

            if (typeof(T) == typeof(JsonElement?))
            {
                JsonElement? boxed = _value;
                return Task.FromResult((T)(object)boxed);
            }

            // Protocol primitives store Infinity / NaN / -0 as unserializableValue strings.
            // WebKit often omits that field and only keeps the preview/description.
            if (JsonValueHelper.TryGetUnserializableNumber(_value, out double unserializable)
                || JsonValueHelper.TryGetUnserializableToken(_preview, out unserializable))
            {
                return Task.FromResult(JsonValueHelper.CoerceNumber<T>(unserializable));
            }

            // GetRawText() throws on Undefined; undefined/null jsonValue is JS nullish.
            if (_value.ValueKind == JsonValueKind.Undefined || _value.ValueKind == JsonValueKind.Null)
            {
                return Task.FromResult(default(T));
            }

            return Task.FromResult(_value.ToObject<T>());
        }

        /// <summary>Returns the official primitive preview.</summary>
        /// <returns>The preview string.</returns>
        public override string ToString() => _preview;

        /// <summary>
        /// Serializes this primitive as a CDP/WIP <c>callFunctionOn</c> argument
        /// so evaluate can unbox Infinity / -0 / numbers / strings.
        /// </summary>
        /// <returns>A protocol argument payload.</returns>
        internal object ToCallArgument()
        {
            if (_value.ValueKind == JsonValueKind.String
                && JsonValueHelper.TryGetUnserializableToken(_value.GetString(), out _))
            {
                return new { unserializableValue = _value.GetString() };
            }

            if (JsonValueHelper.TryGetUnserializableToken(_preview, out _))
            {
                return new { unserializableValue = _preview };
            }

            return _value.ValueKind switch
            {
                JsonValueKind.Undefined => new { value = (object)null },
                JsonValueKind.Null => new { value = (object)null },
                JsonValueKind.True => new { value = true },
                JsonValueKind.False => new { value = false },
                JsonValueKind.String => new { value = _value.GetString() },
                JsonValueKind.Number => new { value = ReadNumber() },
                _ => new { value = JsonSerializer.Deserialize<object>(_value.GetRawText()) },
            };
        }

        /// <summary>
        /// Inlines this primitive into an evaluate JSON tree. Unserializable
        /// numbers become <c>{ __pw_u: token }</c> so WebKit can revive them
        /// without CDP <c>unserializableValue</c>.
        /// </summary>
        /// <returns>A JSON-serializable tree node.</returns>
        internal object ToTreeValue()
        {
            string token = null;
            if (_value.ValueKind == JsonValueKind.String
                && JsonValueHelper.TryGetUnserializableToken(_value.GetString(), out _))
            {
                token = _value.GetString();
            }
            else if (JsonValueHelper.TryGetUnserializableToken(_preview, out _))
            {
                token = _preview;
            }

            if (!string.IsNullOrEmpty(token))
            {
                return new Dictionary<string, string>(StringComparer.Ordinal) { ["__pw_u"] = token };
            }

            return _value.ValueKind switch
            {
                JsonValueKind.Undefined => null,
                JsonValueKind.Null => null,
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String => _value.GetString(),
                JsonValueKind.Number => ReadNumber(),
                _ => JsonSerializer.Deserialize<object>(_value.GetRawText()),
            };
        }

        private object ReadNumber()
        {
            if (_value.TryGetInt32(out int i))
            {
                return i;
            }

            if (_value.TryGetInt64(out long l))
            {
                return l;
            }

            return _value.GetDouble();
        }
    }
}
