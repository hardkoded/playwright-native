// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative
{
    /// <summary>
    /// Generic value assertions created by <see cref="Assertions.Expect(object)"/>.
    /// Matches Node Playwright <c>expect(value)</c> builtins.
    /// </summary>
    public interface IGenericAssertions
    {
        /// <summary>
        /// Inverts the next assertion.
        /// </summary>
        IGenericAssertions Not { get; }

        /// <summary>
        /// <c>Object.is</c> equality (Node <c>toBe</c>).
        /// </summary>
        /// <param name="expected">Expected value.</param>
        void ToBe(object expected);

        /// <summary>
        /// Deep equality that ignores <c>undefined</c> keys (Node <c>toEqual</c>).
        /// </summary>
        /// <param name="expected">Expected value.</param>
        void ToEqual(object expected);

        /// <summary>
        /// Deep equality that keeps <c>undefined</c> keys and constructors
        /// (Node <c>toStrictEqual</c>).
        /// </summary>
        /// <param name="expected">Expected value.</param>
        void ToStrictEqual(object expected);

        /// <summary>
        /// Node <c>toBeInstanceOf</c>.
        /// </summary>
        /// <param name="constructor">Expected constructor (a <see cref="Type"/>).</param>
        void ToBeInstanceOf(object constructor);

        /// <summary>Node <c>toBeTruthy</c>.</summary>
        void ToBeTruthy();

        /// <summary>Node <c>toBeFalsy</c>.</summary>
        void ToBeFalsy();

        /// <summary>Node <c>toBeNaN</c>.</summary>
        void ToBeNaN();

        /// <summary>Node <c>toBeNull</c>.</summary>
        void ToBeNull();

        /// <summary>Node <c>toBeDefined</c>.</summary>
        void ToBeDefined();

        /// <summary>Node <c>toBeUndefined</c>.</summary>
        void ToBeUndefined();

        /// <summary>Node <c>toBeGreaterThan</c>.</summary>
        /// <param name="expected">Number or <see cref="System.Numerics.BigInteger"/>.</param>
        void ToBeGreaterThan(object expected);

        /// <summary>Node <c>toBeGreaterThanOrEqual</c>.</summary>
        /// <param name="expected">Number or <see cref="System.Numerics.BigInteger"/>.</param>
        void ToBeGreaterThanOrEqual(object expected);

        /// <summary>Node <c>toBeLessThan</c>.</summary>
        /// <param name="expected">Number or <see cref="System.Numerics.BigInteger"/>.</param>
        void ToBeLessThan(object expected);

        /// <summary>Node <c>toBeLessThanOrEqual</c>.</summary>
        /// <param name="expected">Number or <see cref="System.Numerics.BigInteger"/>.</param>
        void ToBeLessThanOrEqual(object expected);

        /// <summary>Node <c>toContain</c> (substring or SameValue membership).</summary>
        /// <param name="expected">Expected item or substring.</param>
        void ToContain(object expected);

        /// <summary>Node <c>toContainEqual</c> (deep equality membership).</summary>
        /// <param name="expected">Expected item.</param>
        void ToContainEqual(object expected);

        /// <summary>Node <c>toBeCloseTo</c> with default precision 2.</summary>
        /// <param name="expected">Expected number.</param>
        void ToBeCloseTo(object expected);

        /// <summary>Node <c>toBeCloseTo</c>.</summary>
        /// <param name="expected">Expected number.</param>
        /// <param name="precision">Decimal digits.</param>
        void ToBeCloseTo(object expected, int precision);

        /// <summary>Node <c>toMatch</c> (substring or <see cref="Regex"/>).</summary>
        /// <param name="expected">String or regular expression.</param>
        void ToMatch(object expected);

        /// <summary>Node <c>toHaveLength</c>.</summary>
        /// <param name="length">Expected <c>length</c>.</param>
        void ToHaveLength(int length);

        /// <summary>Node <c>toHaveProperty</c> without a value.</summary>
        /// <param name="keyPath">Dot path or path segments.</param>
        void ToHaveProperty(object keyPath);

        /// <summary>Node <c>toHaveProperty</c> with a value.</summary>
        /// <param name="keyPath">Dot path or path segments.</param>
        /// <param name="value">Expected property value.</param>
        void ToHaveProperty(object keyPath, object value);

        /// <summary>Node <c>toMatchObject</c>.</summary>
        /// <param name="expected">Expected subset.</param>
        void ToMatchObject(object expected);

        /// <summary>Node <c>toThrow</c> (any error).</summary>
        void ToThrow();

        /// <summary>Node <c>toThrow</c> with a string, regex, type, or error object.</summary>
        /// <param name="expected">Expected error.</param>
        void ToThrow(object expected);

        /// <summary>
        /// Official <c>expect(value).toBeOK()</c>. Only an
        /// <see cref="IAPIResponse"/> is valid; anything else throws
        /// <c>toBeOK can be only used with APIResponse object</c>.
        /// </summary>
        /// <returns>A task that completes when the response is OK.</returns>
        Task ToBeOKAsync();
    }
}
