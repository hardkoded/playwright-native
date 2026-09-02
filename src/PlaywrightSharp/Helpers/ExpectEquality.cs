// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Reflection;
using System.Text.RegularExpressions;

namespace PlaywrightSharp.Helpers
{
    internal static class ExpectEquality
    {
        internal static readonly object Undefined = new ExpectUndefinedSentinel();

        internal static bool IsUndefined(object value) => value is ExpectUndefinedSentinel;

        internal static bool IsJsNumber(object value)
        {
            return value is double
                || value is float
                || value is decimal
                || value is int
                || value is long
                || value is short
                || value is byte
                || value is uint
                || value is ulong
                || value is ushort
                || value is sbyte;
        }

        internal static bool IsJsPrimitive(object value)
        {
            return value is string
                || value is bool
                || value is BigInteger
                || IsJsNumber(value);
        }

        internal static bool IsJsArray(object value)
        {
            if (value is null || IsUndefined(value) || value is string || value is IDictionary || value is ExpectMap)
            {
                return false;
            }

            if (value is ExpectJsArray || value is Array || value is IList)
            {
                return true;
            }

            return false;
        }

        internal static bool IsJsObject(object value)
        {
            if (value is null)
            {
                return false;
            }

            if (IsUndefined(value) || IsJsPrimitive(value) || value is Delegate)
            {
                return false;
            }

            return true;
        }

        internal static string JsTypeOf(object value)
        {
            if (IsUndefined(value))
            {
                return "undefined";
            }

            if (value is null)
            {
                return "object";
            }

            if (value is string)
            {
                return "string";
            }

            if (value is bool)
            {
                return "boolean";
            }

            if (value is Delegate)
            {
                return "function";
            }

            if (value is BigInteger)
            {
                return "bigint";
            }

            if (IsJsNumber(value))
            {
                return "number";
            }

            if (IsJsArray(value))
            {
                return "array";
            }

            return "object";
        }

        internal static double ToDouble(object value)
        {
            return Convert.ToDouble(value, CultureInfo.InvariantCulture);
        }

        internal static bool IsNegativeZero(double value)
        {
            return value == 0 && BitConverter.DoubleToInt64Bits(value) < 0;
        }

        internal static bool IsCloseTo(double received, double expected, int precision)
        {
            if (double.IsPositiveInfinity(received) && double.IsPositiveInfinity(expected))
            {
                return true;
            }

            if (double.IsNegativeInfinity(received) && double.IsNegativeInfinity(expected))
            {
                return true;
            }

            double expectedDiff = Math.Pow(10, -precision) / 2;
            return Math.Abs(expected - received) < expectedDiff;
        }

        internal static bool SameValue(object a, object b)
        {
            if (ReferenceEquals(a, b))
            {
                return true;
            }

            if (IsUndefined(a) || IsUndefined(b))
            {
                return IsUndefined(a) && IsUndefined(b);
            }

            if (a is null || b is null)
            {
                return a is null && b is null;
            }

            if (IsJsNumber(a) && IsJsNumber(b))
            {
                double da = ToDouble(a);
                double db = ToDouble(b);
                if (double.IsNaN(da) && double.IsNaN(db))
                {
                    return true;
                }

                if (da == 0 && db == 0)
                {
                    return IsNegativeZero(da) == IsNegativeZero(db);
                }

                return da == db;
            }

            if (a is BigInteger ba && b is BigInteger bb)
            {
                return ba == bb;
            }

            if (IsJsPrimitive(a) && IsJsPrimitive(b) && a.GetType() == b.GetType())
            {
                return a.Equals(b);
            }

            return false;
        }

        internal static bool Equals(object a, object b, bool strict, bool subset)
        {
            return EqualsCore(a, b, strict, subset, new List<object>(), new List<object>());
        }

        internal static IList<object> ToList(object value)
        {
            if (value is ExpectJsArray js)
            {
                return js.Items;
            }

            if (value is IList list)
            {
                List<object> items = new List<object>(list.Count);
                foreach (object item in list)
                {
                    items.Add(item);
                }

                return items;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                return enumerable.Cast<object>().ToList();
            }

            throw new ArgumentException("Expected an array-like value.");
        }

