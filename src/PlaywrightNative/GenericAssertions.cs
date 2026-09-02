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
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IGenericAssertions"/> for Node <c>expect(value)</c> builtins.
    /// </summary>
    public sealed class GenericAssertions : IGenericAssertions
    {
        private readonly object _received;
        private readonly bool _negate;

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericAssertions"/> class.
        /// </summary>
        /// <param name="received">Received value.</param>
        public GenericAssertions(object received)
            : this(received, negate: false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="GenericAssertions"/> class.
        /// </summary>
        /// <param name="received">Received value.</param>
        /// <param name="negate">When <see langword="true"/>, invert each assertion.</param>
        public GenericAssertions(object received, bool negate)
        {
            _received = received;
            _negate = negate;
        }

        /// <inheritdoc/>
        public IGenericAssertions Not => new GenericAssertions(_received, !_negate);

        /// <inheritdoc/>
        public void ToBe(object expected)
        {
            bool pass = ExpectEquality.SameValue(_received, expected);
            Finish("toBe", pass, expected);
        }

        /// <inheritdoc/>
        public void ToEqual(object expected)
        {
            bool pass = ExpectEquality.Equals(_received, expected, strict: false, subset: false);
            Finish("toEqual", pass, expected);
        }

        /// <inheritdoc/>
        public void ToStrictEqual(object expected)
        {
            bool pass = ExpectEquality.Equals(_received, expected, strict: true, subset: false);
            Finish("toStrictEqual", pass, expected);
        }

        /// <inheritdoc/>
        public void ToBeInstanceOf(object constructor)
        {
            if (constructor is not Type type)
            {
                throw new ArgumentException("expected value must be a function");
            }

            bool pass;
            if (_received is null
                || ExpectEquality.IsUndefined(_received)
                || ExpectEquality.IsJsPrimitive(_received))
            {
                pass = false;
            }
            else if (type == typeof(Array))
            {
                pass = ExpectEquality.IsJsArray(_received);
            }
            else
            {
                pass = type.IsInstanceOfType(_received);
            }

            Finish("toBeInstanceOf", pass, constructor);
        }

        /// <inheritdoc/>
        public void ToBeTruthy()
        {
            Finish("toBeTruthy", ExpectEquality.IsTruthy(_received), expected: null);
        }

        /// <inheritdoc/>
        public void ToBeFalsy()
        {
            Finish("toBeFalsy", !ExpectEquality.IsTruthy(_received), expected: null);
        }

        /// <inheritdoc/>
        public void ToBeNaN()
        {
            bool pass = ExpectEquality.IsJsNumber(_received) && double.IsNaN(ExpectEquality.ToDouble(_received));
            Finish("toBeNaN", pass, expected: null);
        }

        /// <inheritdoc/>
        public void ToBeNull()
        {
            Finish("toBeNull", _received is null, expected: null);
        }

        /// <inheritdoc/>
        public void ToBeDefined()
        {
            Finish("toBeDefined", !ExpectEquality.IsUndefined(_received), expected: null);
        }

        /// <inheritdoc/>
        public void ToBeUndefined()
        {
            Finish("toBeUndefined", ExpectEquality.IsUndefined(_received), expected: null);
        }

        /// <inheritdoc/>
        public void ToBeGreaterThan(object expected)
        {
            Finish("toBeGreaterThan", ExpectEquality.CompareNumbers(_received, expected) > 0, expected);
        }

        /// <inheritdoc/>
        public void ToBeGreaterThanOrEqual(object expected)
        {
            Finish("toBeGreaterThanOrEqual", ExpectEquality.CompareNumbers(_received, expected) >= 0, expected);
        }

        /// <inheritdoc/>
        public void ToBeLessThan(object expected)
        {
            Finish("toBeLessThan", ExpectEquality.CompareNumbers(_received, expected) < 0, expected);
        }

        /// <inheritdoc/>
        public void ToBeLessThanOrEqual(object expected)
        {
            Finish("toBeLessThanOrEqual", ExpectEquality.CompareNumbers(_received, expected) <= 0, expected);
        }

        /// <inheritdoc/>
        public void ToContain(object expected)
        {
            if (_received is null || ExpectEquality.IsUndefined(_received))
            {
                throw new ArgumentException("received value must not be null nor undefined");
            }

            bool pass;
            if (_received is string text)
            {
                if (expected is not string needle)
                {
                    throw new ArgumentException("expected value must be a string if received value is a string");
                }

                pass = text.Contains(needle, StringComparison.Ordinal);
            }
            else
            {
                pass = false;
                foreach (object item in Enumerate(_received))
                {
                    if (ExpectEquality.SameValue(item, expected) || ReferenceEquals(item, expected))
                    {
                        pass = true;
                        break;
                    }
                }
            }

            Finish("toContain", pass, expected);
        }

        /// <inheritdoc/>
        public void ToContainEqual(object expected)
        {
            if (_received is null || ExpectEquality.IsUndefined(_received))
            {
                throw new ArgumentException("received value must not be null nor undefined");
            }

            bool pass = false;
            foreach (object item in Enumerate(_received))
            {
                if (ExpectEquality.Equals(item, expected, strict: false, subset: false))
                {
                    pass = true;
                    break;
                }
            }

            Finish("toContainEqual", pass, expected);
        }

        /// <inheritdoc/>
        public void ToBeCloseTo(object expected)
        {
            ToBeCloseTo(expected, 2);
        }

        /// <inheritdoc/>
        public void ToBeCloseTo(object expected, int precision)
        {
            if (!ExpectEquality.IsJsNumber(expected))
            {
                throw new ArgumentException("expected value must be a number");
            }

            if (!ExpectEquality.IsJsNumber(_received))
            {
                throw new ArgumentException("received value must be a number");
            }

            bool pass = ExpectEquality.IsCloseTo(
                ExpectEquality.ToDouble(_received),
                ExpectEquality.ToDouble(expected),
                precision);
            Finish("toBeCloseTo", pass, expected);
        }

        /// <inheritdoc/>
        public void ToMatch(object expected)
        {
            if (_received is not string text)
            {
                throw new ArgumentException("received value must be a string");
            }

            bool pass;
            if (expected is string substring)
            {
                pass = text.Contains(substring, StringComparison.Ordinal);
            }
            else if (expected is Regex regex)
            {
                pass = new Regex(regex.ToString(), regex.Options | RegexOptions.CultureInvariant).IsMatch(text);
                regex.Match(string.Empty);
            }
            else
            {
                throw new ArgumentException("expected value must be a string or regular expression");
            }

            Finish("toMatch", pass, expected);
        }

        /// <inheritdoc/>
        public void ToHaveLength(int length)
        {
            if (!ExpectEquality.HasLength(_received, out int actual))
            {
                throw new ArgumentException("received value must have a length property whose value must be a number");
            }

            Finish("toHaveLength", actual == length, length);
        }

        /// <inheritdoc/>
        public void ToHaveProperty(object keyPath)
        {
            ToHavePropertyCore(keyPath, hasValue: false, value: null);
        }

        /// <inheritdoc/>
        public void ToHaveProperty(object keyPath, object value)
        {
            ToHavePropertyCore(keyPath, hasValue: true, value);
        }

        /// <inheritdoc/>
        public void ToMatchObject(object expected)
        {
            if (!ExpectEquality.IsJsObject(_received) || _received is null)
            {
                throw new ArgumentException("received value must be a non-null object");
            }

            if (!ExpectEquality.IsJsObject(expected) || expected is null)
            {
                throw new ArgumentException("expected value must be a non-null object");
            }

            bool pass = ExpectEquality.Equals(_received, expected, strict: false, subset: true);
            Finish("toMatchObject", pass, expected);
        }

        /// <inheritdoc/>
        public void ToThrow()
        {
            ToThrow(expected: null);
        }

        /// <inheritdoc/>
        public Task ToBeOKAsync()
        {
            if (_received is IAPIResponse response)
            {
                return new APIResponseAssertions(response, _negate).ToBeOKAsync();
            }

            throw new PlaywrightNativeException("toBeOK can be only used with APIResponse object");
        }

        /// <inheritdoc/>
        public void ToThrow(object expected)
        {
            if (_received is not Delegate callback)
            {
                throw new ArgumentException("received value must be a function");
            }

            Exception thrown = null;
            try
            {
                callback.DynamicInvoke();
            }
            catch (Exception ex)
            {
                thrown = ex is System.Reflection.TargetInvocationException target && target.InnerException != null
                    ? target.InnerException
                    : ex;
            }

            bool pass;
            if (expected is null || ExpectEquality.IsUndefined(expected))
            {
                pass = thrown != null;
            }
            else if (expected is Type type)
            {
                pass = thrown != null && type.IsInstanceOfType(thrown);
            }
            else if (expected is string substring)
            {
                pass = thrown != null && (thrown.Message ?? string.Empty).Contains(substring, StringComparison.Ordinal);
            }
            else if (expected is Regex regex)
            {
                pass = thrown != null && regex.IsMatch(thrown.Message ?? string.Empty);
            }
            else if (thrown != null)
            {
                string expectedMessage = MessageOf(expected);
                pass = string.Equals(thrown.Message, expectedMessage, StringComparison.Ordinal)
                    && CausesEqual(thrown, expected);
            }
            else
            {
                pass = false;
            }

            Finish("toThrow", pass, expected);
        }

        private static string MessageOf(object expected)
        {
            if (expected is Exception ex)
            {
                return ex.Message;
            }

            if (expected is IDictionary dictionary && dictionary.Contains("message"))
            {
                return Convert.ToString(dictionary["message"], System.Globalization.CultureInfo.InvariantCulture);
            }

            object message = ExpectEquality.GetProperty(expected, "message");
            return ExpectEquality.IsUndefined(message) ? null : Convert.ToString(message, System.Globalization.CultureInfo.InvariantCulture);
        }

        private static bool CausesEqual(Exception thrown, object expected)
        {
            object expectedCause = ExpectEquality.GetProperty(expected, "cause");
            if (ExpectEquality.IsUndefined(expectedCause) && expected is IDictionary dictionary && !dictionary.Contains("cause"))
            {
                return true;
            }

            if (ExpectEquality.IsUndefined(expectedCause))
            {
                return thrown.InnerException == null;
            }

            return ReferenceEquals(thrown.InnerException, expectedCause)
                || ExpectEquality.Equals(thrown.InnerException, expectedCause, strict: false, subset: false);
        }

        private static IEnumerable Enumerate(object value)
        {
            if (value is string)
            {
                yield break;
            }

            if (value is IEnumerable enumerable)
            {
                foreach (object item in enumerable)
                {
                    yield return item;
                }
            }
        }

        private void ToHavePropertyCore(object keyPath, bool hasValue, object value)
        {
            if (_received is null || ExpectEquality.IsUndefined(_received))
            {
                throw new ArgumentException("received value must not be null nor undefined");
            }

            List<object> path = ExpectEquality.PathAsArray(keyPath);
            if (path == null)
            {
                throw new ArgumentException("expected path must be a string or array");
            }

            bool found = ExpectEquality.TryGetPath(_received, path, out object actual, out bool hasEnd);
            bool pass = hasValue
                ? found && hasEnd && ExpectEquality.Equals(actual, value, strict: false, subset: false)
                : found && hasEnd;
            Finish("toHaveProperty", pass, hasValue ? value : keyPath);
        }

        private void Finish(string name, bool pass, object expected)
        {
            if (_negate ? !pass : pass)
            {
                return;
            }

            throw ExpectException.Fail(
                name + " failed",
                _received,
                expected,
                name,
                pass,
                0,
                string.Empty);
        }
    }
}
