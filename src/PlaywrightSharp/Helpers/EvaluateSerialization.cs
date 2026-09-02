/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official evaluate structured-clone: <c>serializeAsCallArgument</c> /
    /// <c>parseEvaluationResultValue</c> plus CDP error rewrites.
    /// </summary>
    internal static class EvaluateSerialization
    {
        /// <summary>
        /// Official client error when a disposed handle is used as an evaluate argument.
        /// </summary>
        internal const string DisposedHandleMessage = "JSHandle is disposed. no object with guid";

        /// <summary>
        /// Official CDP deep-chain serialize error.
        /// </summary>
        internal const string TooLongChainMessage =
            "Cannot serialize result: object reference chain is too long.";

        /// <summary>
        /// Official navigation-during-evaluate error fragment.
        /// </summary>
        internal const string NavigationMessage =
            "Execution context was destroyed, most likely because of a navigation.";

        /// <summary>
        /// Browser-side serialize matching Playwright's utility-script serializer.
        /// </summary>
        internal const string SerializeJs =
            "function (value) {" +
            "  const seen = new Map();" +
            "  let nextId = 1;" +
            "  const isRegExp = (obj) => { try { return obj instanceof RegExp || Object.prototype.toString.call(obj) === '[object RegExp]'; } catch (e) { return false; } };" +
            "  const isDate = (obj) => { try { return obj instanceof Date || Object.prototype.toString.call(obj) === '[object Date]'; } catch (e) { return false; } };" +
            "  const isURL = (obj) => { try { return typeof URL === 'function' && (obj instanceof URL || Object.prototype.toString.call(obj) === '[object URL]'); } catch (e) { return false; } };" +
            "  const isError = (obj) => { try { return obj instanceof Error || (obj && Object.getPrototypeOf(obj) && Object.getPrototypeOf(obj).name === 'Error'); } catch (e) { return false; } };" +
            "  const typed = [" +
            "    ['i8', typeof Int8Array === 'function' ? Int8Array : null]," +
            "    ['ui8', typeof Uint8Array === 'function' ? Uint8Array : null]," +
            "    ['ui8c', typeof Uint8ClampedArray === 'function' ? Uint8ClampedArray : null]," +
            "    ['i16', typeof Int16Array === 'function' ? Int16Array : null]," +
            "    ['ui16', typeof Uint16Array === 'function' ? Uint16Array : null]," +
            "    ['i32', typeof Int32Array === 'function' ? Int32Array : null]," +
            "    ['ui32', typeof Uint32Array === 'function' ? Uint32Array : null]," +
            "    ['f32', typeof Float32Array === 'function' ? Float32Array : null]," +
            "    ['f64', typeof Float64Array === 'function' ? Float64Array : null]," +
            "    ['bi64', typeof BigInt64Array === 'function' ? BigInt64Array : null]," +
            "    ['bui64', typeof BigUint64Array === 'function' ? BigUint64Array : null]" +
            "  ];" +
            "  const toBase64 = (array) => {" +
            "    if (array && typeof array.toBase64 === 'function') return array.toBase64();" +
            "    const bytes = new Uint8Array(array.buffer, array.byteOffset, array.byteLength);" +
            "    let binary = '';" +
            "    for (let i = 0; i < bytes.length; i++) binary += String.fromCharCode(bytes[i]);" +
            "    return btoa(binary);" +
            "  };" +
            "  const visit = (v) => {" +
            "    if (v && typeof v === 'object') {" +
            "      try {" +
            "        if (typeof globalThis.Window === 'function' && v instanceof globalThis.Window) return { s: 'ref: <Window>' };" +
            "        if (typeof globalThis.Document === 'function' && v instanceof globalThis.Document) return { s: 'ref: <Document>' };" +
            "        if (typeof globalThis.Node === 'function' && v instanceof globalThis.Node) return { s: 'ref: <Node>' };" +
            "      } catch (e) {}" +
            "    }" +
            "    if (Object.is(v, undefined)) return { v: 'undefined' };" +
            "    if (Object.is(v, null)) return { v: 'null' };" +
            "    if (Object.is(v, NaN)) return { v: 'NaN' };" +
            "    if (Object.is(v, Infinity)) return { v: 'Infinity' };" +
            "    if (Object.is(v, -Infinity)) return { v: '-Infinity' };" +
            "    if (Object.is(v, -0)) return { v: '-0' };" +
            "    const type = typeof v;" +
            "    if (type === 'boolean') return { b: v };" +
            "    if (type === 'number') return { n: v };" +
            "    if (type === 'string') return { s: v };" +
            "    if (type === 'bigint') return { bi: String(v) };" +
            "    if (type === 'symbol') return { v: 'undefined' };" +
            "    if (type === 'function') return { v: 'undefined' };" +
            "    if (isError(v)) {" +
            "      let stack = v.stack || '';" +
            "      if (stack && stack.indexOf(v.name + ': ' + v.message) !== 0) stack = v.name + ': ' + v.message + '\\n' + stack;" +
            "      return { e: { n: String(v.name || 'Error'), m: String(v.message || ''), s: String(stack) } };" +
            "    }" +
            "    if (isDate(v)) { try { return { d: v.toJSON() }; } catch (e) { return { d: String(v) }; } }" +
            "    if (isURL(v)) { try { return { u: String(v) }; } catch (e) { return { u: '' }; } }" +
            "    if (isRegExp(v)) return { r: { p: String(v.source), f: String(v.flags || '') } };" +
            "    for (let t = 0; t < typed.length; t++) {" +
            "      const ctor = typed[t][1];" +
            "      if (!ctor) continue;" +
            "      try { if (v instanceof ctor) return { ta: { b: toBase64(v), k: typed[t][0] } }; } catch (e) {}" +
            "    }" +
            "    if (seen.has(v)) return { ref: seen.get(v) };" +
            "    const id = nextId++;" +
            "    seen.set(v, id);" +
            "    if (Array.isArray(v)) {" +
            "      const a = [];" +
            "      for (let i = 0; i < v.length; i++) a[i] = visit(v[i]);" +
            "      return { a: a, id: id };" +
            "    }" +
            "    const o = [];" +
            "    let keys = [];" +
            "    try { keys = Object.keys(v); } catch (e) { keys = []; }" +
            "    for (let i = 0; i < keys.length; i++) {" +
            "      const k = keys[i];" +
            "      if (k === '__proto__') continue;" +
            "      let item;" +
            "      try { item = v[k]; } catch (e) { continue; }" +
            "      if (k === 'toJSON' && typeof item === 'function') o[o.length] = { k: k, v: { o: [], id: 0 } };" +
            "      else o[o.length] = { k: k, v: visit(item) };" +
            "    }" +
            "    if (o.length === 0) {" +
            "      try {" +
            "        if (v.toJSON && typeof v.toJSON === 'function') return visit(v.toJSON());" +
            "      } catch (e) {}" +
            "    }" +
            "    return { o: o, id: id };" +
            "  };" +
            "  return visit(value);" +
            "}";

        /// <summary>
        /// Browser-side parse of the tagged evaluate payload.
        /// </summary>
        internal const string ParseJs =
            "function (value) {" +
            "  const refs = new Map();" +
            "  const typed = {" +
            "    i8: typeof Int8Array === 'function' ? Int8Array : null," +
            "    ui8: typeof Uint8Array === 'function' ? Uint8Array : null," +
            "    ui8c: typeof Uint8ClampedArray === 'function' ? Uint8ClampedArray : null," +
            "    i16: typeof Int16Array === 'function' ? Int16Array : null," +
            "    ui16: typeof Uint16Array === 'function' ? Uint16Array : null," +
            "    i32: typeof Int32Array === 'function' ? Int32Array : null," +
            "    ui32: typeof Uint32Array === 'function' ? Uint32Array : null," +
            "    f32: typeof Float32Array === 'function' ? Float32Array : null," +
            "    f64: typeof Float64Array === 'function' ? Float64Array : null," +
            "    bi64: typeof BigInt64Array === 'function' ? BigInt64Array : null," +
            "    bui64: typeof BigUint64Array === 'function' ? BigUint64Array : null" +
            "  };" +
            "  const fromBase64 = (b64, Ctor) => {" +
            "    const binary = atob(b64);" +
            "    const bytes = new Uint8Array(binary.length);" +
            "    for (let i = 0; i < binary.length; i++) bytes[i] = binary.charCodeAt(i);" +
            "    return Ctor ? new Ctor(bytes.buffer) : bytes;" +
            "  };" +
            "  const visit = (v) => {" +
            "    if (v === undefined || v === null || typeof v !== 'object') return v;" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'ref')) return refs.get(v.ref);" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'v')) {" +
            "      if (v.v === 'undefined') return undefined;" +
            "      if (v.v === 'null') return null;" +
            "      if (v.v === 'NaN') return NaN;" +
            "      if (v.v === 'Infinity') return Infinity;" +
            "      if (v.v === '-Infinity') return -Infinity;" +
            "      if (v.v === '-0') return -0;" +
            "      return undefined;" +
            "    }" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'b')) return v.b;" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'n')) return v.n;" +
            "    if (Object.prototype.hasOwnProperty.call(v, 's')) return v.s;" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'bi')) return BigInt(v.bi);" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'd')) return new Date(v.d);" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'u')) return new URL(v.u);" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'r')) return new RegExp(v.r.p, v.r.f);" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'e')) {" +
            "      const err = new Error(v.e.m);" +
            "      err.name = v.e.n;" +
            "      err.stack = v.e.s;" +
            "      return err;" +
            "    }" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'ta')) return fromBase64(v.ta.b, typed[v.ta.k]);" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'a')) {" +
            "      const a = [];" +
            "      refs.set(v.id, a);" +
            "      for (let i = 0; i < v.a.length; i++) a[i] = visit(v.a[i]);" +
            "      return a;" +
            "    }" +
            "    if (Object.prototype.hasOwnProperty.call(v, 'o')) {" +
            "      const o = {};" +
            "      refs.set(v.id, o);" +
            "      for (let i = 0; i < v.o.length; i++) {" +
            "        const e = v.o[i];" +
            "        if (e.k === '__proto__') continue;" +
            "        o[e.k] = visit(e.v);" +
            "      }" +
            "      return o;" +
            "    }" +
            "    return v;" +
            "  };" +
            "  return visit(value);" +
            "}";

        /// <summary>
        /// Sentinel for JS <c>undefined</c> in evaluate arguments.
        /// </summary>
        internal static readonly object Undefined = new UndefinedSentinel();

        /// <summary>
        /// Awaits a thenable then structured-clone serializes it.
        /// </summary>
        internal static string SerializeAwaitedJs
            => "async function(value) { return (" + SerializeJs + ")(await value); }";

        /// <summary>
        /// Wraps an expression so its completion value is structured-clone serialized
        /// after promises settle, without using <c>async</c>/<c>await</c> (overwritten
        /// <c>Promise</c> must still be thenable for CDP <c>awaitPromise</c>).
        /// </summary>
        /// <param name="expression">A JavaScript expression.</param>
        /// <returns>An evaluable expression that returns the tagged payload.</returns>
        internal static string WithSerializedResult(string expression)
        {
            return "(function(){ const s = (" + SerializeJs + "); const v = (" + expression +
                "); if (v && typeof v.then === 'function') return v.then(s); return s(v); })()";
        }

        /// <summary>
        /// Returns whether <paramref name="expression"/> can be parenthesized as a
        /// JavaScript expression (function IIFEs and wrapped calls). Programs such as
        /// <c>1 + 5;</c> must stay two-step so the completion value is preserved.
        /// </summary>
        /// <param name="expression">The already-invoked evaluate expression.</param>
        /// <returns><see langword="true"/> when same-turn serialize wrapping is safe.</returns>
        internal static bool CanWrapExpression(string expression)
        {
            if (string.IsNullOrEmpty(expression))
            {
                return false;
            }

            string trimmed = expression.TrimStart();
            if (trimmed.Contains(".then(", StringComparison.Ordinal)
                || trimmed.Contains("await ", StringComparison.Ordinal)
                || trimmed.Contains("new Promise", StringComparison.Ordinal)
                || trimmed.Contains("Promise.", StringComparison.Ordinal)
                || trimmed.StartsWith("(async", StringComparison.Ordinal)
                || trimmed.StartsWith("async ", StringComparison.Ordinal)
                || trimmed.StartsWith("async(", StringComparison.Ordinal))
            {
                // Thenables must keep a handle so WebKit can awaitPromise via
                // callFunctionOn. Wrapping them with returnByValue:true drops
                // the objectId and a second evaluate re-runs fetch/side effects.
                return false;
            }

            return trimmed.StartsWith('(') || trimmed.StartsWith("function", StringComparison.Ordinal);
        }

        /// <summary>
        /// Serializes a C# evaluate argument to the official tagged payload.
        /// </summary>
        /// <param name="value">The argument.</param>
        /// <returns>The tagged JSON element.</returns>
        internal static JsonElement SerializeCallArgument(object value)
        {
            object tagged = VisitArgument(value, new Dictionary<object, int>(IdentityComparer.Instance), new IdBox(), string.Empty);
            return JsonSerializer.SerializeToElement(tagged);
        }

        /// <summary>
        /// Reads a CDP/WIP remote object produced by <see cref="WithSerializedResult"/>.
        /// </summary>
        /// <typeparam name="T">Caller-requested type.</typeparam>
        /// <param name="remote">The protocol remote object.</param>
        /// <returns>The reconstructed value.</returns>
        internal static T ParseRemote<T>(JsonElement? remote)
        {
            if (remote == null)
            {
                return default;
            }

            JsonElement el = remote.Value;
            if (JsonValueHelper.TryReadUnserializableToken(el, out string token)
                && JsonValueHelper.TryGetUnserializableToken(token, out double number))
            {
                return JsonValueHelper.CoerceNumber<T>(number);
            }

            if (el.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "undefined", StringComparison.Ordinal))
            {
                return default;
            }

            if (!el.TryGetProperty("value", out JsonElement value))
            {
                return default;
            }

            return JsonValueHelper.Parse<T>(value);
        }

        /// <summary>
        /// Rewrites official evaluate transport errors (deep chain, navigation).
        /// </summary>
        /// <param name="message">The protocol or engine message.</param>
        /// <param name="frameEvaluate">Whether the call is <c>frame.evaluate</c>.</param>
        /// <returns>The rewritten message.</returns>
        internal static string RewriteError(string message, bool frameEvaluate = false)
        {
            if (string.IsNullOrEmpty(message))
            {
                return message;
            }

            if (message.Contains("Object reference chain is too long", StringComparison.Ordinal)
                || message.Contains("Object has too long reference chain", StringComparison.Ordinal)
                || message.Contains("CBOR: stack limit exceeded", StringComparison.Ordinal))
            {
                return TooLongChainMessage;
            }

            if (message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                || message.Contains("Frame was detached", StringComparison.Ordinal)
                || message.Contains("Missing injected script", StringComparison.Ordinal))
            {
                if (!frameEvaluate)
                {
                    return NavigationMessage;
                }

                string detail = message.Contains("Frame was detached", StringComparison.Ordinal)
                    ? "Frame was detached"
                    : "Execution context was destroyed";
                return "frame.evaluate: " + detail;
            }

            if (message.Contains("Cannot find context", StringComparison.Ordinal)
                || message.Contains("Inspected target navigated or closed", StringComparison.Ordinal)
                || message.Contains("context destroyed", StringComparison.OrdinalIgnoreCase))
            {
                return NavigationMessage;
            }

            return message;
        }

        /// <summary>
        /// Rewrites a caught evaluate exception to the official message.
        /// </summary>
        /// <param name="error">The protocol or engine exception.</param>
        /// <param name="frameEvaluate">Whether the call is <c>frame.evaluate</c>.</param>
        /// <returns>The original or rewritten exception.</returns>
        internal static PlaywrightSharpException RewriteException(PlaywrightSharpException error, bool frameEvaluate = false)
        {
            if (error == null)
            {
                return error;
            }

            string rewritten = RewriteError(error.Message, frameEvaluate);
            return rewritten == error.Message ? error : new PlaywrightSharpException(rewritten);
        }

        /// <summary>
        /// Structured-clone materializes a remote evaluate result.
        /// </summary>
        /// <typeparam name="T">Caller-requested type.</typeparam>
        /// <param name="remote">A <c>returnByValue: false</c> remote object.</param>
        /// <param name="serializeOnHandle">Runs <see cref="SerializeJs"/> on an object id.</param>
        /// <param name="release">Releases the remote object.</param>
        /// <returns>The reconstructed value.</returns>
        internal static async Task<T> MaterializeAsync<T>(
            JsonElement? remote,
            Func<string, Task<JsonElement>> serializeOnHandle,
            Func<string, Task> release)
        {
            if (remote == null)
            {
                return default;
            }

            string objectId = RemoteObject.GetObjectId(remote);
            if (string.IsNullOrEmpty(objectId))
            {
                return ParseRemote<T>(remote);
            }

            try
            {
                JsonElement tagged = await serializeOnHandle(objectId).ConfigureAwait(false);
                return JsonValueHelper.Parse<T>(tagged);
            }
            finally
            {
                if (release != null)
                {
                    await release(objectId).ConfigureAwait(false);
                }
            }
        }

        private static object VisitArgument(object value, IDictionary<object, int> seen, IdBox ids, string path)
        {
            if (ReferenceEquals(value, Undefined))
            {
                return new Dictionary<string, string> { ["v"] = "undefined" };
            }

            if (value == null)
            {
                return new Dictionary<string, string> { ["v"] = "null" };
            }

            if (value is JsonElement json)
            {
                if (json.ValueKind == JsonValueKind.Null || json.ValueKind == JsonValueKind.Undefined)
                {
                    return new Dictionary<string, string>
                    {
                        ["v"] = json.ValueKind == JsonValueKind.Undefined ? "undefined" : "null",
                    };
                }

                value = JsonSerializer.Deserialize<object>(json.GetRawText()) ?? json;
            }

            if (value is bool boolean)
            {
                return new Dictionary<string, object> { ["b"] = boolean };
            }

            if (value is string text)
            {
                return new Dictionary<string, object> { ["s"] = text };
            }

            if (value is BigInteger big)
            {
                return new Dictionary<string, object> { ["bi"] = big.ToString(CultureInfo.InvariantCulture) };
            }

            if (value is DateTime dateTime)
            {
                return new Dictionary<string, object> { ["d"] = dateTime.ToUniversalTime().ToString("o", CultureInfo.InvariantCulture) };
            }

            if (value is DateTimeOffset dateTimeOffset)
            {
                return new Dictionary<string, object> { ["d"] = dateTimeOffset.UtcDateTime.ToString("o", CultureInfo.InvariantCulture) };
            }

            if (value is Uri uri)
            {
                return new Dictionary<string, object> { ["u"] = uri.ToString() };
            }

            if (value is Regex regex)
            {
                return new Dictionary<string, object>
                {
                    ["r"] = new Dictionary<string, object>
                    {
                        ["p"] = regex.ToString(),
                        ["f"] = RegexFlags(regex),
                    },
                };
            }

            if (value is JavaScriptEvalError jsError)
            {
                return new Dictionary<string, object>
                {
                    ["e"] = new Dictionary<string, object>
                    {
                        ["n"] = jsError.Name ?? "Error",
                        ["m"] = jsError.Message ?? string.Empty,
                        ["s"] = jsError.Stack ?? string.Empty,
                    },
                };
            }

            if (value is Exception exception)
            {
                return new Dictionary<string, object>
                {
                    ["e"] = new Dictionary<string, object>
                    {
                        ["n"] = exception.GetType().Name,
                        ["m"] = exception.Message ?? string.Empty,
                        ["s"] = exception.ToString(),
                    },
                };
            }

            if (value is double d)
            {
                return NumberTag(d);
            }

            if (value is float f)
            {
                return NumberTag(f);
            }

            if (value is decimal dec)
            {
                return new Dictionary<string, object> { ["n"] = dec };
            }

            if (value is byte or sbyte or short or ushort or int or uint or long or ulong)
            {
                return new Dictionary<string, object> { ["n"] = Convert.ToDouble(value, CultureInfo.InvariantCulture) };
            }

            if (value is IJSHandle)
            {
                throw new PlaywrightSharpException("JSHandle arguments must be passed through the handle evaluate path.");
            }

            Type type = value.GetType();
            if (type.IsPrimitive)
            {
                return new Dictionary<string, object> { ["n"] = Convert.ToDouble(value, CultureInfo.InvariantCulture) };
            }

            if (seen.TryGetValue(value, out int existing))
            {
                return new Dictionary<string, object> { ["ref"] = existing };
            }

            if (IsNonStringKeyDictionary(value))
            {
                int emptyId = ++ids.Value;
                seen[value] = emptyId;
                return new Dictionary<string, object>
                {
                    ["o"] = Array.Empty<object>(),
                    ["id"] = emptyId,
                };
            }

            if (value is IDictionary dictionary)
            {
                int id = ++ids.Value;
                seen[value] = id;
                List<object> entries = new List<object>();
                foreach (DictionaryEntry entry in dictionary)
                {
                    string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                    if (string.Equals(key, "__proto__", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    entries.Add(new Dictionary<string, object>
                    {
                        ["k"] = key,
                        ["v"] = VisitArgument(entry.Value, seen, ids, Combine(path, key)),
                    });
                }

                return new Dictionary<string, object> { ["o"] = entries, ["id"] = id };
            }

            if (TryTypedArray(value, out object typed))
            {
                return typed;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                int id = ++ids.Value;
                seen[value] = id;
                List<object> items = new List<object>();
                int index = 0;
                foreach (object item in enumerable)
                {
                    items.Add(VisitArgument(item, seen, ids, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]"));
                    index++;
                }

                return new Dictionary<string, object> { ["a"] = items, ["id"] = id };
            }

            int objectId = ++ids.Value;
            seen[value] = objectId;
            List<object> props = new List<object>();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (string.Equals(property.Name, "__proto__", StringComparison.Ordinal))
                {
                    continue;
                }

                object propertyValue = property.GetValue(value);
                props.Add(new Dictionary<string, object>
                {
                    ["k"] = property.Name,
                    ["v"] = VisitArgument(propertyValue, seen, ids, Combine(path, property.Name)),
                });
            }

            return new Dictionary<string, object> { ["o"] = props, ["id"] = objectId };
        }

        private static object NumberTag(double number)
        {
            if (double.IsNaN(number))
            {
                return new Dictionary<string, string> { ["v"] = "NaN" };
            }

            if (double.IsPositiveInfinity(number))
            {
                return new Dictionary<string, string> { ["v"] = "Infinity" };
            }

            if (double.IsNegativeInfinity(number))
            {
                return new Dictionary<string, string> { ["v"] = "-Infinity" };
            }

            if (number == 0d && BitConverter.DoubleToInt64Bits(number) < 0)
            {
                return new Dictionary<string, string> { ["v"] = "-0" };
            }

            return new Dictionary<string, object> { ["n"] = number };
        }

        private static bool TryTypedArray(object value, out object tagged)
        {
            tagged = null;
            if (value == null)
            {
                return false;
            }

            // CLR treats same-width signed/unsigned arrays as interchangeable
            // (int[] is uint[]). Exact types keep official number arrays as
            // JS Array (Array.isArray) and typed arrays as TypedArray.
            Type type = value.GetType();
            byte[] bytes;
            string kind;
            if (type == typeof(sbyte[]))
            {
                sbyte[] i8 = (sbyte[])value;
                bytes = new byte[i8.Length];
                Buffer.BlockCopy(i8, 0, bytes, 0, bytes.Length);
                kind = "i8";
            }
            else if (type == typeof(byte[]))
            {
                bytes = (byte[])value;
                kind = "ui8";
            }
            else if (type == typeof(short[]))
            {
                short[] i16 = (short[])value;
                bytes = new byte[i16.Length * 2];
                Buffer.BlockCopy(i16, 0, bytes, 0, bytes.Length);
                kind = "i16";
            }
            else if (type == typeof(ushort[]))
            {
                ushort[] ui16 = (ushort[])value;
                bytes = new byte[ui16.Length * 2];
                Buffer.BlockCopy(ui16, 0, bytes, 0, bytes.Length);
                kind = "ui16";
            }
            else if (type == typeof(uint[]))
            {
                uint[] ui32 = (uint[])value;
                bytes = new byte[ui32.Length * 4];
                Buffer.BlockCopy(ui32, 0, bytes, 0, bytes.Length);
                kind = "ui32";
            }
            else if (type == typeof(float[]))
            {
                float[] f32 = (float[])value;
                bytes = new byte[f32.Length * 4];
                Buffer.BlockCopy(f32, 0, bytes, 0, bytes.Length);
                kind = "f32";
            }
            else if (type == typeof(long[]))
            {
                long[] i64 = (long[])value;
                bytes = new byte[i64.Length * 8];
                Buffer.BlockCopy(i64, 0, bytes, 0, bytes.Length);
                kind = "bi64";
            }
            else if (type == typeof(ulong[]))
            {
                ulong[] u64 = (ulong[])value;
                bytes = new byte[u64.Length * 8];
                Buffer.BlockCopy(u64, 0, bytes, 0, bytes.Length);
                kind = "bui64";
            }
            else
            {
                // int[] / double[] stay regular JS arrays (official [1,2,3]).
                return false;
            }

            tagged = new Dictionary<string, object>
            {
                ["ta"] = new Dictionary<string, object>
                {
                    ["b"] = Convert.ToBase64String(bytes),
                    ["k"] = kind,
                },
            };
            return true;
        }

        private static bool IsNonStringKeyDictionary(object value)
        {
            Type type = value.GetType();
            if (!type.IsGenericType)
            {
                return false;
            }

            Type definition = type.GetGenericTypeDefinition();
            if (definition != typeof(Dictionary<,>) && definition != typeof(SortedDictionary<,>))
            {
                return false;
            }

            return type.GetGenericArguments()[0] != typeof(string);
        }

        private static string RegexFlags(Regex regex)
        {
            string flags = string.Empty;
            if ((regex.Options & RegexOptions.IgnoreCase) != 0)
            {
                flags += "i";
            }

            if ((regex.Options & RegexOptions.Multiline) != 0)
            {
                flags += "m";
            }

            return flags;
        }

        private static string Combine(string path, string name)
            => string.IsNullOrEmpty(path) ? name ?? string.Empty : path + "." + name;

        private sealed class IdBox
        {
            internal int Value { get; set; }
        }

        private sealed class UndefinedSentinel
        {
        }

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            internal static readonly IdentityComparer Instance = new IdentityComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