        internal static bool HasLength(object value, out int length)
        {
            length = 0;
            if (value is null || IsUndefined(value))
            {
                return false;
            }

            if (value is string text)
            {
                length = text.Length;
                return true;
            }

            if (value is ExpectJsArray js)
            {
                length = js.Items.Count;
                return true;
            }

            if (value is Array array)
            {
                length = array.Length;
                return true;
            }

            if (value is ICollection collection && value is not IDictionary)
            {
                length = collection.Count;
                return true;
            }

            if (value is Delegate del)
            {
                length = del.Method.GetParameters().Length;
                return true;
            }

            PropertyInfo lengthProperty = value.GetType().GetProperty("Length", BindingFlags.Instance | BindingFlags.Public);
            if (lengthProperty != null && lengthProperty.PropertyType == typeof(int))
            {
                length = (int)lengthProperty.GetValue(value);
                return true;
            }

            return false;
        }

        internal static IEnumerable<KeyValuePair<object, object>> GetOwnEntries(object value, bool includeUndefined)
        {
            if (value is ExpectJsArray js)
            {
                for (int i = 0; i < js.Items.Count; i++)
                {
                    yield return new KeyValuePair<object, object>(i, js.Items[i]);
                }

                foreach (KeyValuePair<ExpectSymbol, object> pair in js.Symbols)
                {
                    yield return new KeyValuePair<object, object>(pair.Key, pair.Value);
                }

                yield break;
            }

            if (value is ExpectMap map)
            {
                foreach (KeyValuePair<object, object> pair in map)
                {
                    yield return pair;
                }

                yield break;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (!includeUndefined && IsUndefined(entry.Value))
                    {
                        continue;
                    }

                    yield return new KeyValuePair<object, object>(entry.Key, entry.Value);
                }

                yield break;
            }

            if (value is Exception exception)
            {
                yield return new KeyValuePair<object, object>("message", exception.Message);
                if (exception.InnerException != null)
                {
                    yield return new KeyValuePair<object, object>("cause", exception.InnerException);
                }

                yield break;
            }

            if (value is DateTime || value is DateTimeOffset || value is Regex || value is BigInteger || IsJsPrimitive(value))
            {
                yield break;
            }

            if (value is IEnumerable enumerable && value is not string && IsJsArray(value))
            {
                int index = 0;
                foreach (object item in enumerable)
                {
                    yield return new KeyValuePair<object, object>(index, item);
                    index++;
                }

                yield break;
            }

            Type type = value.GetType();
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object propertyValue = property.GetValue(value);
                if (!includeUndefined && IsUndefined(propertyValue))
                {
                    continue;
                }

