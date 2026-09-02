// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Jest-style asymmetric matchers for generic expect.
    /// </summary>
    internal static class ExpectAsymmetric
    {
        /// <summary>Asymmetric matcher contract.</summary>
        internal interface IMatcher
        {
            /// <summary>Whether <paramref name="other"/> matches the sample.</summary>
            /// <param name="other">Received value.</param>
            /// <returns>True when the value matches.</returns>
            bool AsymmetricMatch(object other);
        }

        /// <summary>Node <c>expect.any</c>.</summary>
        internal sealed class AnyMatcher : IMatcher
        {
            private readonly Type _constructor;

            internal AnyMatcher(Type constructor)
            {
                _constructor = constructor ?? throw new ArgumentException(
                    "any() expects to be passed a constructor function. Please pass one or use anything() to match any object.");
            }

            public bool AsymmetricMatch(object other)
            {
                if (IsStringConstructor(_constructor))
                {
                    return other is string;
                }

                if (IsNumberConstructor(_constructor))
                {
                    return ExpectEquality.IsJsNumber(other);
                }

                if (IsFunctionConstructor(_constructor))
                {
                    return other is Delegate;
                }

                if (_constructor == typeof(bool))
                {
                    return other is bool;
                }

                if (IsBigIntConstructor(_constructor))
                {
                    return other is System.Numerics.BigInteger;
                }

                if (_constructor == typeof(object))
                {
                    if (other is Delegate)
                    {
                        return false;
                    }

                    return other is null || (!ExpectEquality.IsUndefined(other) && !ExpectEquality.IsJsPrimitive(other));
                }

                if (_constructor == typeof(Array) || _constructor == typeof(IList) || _constructor == typeof(IEnumerable))
                {
                    return ExpectEquality.IsJsArray(other);
                }

                if (other == null || ExpectEquality.IsUndefined(other))
                {
                    return false;
                }

                return _constructor.IsInstanceOfType(other);
            }

            internal static void ThrowIfMissing()
            {
                throw new ArgumentException(
                    "any() expects to be passed a constructor function. Please pass one or use anything() to match any object.");
            }

            private static bool IsStringConstructor(Type type) => type == typeof(string);

            private static bool IsNumberConstructor(Type type)
            {
                return type == typeof(double)
                    || type == typeof(float)
                    || type == typeof(decimal)
                    || type == typeof(int)
                    || type == typeof(long)
                    || type == typeof(short)
                    || type == typeof(byte)
                    || type == typeof(uint)
                    || type == typeof(ulong)
                    || type == typeof(ushort)
                    || type == typeof(sbyte)
                    || string.Equals(type.Name, "ExpectNumber", StringComparison.Ordinal);
            }

            private static bool IsFunctionConstructor(Type type)
                => type == typeof(Delegate) || typeof(Delegate).IsAssignableFrom(type);

            private static bool IsBigIntConstructor(Type type)
                => type == typeof(System.Numerics.BigInteger);
        }

        /// <summary>Node <c>expect.anything</c>.</summary>
        internal sealed class AnythingMatcher : IMatcher
        {
            public bool AsymmetricMatch(object other)
                => other != null && !ExpectEquality.IsUndefined(other);
        }

        /// <summary>Node <c>expect.arrayContaining</c>.</summary>
        internal sealed class ArrayContainingMatcher : IMatcher
        {
            private readonly IList<object> _sample;

            internal ArrayContainingMatcher(IEnumerable sample)
            {
                if (sample == null || sample is string)
                {
                    throw new ArgumentException("You must provide an array to ArrayContaining, not '" + ExpectEquality.JsTypeOf(sample) + "'.");
                }

                _sample = ExpectEquality.ToList(sample);
            }

            public bool AsymmetricMatch(object other)
            {
                if (!ExpectEquality.IsJsArray(other))
                {
                    return false;
                }

                if (_sample.Count == 0)
                {
                    return true;
                }

                IList<object> received = ExpectEquality.ToList(other);
                foreach (object item in _sample)
                {
                    bool found = false;
                    foreach (object candidate in received)
                    {
                        if (ExpectEquality.Equals(item, candidate, strict: false, subset: false))
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
        }

        /// <summary>Node <c>expect.arrayOf</c>.</summary>
        internal sealed class ArrayOfMatcher : IMatcher
        {
            private readonly object _sample;

            internal ArrayOfMatcher(object sample)
            {
                _sample = sample;
            }

            public bool AsymmetricMatch(object other)
            {
                if (!ExpectEquality.IsJsArray(other))
                {
                    return false;
                }

                foreach (object item in ExpectEquality.ToList(other))
                {
                    if (!ExpectEquality.Equals(_sample, item, strict: false, subset: false))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Node <c>expect.objectContaining</c>.</summary>
        internal sealed class ObjectContainingMatcher : IMatcher
        {
            private readonly object _sample;

            internal ObjectContainingMatcher(object sample)
            {
                if (!ExpectEquality.IsJsObject(sample) || ExpectEquality.IsJsArray(sample))
                {
                    throw new ArgumentException(
                        "You must provide an object to ObjectContaining, not '" + ExpectEquality.JsTypeOf(sample) + "'.");
                }

                _sample = sample;
            }

            public bool AsymmetricMatch(object other)
            {
                if (!ExpectEquality.IsJsObject(other) || ExpectEquality.IsJsArray(other))
                {
                    return false;
                }

                foreach (KeyValuePair<object, object> pair in ExpectEquality.GetOwnEntries(_sample, includeUndefined: true))
                {
                    if (!ExpectEquality.HasProperty(other, pair.Key)
                        || !ExpectEquality.Equals(pair.Value, ExpectEquality.GetProperty(other, pair.Key), strict: false, subset: false))
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>Node <c>expect.stringContaining</c>.</summary>
        internal sealed class StringContainingMatcher : IMatcher
        {
            private readonly string _sample;

            internal StringContainingMatcher(string sample)
            {
                _sample = sample ?? throw new ArgumentException("Expected is not a string");
            }

            public bool AsymmetricMatch(object other)
                => other is string text && text.Contains(_sample, StringComparison.Ordinal);
        }

        /// <summary>Node <c>expect.stringMatching</c>.</summary>
        internal sealed class StringMatchingMatcher : IMatcher
        {
            private readonly Regex _sample;

            internal StringMatchingMatcher(object sample)
            {
                if (sample is Regex regex)
                {
                    _sample = regex;
                    return;
                }

                if (sample is string pattern)
                {
                    _sample = new Regex(Regex.Escape(pattern), RegexOptions.None);
                    return;
                }

                throw new ArgumentException("Expected is not a String or a RegExp");
            }

            public bool AsymmetricMatch(object other)
                => other is string text && _sample.IsMatch(text);
        }

        /// <summary>Node <c>expect.closeTo</c>.</summary>
        internal sealed class CloseToMatcher : IMatcher
        {
            private readonly double _expected;
            private readonly int _precision;

            internal CloseToMatcher(object expected, int precision)
            {
                if (!ExpectEquality.IsJsNumber(expected) || expected is System.Numerics.BigInteger)
                {
                    throw new ArgumentException("Expected is not a Number");
                }

                _expected = ExpectEquality.ToDouble(expected);
                _precision = precision;
            }

            public bool AsymmetricMatch(object other)
            {
                if (!ExpectEquality.IsJsNumber(other))
                {
                    return false;
                }

                return ExpectEquality.IsCloseTo(ExpectEquality.ToDouble(other), _expected, _precision);
            }
        }
    }
}
