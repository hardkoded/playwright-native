/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Structured-clone serialization for <see cref="IJSHandle.JsonValueAsync{T}"/>.
    /// Mirrors Playwright's <c>serializeAsCallArgument</c> / <c>parseEvaluationResultValue</c>
    /// so dates stay dates and object graphs may contain cycles.
    /// </summary>
    internal static class JsonValueHelper
    {
        /// <summary>
        /// Browser-side function that serializes a handle with object ids and back-references.
        /// Passed to <c>Runtime.callFunctionOn</c> as <c>functionDeclaration</c>.
        /// </summary>
        internal const string SerializeFunction = EvaluateSerialization.SerializeJs;

        /// <summary>
        /// Returns own and inherited (prototype) property names, or an empty list for
        /// primitives / <c>null</c>. Stops at <c>Object.prototype</c> so <c>getProperties</c>
        /// includes constructor-assigned parent fields without walking built-ins.
        /// </summary>
        internal const string EnumerablePropertyNamesFunction =
            "o => { if (o === null || o === undefined || (typeof o !== 'object' && typeof o !== 'function')) return []; const names = []; const seen = new Set(); for (let obj = o; obj && obj !== Object.prototype; obj = Object.getPrototypeOf(obj)) { const own = Object.getOwnPropertyNames(obj); for (let i = 0; i < own.length; i++) { const name = own[i]; if (!seen.has(name)) { seen.add(name); names.push(name); } } } return names; }";

        private static readonly JsonSerializerOptions PreserveOptions = new JsonSerializerOptions
        {
            ReferenceHandler = ReferenceHandler.Preserve,
        };

        /// <summary>
        /// Reconstructs a .NET value from the tagged payload produced by
        /// <see cref="SerializeFunction"/>.
        /// </summary>
        /// <typeparam name="T">The caller-requested type.</typeparam>
        /// <param name="serialized">The browser-side tagged value.</param>
        /// <returns>The reconstructed value.</returns>
        internal static T Parse<T>(JsonElement serialized)
        {
            if (serialized.ValueKind == JsonValueKind.Undefined ||
                serialized.ValueKind == JsonValueKind.Null)
            {
                return default;
            }

            object parsed = ParseToClr(serialized, new Dictionary<int, object>());

            if (typeof(T) == typeof(JsonElement) || typeof(T) == typeof(JsonElement?))
            {
                if (parsed == null)
                {
                    return typeof(T) == typeof(JsonElement?)
                        ? default
                        : (T)(object)default(JsonElement);
                }

                JsonElement element = SerializeClrToJsonElement(parsed);
                return (T)(object)element;
            }

            if (parsed is T typed)
            {
                return typed;
            }

            if (parsed == null)
            {
                return default;
            }

            return (T)ToExpectedType(parsed, typeof(T), new Dictionary<object, object>());
        }

        /// <summary>
        /// Parses a protocol <c>unserializableValue</c> token stored as a JSON string
        /// (<c>Infinity</c>, <c>-Infinity</c>, <c>NaN</c>, <c>-0</c>).
        /// </summary>
        /// <param name="value">The stored protocol value.</param>
        /// <param name="number">The reconstructed IEEE number when recognized.</param>
        /// <returns><see langword="true"/> when <paramref name="value"/> is a special number token.</returns>
        internal static bool TryGetUnserializableNumber(JsonElement value, out double number)
            => TryGetUnserializableToken(value.ValueKind == JsonValueKind.String ? value.GetString() : null, out number);

        /// <summary>
        /// Parses a special-number token (<c>Infinity</c>, <c>-Infinity</c>, <c>NaN</c>, <c>-0</c>).
        /// </summary>
        /// <param name="token">A protocol token or handle preview.</param>
        /// <param name="number">The reconstructed IEEE number when recognized.</param>
        /// <returns><see langword="true"/> when <paramref name="token"/> is a special number.</returns>
        internal static bool TryGetUnserializableToken(string token, out double number)
        {
            switch (token)
            {
                case "Infinity":
                    number = double.PositiveInfinity;
                    return true;
                case "-Infinity":
                    number = double.NegativeInfinity;
                    return true;
                case "NaN":
                    number = double.NaN;
                    return true;
                case "-0":
                    number = -0.0;
                    return true;
                default:
                    number = default;
                    return false;
            }
        }

        /// <summary>
        /// Reads <c>unserializableValue</c> or <c>description</c> when WebKit/CDP omit a
        /// JSON <c>value</c> for Infinity / NaN / -0.
        /// </summary>
        /// <param name="remote">A protocol remote object.</param>
        /// <param name="token">The special-number token when present.</param>
        /// <returns><see langword="true"/> when a special number token was found.</returns>
        internal static bool TryReadUnserializableToken(JsonElement remote, out string token)
        {
            if (remote.TryGetProperty("unserializableValue", out JsonElement uns)
                && uns.ValueKind == JsonValueKind.String
                && IsUnserializableToken(uns.GetString()))
            {
                token = uns.GetString();
                return true;
            }

            if (remote.TryGetProperty("description", out JsonElement description)
                && description.ValueKind == JsonValueKind.String
                && IsUnserializableToken(description.GetString()))
            {
                token = description.GetString();
                return true;
            }

            token = null;
            return false;
        }

        /// <summary>
        /// Converts a special IEEE number to the caller-requested <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The caller-requested type.</typeparam>
        /// <param name="number">The reconstructed number.</param>
        /// <returns>The coerced value.</returns>
        internal static T CoerceNumber<T>(double number)
        {
            if (number is T typed)
            {
                return typed;
            }

            if (typeof(T) == typeof(object))
            {
                return (T)(object)number;
            }

            Type target = Nullable.GetUnderlyingType(typeof(T)) ?? typeof(T);
            if (target == typeof(float))
            {
                return (T)(object)(float)number;
            }

            if (target == typeof(double))
            {
                return (T)(object)number;
            }

            return (T)Convert.ChangeType(number, target, CultureInfo.InvariantCulture);
        }

        private static object ParseTypedArray(string kind, byte[] bytes)
        {
            bytes ??= Array.Empty<byte>();
            return kind switch
            {
                "i8" => ToInt8(bytes),
                "ui8" or "ui8c" => bytes,
                "i16" => ToInt16(bytes),
                "ui16" => ToUInt16(bytes),
                "i32" => ToInt32(bytes),
                "ui32" => ToUInt32(bytes),
                "f32" => ToFloat(bytes),
                "f64" => ToDouble(bytes),
                "bi64" => ToInt64(bytes),
                "bui64" => ToUInt64(bytes),
                _ => bytes,
            };
        }

        private static sbyte[] ToInt8(byte[] bytes)
        {
            sbyte[] result = new sbyte[bytes.Length];
            for (int i = 0; i < bytes.Length; i++)
            {
                result[i] = unchecked((sbyte)bytes[i]);
            }

            return result;
        }

        private static short[] ToInt16(byte[] bytes)
        {
            short[] result = new short[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 2);
            return result;
        }

        private static ushort[] ToUInt16(byte[] bytes)
        {
            ushort[] result = new ushort[bytes.Length / 2];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 2);
            return result;
        }

        private static int[] ToInt32(byte[] bytes)
        {
            int[] result = new int[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 4);
            return result;
        }

        private static uint[] ToUInt32(byte[] bytes)
        {
            uint[] result = new uint[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 4);
            return result;
        }

        private static float[] ToFloat(byte[] bytes)
        {
            float[] result = new float[bytes.Length / 4];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 4);
            return result;
        }

        private static double[] ToDouble(byte[] bytes)
        {
            double[] result = new double[bytes.Length / 8];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 8);
            return result;
        }

        private static long[] ToInt64(byte[] bytes)
        {
            long[] result = new long[bytes.Length / 8];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 8);
            return result;
        }

        private static ulong[] ToUInt64(byte[] bytes)
        {
            ulong[] result = new ulong[bytes.Length / 8];
            Buffer.BlockCopy(bytes, 0, result, 0, result.Length * 8);
            return result;
        }

        private static bool IsUnserializableToken(string token)
            => token is "Infinity" or "-Infinity" or "NaN" or "-0";

        private static JsonElement SerializeClrToJsonElement(object parsed)
        {
            try
            {
                return JsonSerializer.SerializeToElement(parsed);
            }
            catch (JsonException)
            {
                return JsonSerializer.SerializeToElement(parsed, PreserveOptions);
            }
        }

        private static object ParseToClr(JsonElement result, IDictionary<int, object> refs)
        {
            if (result.ValueKind != JsonValueKind.Object)
            {
                return result.ValueKind switch
                {
                    JsonValueKind.String => result.GetString(),
                    JsonValueKind.Number => ReadNumber(result),
                    JsonValueKind.True => true,
                    JsonValueKind.False => false,
                    JsonValueKind.Null => null,
                    _ => null,
                };
            }

            if (result.TryGetProperty("v", out JsonElement special))
            {
                if (special.ValueKind == JsonValueKind.Null)
                {
                    return null;
                }

                return special.GetString() switch
                {
                    "null" => null,
                    "undefined" => null,
                    "Infinity" => double.PositiveInfinity,
                    "-Infinity" => double.NegativeInfinity,
                    "-0" => -0d,
                    "NaN" => double.NaN,
                    _ => null,
                };
            }

            if (result.TryGetProperty("ref", out JsonElement refValue)
                && refValue.TryGetInt32(out int refId)
                && refs.TryGetValue(refId, out object existing))
            {
                return existing;
            }

            if (result.TryGetProperty("d", out JsonElement date))
            {
                string iso = date.ValueKind == JsonValueKind.String ? date.GetString() : date.GetRawText().Trim('"');
                if (DateTimeOffset.TryParse(iso, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out DateTimeOffset dto))
                {
                    return dto.UtcDateTime;
                }

                return iso;
            }

            if (result.TryGetProperty("u", out JsonElement url)
                && url.ValueKind == JsonValueKind.String)
            {
                return new Uri(url.GetString());
            }

            if (result.TryGetProperty("bi", out JsonElement bigInt)
                && bigInt.ValueKind == JsonValueKind.String)
            {
                if (BigInteger.TryParse(bigInt.GetString(), NumberStyles.Integer, CultureInfo.InvariantCulture, out BigInteger parsedBig))
                {
                    return parsedBig;
                }

                return bigInt.GetString();
            }

            if (result.TryGetProperty("e", out JsonElement error)
                && error.ValueKind == JsonValueKind.Object)
            {
                string name = error.TryGetProperty("n", out JsonElement errorName) ? errorName.GetString() : "Error";
                string message = error.TryGetProperty("m", out JsonElement errorMessage) ? errorMessage.GetString() : string.Empty;
                string stack = error.TryGetProperty("s", out JsonElement errorStack) ? errorStack.GetString() : string.Empty;
                return new JavaScriptEvalError
                {
                    Name = name ?? "Error",
                    Message = message ?? string.Empty,
                    Stack = stack ?? string.Empty,
                };
            }

            if (result.TryGetProperty("r", out JsonElement regex)
                && regex.ValueKind == JsonValueKind.Object
                && regex.TryGetProperty("p", out JsonElement pattern)
                && pattern.ValueKind == JsonValueKind.String)
            {
                string flags = regex.TryGetProperty("f", out JsonElement flagEl) && flagEl.ValueKind == JsonValueKind.String
                    ? flagEl.GetString()
                    : string.Empty;
                RegexOptions options = RegexOptions.None;
                if (!string.IsNullOrEmpty(flags) && flags.Contains('i', StringComparison.Ordinal))
                {
                    options |= RegexOptions.IgnoreCase;
                }

                if (!string.IsNullOrEmpty(flags) && flags.Contains('m', StringComparison.Ordinal))
                {
                    options |= RegexOptions.Multiline;
                }

                return new Regex(pattern.GetString() ?? string.Empty, options);
            }

            if (result.TryGetProperty("ta", out JsonElement typed)
                && typed.ValueKind == JsonValueKind.Object
                && typed.TryGetProperty("b", out JsonElement base64)
                && base64.ValueKind == JsonValueKind.String
                && typed.TryGetProperty("k", out JsonElement kind)
                && kind.ValueKind == JsonValueKind.String)
            {
                return ParseTypedArray(kind.GetString(), Convert.FromBase64String(base64.GetString() ?? string.Empty));
            }

            if (result.TryGetProperty("b", out JsonElement boolean)
                && (boolean.ValueKind == JsonValueKind.True || boolean.ValueKind == JsonValueKind.False))
            {
                return boolean.GetBoolean();
            }

            if (result.TryGetProperty("s", out JsonElement stringValue)
                && stringValue.ValueKind == JsonValueKind.String)
            {
                return stringValue.GetString();
            }

            if (result.TryGetProperty("n", out JsonElement numericValue)
                && numericValue.ValueKind == JsonValueKind.Number)
            {
                return ReadNumber(numericValue);
            }

            if (result.TryGetProperty("o", out JsonElement obj)
                && obj.ValueKind == JsonValueKind.Array)
            {
                ExpandoObject expando = new ExpandoObject();
                if (result.TryGetProperty("id", out JsonElement objectId)
                    && objectId.TryGetInt32(out int oid))
                {
                    refs[oid] = expando;
                }

                IDictionary<string, object> dict = expando;
                foreach (JsonElement entry in obj.EnumerateArray())
                {
                    if (!entry.TryGetProperty("k", out JsonElement keyElement)
                        || keyElement.ValueKind != JsonValueKind.String
                        || !entry.TryGetProperty("v", out JsonElement valueElement))
                    {
                        continue;
                    }

                    string key = keyElement.GetString();
                    if (string.Equals(key, "__proto__", StringComparison.Ordinal))
                    {
                        continue;
                    }

                    dict[key] = ParseToClr(valueElement, refs);
                }

                return expando;
            }

            if (result.TryGetProperty("a", out JsonElement array)
                && array.ValueKind == JsonValueKind.Array)
            {
                List<object> list = new List<object>();
                if (result.TryGetProperty("id", out JsonElement arrayId)
                    && arrayId.TryGetInt32(out int aid))
                {
                    refs[aid] = list;
                }

                foreach (JsonElement item in array.EnumerateArray())
                {
                    list.Add(ParseToClr(item, refs));
                }

                return list.ToArray();
            }

            return null;
        }

        private static object ReadNumber(JsonElement numericValue)
        {
            string raw = numericValue.GetRawText();
            if (raw.IndexOf('.') < 0
                && raw.IndexOf('e') < 0
                && raw.IndexOf('E') < 0
                && numericValue.TryGetInt32(out int i))
            {
                return i;
            }

            return numericValue.GetDouble();
        }

        private static object ToExpectedType(object parsed, Type t, IDictionary<object, object> visited)
        {
            if (parsed == null)
            {
                return null;
            }

            if (visited.TryGetValue(parsed, out object cached))
            {
                return cached;
            }

            Type underlying = Nullable.GetUnderlyingType(t) ?? t;
            if (underlying.IsInstanceOfType(parsed))
            {
                return parsed;
            }

            if (parsed is Array parsedArray && t.IsArray)
            {
                Type elementType = t.GetElementType();
                Array result = Array.CreateInstance(elementType, parsedArray.Length);
                visited[parsed] = result;
                for (int i = 0; i < parsedArray.Length; i++)
                {
                    result.SetValue(ToExpectedType(parsedArray.GetValue(i), elementType, visited), i);
                }

                return result;
            }

            if (parsed is ExpandoObject parsedExpando)
            {
                if (IsStringDictionary(underlying, out Type valueType))
                {
                    object dictResult = Activator.CreateInstance(t);
                    visited[parsed] = dictResult;
                    IDictionary dict = (IDictionary)dictResult;
                    foreach (KeyValuePair<string, object> kv in parsedExpando)
                    {
                        dict.Add(kv.Key, ToExpectedType(kv.Value, valueType, visited));
                    }

                    return dictResult;
                }

                object objResult = Activator.CreateInstance(t);
                visited[parsed] = objResult;
                foreach (KeyValuePair<string, object> kv in parsedExpando)
                {
                    PropertyInfo property = FindProperty(t, kv.Key);
                    if (property == null || !property.CanWrite)
                    {
                        continue;
                    }

                    property.SetValue(objResult, ToExpectedType(kv.Value, property.PropertyType, visited));
                }

                return objResult;
            }

            if (parsed is IConvertible)
            {
                return Convert.ChangeType(parsed, underlying, CultureInfo.InvariantCulture);
            }

            return parsed;
        }

        private static bool IsStringDictionary(Type t, out Type valueType)
        {
            valueType = null;
            if (!t.IsGenericType || !typeof(IDictionary).IsAssignableFrom(t))
            {
                return false;
            }

            Type[] args = t.GetGenericArguments();
            if (args.Length != 2 || args[0] != typeof(string))
            {
                return false;
            }

            valueType = args[1];
            return true;
        }

        private static PropertyInfo FindProperty(Type t, string name)
        {
            foreach (PropertyInfo property in t.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return property;
                }
            }

            return null;
        }
    }
}