                yield return new KeyValuePair<object, object>(ToJsPropertyName(property.Name), propertyValue);
            }
        }

        internal static bool HasOwnProperty(object value, object key)
        {
            if (value is null || IsUndefined(value))
            {
                return false;
            }

            if (value is ExpectJsArray js)
            {
                if (key is ExpectSymbol symbol)
                {
                    return js.Symbols.ContainsKey(symbol);
                }

                if (TryIndex(key, out int index))
                {
                    return index >= 0 && index < js.Items.Count;
                }

                return false;
            }

            if (value is IDictionary dictionary)
            {
                return dictionary.Contains(key) || (key is string text && dictionary.Contains(text));
            }

            if (value is Exception exception)
            {
                string name = key as string;
                return string.Equals(name, "message", StringComparison.Ordinal)
                    || (string.Equals(name, "cause", StringComparison.Ordinal) && exception.InnerException != null);
            }

            if (IsJsArray(value) && TryIndex(key, out int arrayIndex))
            {
                IList<object> items = ToList(value);
                return arrayIndex >= 0 && arrayIndex < items.Count;
            }

            if (key is string propertyName)
            {
                PropertyInfo property = FindProperty(value.GetType(), propertyName);
                return property != null;
            }

            return false;
        }

        internal static bool HasProperty(object value, object key)
        {
            return HasOwnProperty(value, key);
        }

        internal static object GetProperty(object value, object key)
        {
            if (value is ExpectJsArray js)
            {
                if (key is ExpectSymbol symbol)
                {
                    return js.Symbols.TryGetValue(symbol, out object symbolValue) ? symbolValue : Undefined;
                }

                if (TryIndex(key, out int index) && index >= 0 && index < js.Items.Count)
                {
                    return js.Items[index];
                }

                return Undefined;
            }

            if (value is IDictionary dictionary)
            {
                if (dictionary.Contains(key))
                {
                    return dictionary[key];
                }

                if (key is string text && dictionary.Contains(text))
                {
                    return dictionary[text];
                }

                return Undefined;
            }

            if (value is Exception exception)
            {
                if (key is string name && string.Equals(name, "message", StringComparison.Ordinal))
                {
                    return exception.Message;
                }

                if (key is string cause && string.Equals(cause, "cause", StringComparison.Ordinal))
                {
                    return (object)exception.InnerException ?? Undefined;
                }

                return Undefined;
            }

            if (IsJsArray(value) && TryIndex(key, out int arrayIndex))
            {
                IList<object> items = ToList(value);
                return arrayIndex >= 0 && arrayIndex < items.Count ? items[arrayIndex] : Undefined;
            }

            if (key is string propertyName)
            {
                PropertyInfo property = FindProperty(value.GetType(), propertyName);
                if (property != null)
                {
                    return property.GetValue(value);
                }
            }

            return Undefined;
        }

        internal static List<object> PathAsArray(object keyPath)
        {
            if (keyPath is IEnumerable enumerable && keyPath is not string)
            {
                return enumerable.Cast<object>().ToList();
            }

            if (keyPath is not string text)
            {
                return null;
            }

            List<object> parts = new List<object>();
            if (text.Length == 0)
            {
                parts.Add(string.Empty);
                return parts;
            }

            int i = 0;
            while (i < text.Length)
            {
                if (text[i] == '.')
                {
                    i++;
                    continue;
                }

                if (text[i] == '[')
                {
                    int close = text.IndexOf(']', i);
                    if (close < 0)
                    {
                        break;
                    }

                    string raw = text.Substring(i + 1, close - i - 1);
                    parts.Add(int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out int index) ? index : raw);
                    i = close + 1;
                    continue;
                }

                int nextDot = text.IndexOf('.', i);
                int nextBracket = text.IndexOf('[', i);
                int end = text.Length;
                if (nextDot >= 0)
                {
                    end = nextDot;
                }

                if (nextBracket >= 0 && nextBracket < end)
                {
                    end = nextBracket;
                }

                parts.Add(text.Substring(i, end - i));
                i = end;
            }

            return parts;
        }

        internal static bool TryGetPath(object received, IList<object> path, out object value, out bool hasEnd)
        {
            value = received;
            hasEnd = true;
            if (path == null || path.Count == 0)
            {
                return false;
            }

            for (int i = 0; i < path.Count; i++)
            {
                if (value is null || IsUndefined(value) || (!IsJsObject(value) && !IsJsArray(value) && value is not string))
                {
                    hasEnd = false;
                    return false;
                }

                object key = path[i];
                if (!HasOwnProperty(value, key) && !HasArrayIndex(value, key))
                {
                    hasEnd = false;
                    return false;
                }

                value = GetProperty(value, key);
            }

            return true;
        }

        internal static int CompareNumbers(object received, object expected)
        {
            if (received is BigInteger receivedBig)
            {
                if (expected is BigInteger expectedBig)
                {
                    return receivedBig.CompareTo(expectedBig);
                }

                return receivedBig.CompareTo(new BigInteger(ToDouble(expected)));
            }

            if (expected is BigInteger expectedOnly)
            {
                return new BigInteger(ToDouble(received)).CompareTo(expectedOnly);
            }

            return ToDouble(received).CompareTo(ToDouble(expected));
        }

        internal static bool IsTruthy(object value)
        {
            if (value is null || IsUndefined(value))
            {
                return false;
            }

            if (value is bool flag)
            {
                return flag;
            }

            if (value is string text)
            {
                return text.Length > 0;
            }

            if (IsJsNumber(value))
            {
                double number = ToDouble(value);
                return number != 0 && !double.IsNaN(number);
            }

            if (value is BigInteger big)
            {
                return big != BigInteger.Zero;
            }

            return true;
        }

        private static bool HasArrayIndex(object value, object key)
        {
            return IsJsArray(value) && TryIndex(key, out int index) && index >= 0 && index < ToList(value).Count;
        }

        private static bool TryIndex(object key, out int index)
        {
            switch (key)
            {
                case int i:
                    index = i;
                    return true;
                case long l when l >= int.MinValue && l <= int.MaxValue:
                    index = (int)l;
                    return true;
                case string text when int.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out int parsed):
                    index = parsed;
                    return true;
                default:
                    index = 0;
                    return false;
            }
        }

        private static string ToJsPropertyName(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return name;
            }

            return char.ToLowerInvariant(name[0]) + name.Substring(1);
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (string.Equals(property.Name, name, StringComparison.Ordinal)
                    || string.Equals(ToJsPropertyName(property.Name), name, StringComparison.Ordinal))
                {
                    return property;
                }
            }

            return null;
        }

        private static bool EqualsCore(object a, object b, bool strict, bool subset, List<object> aStack, List<object> bStack)
        {
            if (a is ExpectAsymmetric.IMatcher leftMatcher)
            {
                return leftMatcher.AsymmetricMatch(b);
            }

            if (b is ExpectAsymmetric.IMatcher rightMatcher)
            {
                return rightMatcher.AsymmetricMatch(a);
            }

            if (SameValue(a, b))
            {
                return true;
            }

            if (a is null || b is null || IsUndefined(a) || IsUndefined(b))
            {
                return false;
            }

            int stacked = aStack.IndexOf(a);
            if (stacked >= 0)
            {
                return ReferenceEquals(bStack[stacked], b);
            }

            if (a is DateTime dateA && b is DateTime dateB)
            {
                return dateA.ToUniversalTime().Ticks == dateB.ToUniversalTime().Ticks;
            }

            if (a is DateTimeOffset offsetA && b is DateTimeOffset offsetB)
            {
                return offsetA.UtcTicks == offsetB.UtcTicks;
            }

            if (a is Regex regexA && b is Regex regexB)
            {
                return string.Equals(regexA.ToString(), regexB.ToString(), StringComparison.Ordinal)
                    && regexA.Options == regexB.Options;
            }

            if (a is byte[] bytesA && b is byte[] bytesB)
            {
                return bytesA.AsSpan().SequenceEqual(bytesB);
            }

            if (a is ISet<object> || b is ISet<object> || IsSet(a) || IsSet(b))
            {
                return SetEquals(a, b, strict, aStack, bStack);
            }

            if (a is ExpectMap || b is ExpectMap)
            {
                return MapEquals(a, b, strict, aStack, bStack);
            }

            if (IsJsArray(a) || IsJsArray(b))
            {
                if (!IsJsArray(a) || !IsJsArray(b))
                {
                    return false;
                }

                return ArrayEquals(a, b, strict, subset, aStack, bStack);
            }

            if (!IsJsObject(a) || !IsJsObject(b))
            {
                return false;
            }

            if (strict && !subset && a.GetType() != b.GetType()
                && !BothPlainObjects(a, b))
            {
                return false;
            }

            return ObjectEquals(a, b, strict, subset, aStack, bStack);
        }

        private static bool BothPlainObjects(object a, object b)
        {
            return (a is IDictionary && b is IDictionary)
                || (a is IDictionary && IsAnonymous(b))
                || (b is IDictionary && IsAnonymous(a));
        }

        private static bool IsAnonymous(object value)
        {
            Type type = value.GetType();
            return type.Name.Contains("AnonymousType", StringComparison.Ordinal)
                || type.Name.StartsWith("<>", StringComparison.Ordinal);
        }

        private static bool IsSet(object value)
        {
            if (value is null)
            {
                return false;
            }

            Type type = value.GetType();
            return type.IsGenericType && type.GetGenericTypeDefinition() == typeof(HashSet<>);
        }

        private static bool SetEquals(object a, object b, bool strict, List<object> aStack, List<object> bStack)
        {
            if (!IsSet(a) || !IsSet(b))
            {
                return false;
            }

            List<object> left = ((IEnumerable)a).Cast<object>().ToList();
            List<object> right = ((IEnumerable)b).Cast<object>().ToList();
            if (left.Count != right.Count)
            {
                return false;
            }

            aStack.Add(a);
            bStack.Add(b);
            try
            {
                foreach (object item in left)
                {
                    bool found = false;
                    foreach (object candidate in right)
                    {
                        if (EqualsCore(item, candidate, strict, subset: false, aStack, bStack))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                aStack.RemoveAt(aStack.Count - 1);
                bStack.RemoveAt(bStack.Count - 1);
            }
        }

        private static bool MapEquals(object a, object b, bool strict, List<object> aStack, List<object> bStack)
        {
            if (a is not ExpectMap left || b is not ExpectMap right)
            {
                return false;
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            aStack.Add(a);
            bStack.Add(b);
            try
            {
                foreach (KeyValuePair<object, object> pair in left)
                {
                    bool found = false;
                    foreach (KeyValuePair<object, object> candidate in right)
                    {
                        if (EqualsCore(pair.Key, candidate.Key, strict, subset: false, aStack, bStack)
                            && EqualsCore(pair.Value, candidate.Value, strict, subset: false, aStack, bStack))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                aStack.RemoveAt(aStack.Count - 1);
                bStack.RemoveAt(bStack.Count - 1);
            }
        }

        private static bool ArrayEquals(object a, object b, bool strict, bool subset, List<object> aStack, List<object> bStack)
        {
            IList<object> left = ToList(a);
            IList<object> right = ToList(b);
            if (!subset && left.Count != right.Count)
            {
                return false;
            }

            if (subset && left.Count != right.Count)
            {
                return false;
            }

            aStack.Add(a);
            bStack.Add(b);
            try
            {
                int count = left.Count;
                for (int i = 0; i < count; i++)
                {
                    if (!EqualsCore(left[i], right[i], strict, subset, aStack, bStack))
                    {
                        return false;
                    }
                }

                if (a is ExpectJsArray leftJs && b is ExpectJsArray rightJs)
                {
                    if (leftJs.Symbols.Count != rightJs.Symbols.Count && !subset)
                    {
                        return false;
                    }

                    foreach (KeyValuePair<ExpectSymbol, object> pair in leftJs.Symbols)
                    {
                        if (!rightJs.Symbols.TryGetValue(pair.Key, out object other)
                            || !EqualsCore(pair.Value, other, strict, subset, aStack, bStack))
                        {
                            return false;
                        }
                    }
                }

                return true;
            }
            finally
            {
                aStack.RemoveAt(aStack.Count - 1);
                bStack.RemoveAt(bStack.Count - 1);
            }
        }

        private static bool ObjectEquals(object a, object b, bool strict, bool subset, List<object> aStack, List<object> bStack)
        {
            List<KeyValuePair<object, object>> left = GetOwnEntries(a, includeUndefined: strict).ToList();
            List<KeyValuePair<object, object>> right = GetOwnEntries(b, includeUndefined: strict).ToList();

            if (subset)
            {
                aStack.Add(a);
                bStack.Add(b);
                try
                {
                    foreach (KeyValuePair<object, object> pair in right)
                    {
                        if (!HasOwnProperty(a, pair.Key)
                            || !EqualsCore(GetProperty(a, pair.Key), pair.Value, strict, subset, aStack, bStack))
                        {
                            return false;
                        }
                    }

                    return true;
                }
                finally
                {
                    aStack.RemoveAt(aStack.Count - 1);
                    bStack.RemoveAt(bStack.Count - 1);
                }
            }

            if (left.Count != right.Count)
            {
                return false;
            }

            aStack.Add(a);
            bStack.Add(b);
            try
            {
                foreach (KeyValuePair<object, object> pair in left)
                {
                    bool found = false;
                    foreach (KeyValuePair<object, object> candidate in right)
                    {
                        if (KeysEqual(pair.Key, candidate.Key)
                            && EqualsCore(pair.Value, candidate.Value, strict, subset: false, aStack, bStack))
                        {
                            found = true;
                            break;
                        }
                    }

                    if (!found)
                    {
                        return false;
                    }
                }

                return true;
            }
            finally
            {
                aStack.RemoveAt(aStack.Count - 1);
                bStack.RemoveAt(bStack.Count - 1);
            }
        }

        private static bool KeysEqual(object a, object b)
        {
            if (a is string left && b is string right)
            {
                return string.Equals(left, right, StringComparison.Ordinal);
            }

            return SameValue(a, b) || (a != null && a.Equals(b));
        }

        private sealed class ExpectUndefinedSentinel
        {
            public override string ToString() => "undefined";
        }
    }
}
