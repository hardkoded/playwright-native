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
using System.Collections.Generic;
using System.Numerics;
using System.Text.RegularExpressions;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect-builtins.spec.ts</c> parity. Jest-style generic expect.
    /// </summary>
    [TestFixture]
    public class ExpectBuiltinsParityTests : PageTestEx
    {
        private static IGenericAssertions Expect(object value) => Assertions.Expect(value);

        private sealed class TestClassA
        {
            public TestClassA(int a, int b)
            {
                A = a;
                B = b;
            }

            public int A { get; }

            public int B { get; }
        }

        private sealed class TestClassB
        {
            public TestClassB(int a, int b)
            {
                A = a;
                B = b;
            }

            public int A { get; }

            public int B { get; }
        }

        private class A
        {
        }

        private class B
        {
        }

        private class C : B
        {
        }

        private class E : C
        {
        }

        private class CustomError : Exception
        {
            public CustomError(string message)
                : base(message)
            {
            }
        }

        [PlaywrightTest("expect-builtins.spec.ts", "does not throw")]
        [Test]
        [Timeout(30_000)]
        public void ToBeDoesNotThrow()
        {
            Expect("a").Not.ToBe("b");
            Expect("a").ToBe("a");
            Expect(1).Not.ToBe(2);
            Expect(1).ToBe(1);
            Expect((object)null).Not.ToBe(Assertions.Undefined);
            Expect((object)null).ToBe((object)null);
            Expect(Assertions.Undefined).ToBe(Assertions.Undefined);
            Expect(double.NaN).ToBe(double.NaN);
            Expect(new BigInteger(1)).Not.ToBe(new BigInteger(2));
            Expect(new BigInteger(1)).Not.ToBe(1);
            Expect(new BigInteger(1)).ToBe(new BigInteger(1));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: 1 and 2 #0")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsFor1And2() => Expect((Action)(() => Expect(1).ToBe(2))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: true and false #1")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForTrueAndFalse() => Expect((Action)(() => Expect(true).ToBe(false))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: {} and {} #2")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForEmptyObjects() => Expect((Action)(() => Expect(new Dictionary<string, object>()).ToBe(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: {\"a\":1} and {\"a\":1} #3")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForEqualShapedObjects() => Expect((Action)(() => Expect(Obj(("a", 1))).ToBe(Obj(("a", 1))))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: {\"a\":1} and {\"a\":5} #4")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForDifferentObjects() => Expect((Action)(() => Expect(Obj(("a", 1))).ToBe(Obj(("a", 5))))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: {\"b\":2} and {\"b\":2} #5")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForUndefinedKeyObjects() => Expect((Action)(() => Expect(Obj(("a", Assertions.Undefined), ("b", 2))).ToBe(Obj(("b", 2))))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: \"2020-02-20T00:00:00.000Z\" and \"2020-02-20T00:00:00.000Z\" #6")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForEqualDates()
        {
            DateTime a = new DateTime(2020, 2, 20, 0, 0, 0, DateTimeKind.Utc);
            DateTime b = new DateTime(2020, 2, 20, 0, 0, 0, DateTimeKind.Utc);
            Expect((Action)(() => Expect(a).ToBe(b))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: {} and {} #7")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForRegex() => Expect((Action)(() => Expect(new Regex("received")).ToBe(new Regex("expected")))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: [\"abc\"] and [\"cde\"] #8")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForArrays() => Expect((Action)(() => Expect(new object[] { "abc" }).ToBe(new object[] { "cde" }))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: [] and [] #9")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForEmptyArrays() => Expect((Action)(() => Expect(new object[0]).ToBe(new object[0]))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: null and undefined #10")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForNullAndUndefined() => Expect((Action)(() => Expect((object)null).ToBe(Assertions.Undefined))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for: 0 and 0 #11")]
        [Test]
        [Timeout(30_000)]
        public void ToBeFailsForNegativeZero() => Expect((Action)(() => Expect(-0.0).ToBe(+0.0))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for 'false' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsForFalse() => Expect((Action)(() => Expect(false).Not.ToBe(false))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '1' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsFor1() => Expect((Action)(() => Expect(1).Not.ToBe(1))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '\"a\"' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsForA() => Expect((Action)(() => Expect("a").Not.ToBe("a"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for 'undefined' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsForUndefined() => Expect((Action)(() => Expect(Assertions.Undefined).Not.ToBe(Assertions.Undefined))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for 'null' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsForNull() => Expect((Action)(() => Expect((object)null).Not.ToBe((object)null))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '{}' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsForObject()
        {
            Dictionary<string, object> value = new Dictionary<string, object>();
            Expect((Action)(() => Expect(value).Not.ToBe(value))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '[]' with '.not'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNotFailsForArray()
        {
            object[] value = Array.Empty<object>();
            Expect((Action)(() => Expect(value).Not.ToBe(value))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "does not crash on circular references")]
        [Test]
        [Timeout(30_000)]
        public void ToBeDoesNotCrashOnCircularReferences()
        {
            Dictionary<string, object> obj = new Dictionary<string, object>();
            obj["circular"] = obj;
            Expect((Action)(() => Expect(obj).ToBe(new Dictionary<string, object>()))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "matcherResult property contains matcher name, expected and actual values")]
        [Test]
        [Timeout(30_000)]
        public void ToBeMatcherResultProperty()
        {
            Dictionary<string, object> actual = Obj(("a", 1));
            Dictionary<string, object> expected = Obj(("a", 2));
            try
            {
                Expect(actual).ToBe(expected);
            }
            catch (ExpectException error)
            {
                Expect(Result(error)).ToEqual(Assertions.ObjectContaining(new Dictionary<string, object>
                {
                    ["actual"] = actual,
                    ["expected"] = expected,
                    ["name"] = "toBe",
                }));
            }
        }

        [PlaywrightTest("expect-builtins.spec.ts", "does not ignore keys with undefined values")]
        [Test]
        [Timeout(30_000)]
        public void ToStrictEqualDoesNotIgnoreUndefinedKeys()
            => Expect(Obj(("a", Assertions.Undefined), ("b", 2))).Not.ToStrictEqual(Obj(("b", 2)));

        [PlaywrightTest("expect-builtins.spec.ts", "does not ignore keys with undefined values inside an array")]
        [Test]
        [Timeout(30_000)]
        public void ToStrictEqualDoesNotIgnoreUndefinedKeysInsideArray()
            => Expect(new object[] { Obj(("a", Assertions.Undefined)) }).Not.ToStrictEqual(new object[] { new Dictionary<string, object>() });

        [PlaywrightTest("expect-builtins.spec.ts", "passes when comparing same type")]
        [Test]
        [Timeout(30_000)]
        public void ToStrictEqualPassesWhenComparingSameType()
            => Expect(Obj(("test", new TestClassA(1, 2)))).ToStrictEqual(Obj(("test", new TestClassA(1, 2))));

        [PlaywrightTest("expect-builtins.spec.ts", "does not pass for different types")]
        [Test]
        [Timeout(30_000)]
        public void ToStrictEqualDoesNotPassForDifferentTypes()
            => Expect(Obj(("test", new TestClassA(1, 2)))).Not.ToStrictEqual(Obj(("test", new TestClassB(1, 2))));

        [PlaywrightTest("expect-builtins.spec.ts", "passes for matching buffers")]
        [Test]
        [Timeout(30_000)]
        public void ToStrictEqualPassesForMatchingBuffers()
        {
            Expect(new byte[] { 1 }).ToStrictEqual(new byte[] { 1 });
            Expect(Array.Empty<byte>()).ToStrictEqual(Array.Empty<byte>());
            Expect(new byte[] { 9, 3 }).ToStrictEqual(new byte[] { 9, 3 });
        }

        [PlaywrightTest("expect-builtins.spec.ts", "does not pass when ArrayBuffers are not equal")]
        [Test]
        [Timeout(30_000)]
        public void ToStrictEqualDoesNotPassWhenArrayBuffersAreNotEqual()
            => Expect(new byte[] { 1, 2 }).Not.ToStrictEqual(new byte[] { 0, 0 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #0")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail0() => ToEqualFail(true, false);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #1")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail1() => ToEqualFail(1, 2);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #2")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail2() => ToEqualFail(0, double.Epsilon);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #3")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail3() => ToEqualFail(Obj(("a", 1)), Obj(("a", 2)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #4")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail4() => ToEqualFail("banana", "apple");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #5")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail5() => ToEqualFail((object)null, Assertions.Undefined);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #6")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail6() => ToEqualFail(new object[] { 1 }, new object[] { 2 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #7")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail7() => ToEqualFail(new HashSet<object> { 1, 2 }, new HashSet<object>());

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #8")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail8() => ToEqualFail(new HashSet<object> { 1, 2 }, new HashSet<object> { 1, 2, 3 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #9")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail9() => ToEqualFail(new ExpectMap { ["a"] = 0 }, new ExpectMap { ["b"] = 0 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #10")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail10() => ToEqualFail(new byte[] { 97, 98, 99 }, new byte[] { 97, 98, 100 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #11")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail11() => ToEqualFail(Obj(("a", 1), ("b", 2)), Assertions.ObjectContaining(Obj(("a", 2))));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #12")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail12() => ToEqualFail(new object[] { 1, 3 }, Assertions.ArrayContaining(new object[] { 1, 2 }));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #13")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail13() => ToEqualFail("abd", Assertions.StringContaining("bc"));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #14")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail14() => ToEqualFail("abd", Assertions.StringMatching(new Regex("bc", RegexOptions.IgnoreCase)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #15")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail15() => ToEqualFail(Assertions.Undefined, Assertions.Anything());

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect().toEqual() #16")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualFail16() => ToEqualFail(Assertions.Undefined, Assertions.Any(typeof(Delegate)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #0")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass0() => ToEqualPass(true, true);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #1")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass1() => ToEqualPass(1, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #2")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass2() => ToEqualPass(double.NaN, double.NaN);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #3")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass3() => ToEqualPass("abc", "abc");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #4")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass4() => ToEqualPass(new object[] { 1 }, new object[] { 1 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #5")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass5() => ToEqualPass(new Dictionary<string, object>(), new Dictionary<string, object>());

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #6")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass6() => ToEqualPass(Obj(("a", 99)), Obj(("a", 99)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #7")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass7() => ToEqualPass(new HashSet<object>(), new HashSet<object>());

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #8")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass8() => ToEqualPass(new HashSet<object> { 1, 2 }, new HashSet<object> { 1, 2 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #9")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass9() => ToEqualPass(new HashSet<object> { 1, 2 }, new HashSet<object> { 2, 1 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #10")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass10() => ToEqualPass(new ExpectMap(), new ExpectMap());

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #11")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass11() => ToEqualPass(new byte[] { 97, 98, 99 }, new byte[] { 97, 98, 99 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #12")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass12() => ToEqualPass(Obj(("a", 1), ("b", 2)), Assertions.ObjectContaining(Obj(("a", 1))));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #13")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass13() => ToEqualPass(new object[] { 1, 2, 3 }, Assertions.ArrayContaining(new object[] { 2, 3 }));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #14")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass14() => ToEqualPass("abcd", Assertions.StringContaining("bc"));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #15")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass15() => ToEqualPass("abcd", Assertions.StringMatching("bc"));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #16")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass16() => ToEqualPass(true, Assertions.Anything());

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect().toEqual() #17")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualPass17() => ToEqualPass((Action)(() => { }), Assertions.Any(typeof(Delegate)));

        [PlaywrightTest("expect-builtins.spec.ts", "assertion error matcherResult property contains matcher name, expected and actual values")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualMatcherResultProperty()
        {
            Dictionary<string, object> actual = Obj(("a", 1));
            Dictionary<string, object> expected = Obj(("a", 2));
            try
            {
                Expect(actual).ToEqual(expected);
            }
            catch (ExpectException error)
            {
                Expect(Result(error)).ToEqual(Assertions.ObjectContaining(new Dictionary<string, object>
                {
                    ["actual"] = actual,
                    ["expected"] = expected,
                    ["name"] = "toEqual",
                }));
            }
        }

        [PlaywrightTest("expect-builtins.spec.ts", "symbol based keys in arrays are processed correctly")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualSymbolBasedKeysInArrays()
        {
            ExpectSymbol mySymbol = new ExpectSymbol("test");
            ExpectJsArray actual1 = new ExpectJsArray();
            actual1.Symbols[mySymbol] = 3;
            ExpectJsArray actual2 = new ExpectJsArray();
            actual2.Symbols[mySymbol] = 4;
            ExpectJsArray expected = new ExpectJsArray();
            expected.Symbols[mySymbol] = 3;
            Expect(actual1).ToEqual(expected);
            Expect(actual2).Not.ToEqual(expected);
        }

        [PlaywrightTest("expect-builtins.spec.ts", "non-enumerable members should be skipped during equal")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualSkipsNonEnumerableMembers()
        {
            Dictionary<string, object> actual = Obj(("x", 3));
            Expect(actual).ToEqual(Obj(("x", 3)));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "objectContaining sample can be used multiple times")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualObjectContainingSampleCanBeUsedMultipleTimes()
        {
            object expected = Assertions.ObjectContaining(Obj(("b", 7)));
            Expect(Obj(("a", 1), ("b", 2))).Not.ToEqual(expected);
            Expect(Obj(("a", 3), ("b", 7))).ToEqual(expected);
        }

        [PlaywrightTest("expect-builtins.spec.ts", "properties with the same circularity are equal")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualSameCircularity()
        {
            Dictionary<string, object> a = new Dictionary<string, object>();
            a["x"] = a;
            Dictionary<string, object> b = new Dictionary<string, object>();
            b["x"] = b;
            Expect(a).ToEqual(b);
            Expect(b).ToEqual(a);
        }

        [PlaywrightTest("expect-builtins.spec.ts", "properties with different circularity are not equal")]
        [Test]
        [Timeout(30_000)]
        public void ToEqualDifferentCircularity()
        {
            Dictionary<string, object> a = new Dictionary<string, object>();
            a["x"] = Obj(("y", a));
            Dictionary<string, object> b = new Dictionary<string, object>();
            Dictionary<string, object> bx = new Dictionary<string, object>();
            b["x"] = bx;
            bx["y"] = bx;
            Expect(a).Not.ToEqual(b);
            Expect(b).Not.ToEqual(a);
        }

        [PlaywrightTest("expect-builtins.spec.ts", "passing instanceof #0")]
        [Test]
        [Timeout(30_000)]
        public void PassingInstanceOf0() => InstanceOfPass(new ExpectMap(), typeof(ExpectMap));

        [PlaywrightTest("expect-builtins.spec.ts", "passing instanceof #1")]
        [Test]
        [Timeout(30_000)]
        public void PassingInstanceOf1() => InstanceOfPass(Array.Empty<object>(), typeof(Array));

        [PlaywrightTest("expect-builtins.spec.ts", "passing instanceof #2")]
        [Test]
        [Timeout(30_000)]
        public void PassingInstanceOf2() => InstanceOfPass(new A(), typeof(A));

        [PlaywrightTest("expect-builtins.spec.ts", "passing instanceof #3")]
        [Test]
        [Timeout(30_000)]
        public void PassingInstanceOf3() => InstanceOfPass(new C(), typeof(B));

        [PlaywrightTest("expect-builtins.spec.ts", "passing instanceof #4")]
        [Test]
        [Timeout(30_000)]
        public void PassingInstanceOf4() => InstanceOfPass(new E(), typeof(B));

        [PlaywrightTest("expect-builtins.spec.ts", "failing instanceof #0")]
        [Test]
        [Timeout(30_000)]
        public void FailingInstanceOf0() => InstanceOfFail("a", typeof(string));

        [PlaywrightTest("expect-builtins.spec.ts", "failing instanceof #1")]
        [Test]
        [Timeout(30_000)]
        public void FailingInstanceOf1() => InstanceOfFail(1, typeof(double));

        [PlaywrightTest("expect-builtins.spec.ts", "failing instanceof #2")]
        [Test]
        [Timeout(30_000)]
        public void FailingInstanceOf2() => InstanceOfFail(true, typeof(bool));

        [PlaywrightTest("expect-builtins.spec.ts", "failing instanceof #3")]
        [Test]
        [Timeout(30_000)]
        public void FailingInstanceOf3() => InstanceOfFail(new A(), typeof(B));

        [PlaywrightTest("expect-builtins.spec.ts", "throws if constructor is not a function")]
        [Test]
        [Timeout(30_000)]
        public void ToBeInstanceOfThrowsIfConstructorIsNotAFunction()
            => Expect((Action)(() => Expect(new Dictionary<string, object>()).ToBeInstanceOf(4))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "'[object Object]' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void ObjectIsTruthy() => Truthy(new Dictionary<string, object>());

        [PlaywrightTest("expect-builtins.spec.ts", "'' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void EmptyArrayIsTruthy() => Truthy(Array.Empty<object>());

        [PlaywrightTest("expect-builtins.spec.ts", "'true' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void TrueIsTruthy() => Truthy(true);

        [PlaywrightTest("expect-builtins.spec.ts", "'1' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void OneIsTruthy() => Truthy(1);

        [PlaywrightTest("expect-builtins.spec.ts", "'a' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void StringAIsTruthy() => Truthy("a");

        [PlaywrightTest("expect-builtins.spec.ts", "'0.5' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void HalfIsTruthy() => Truthy(0.5);

        [PlaywrightTest("expect-builtins.spec.ts", "'[object Map]' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void MapIsTruthy() => Truthy(new ExpectMap());

        [PlaywrightTest("expect-builtins.spec.ts", "'() => {}' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void FunctionIsTruthy() => Truthy((Action)(() => { }));

        [PlaywrightTest("expect-builtins.spec.ts", "'Infinity' is truthy")]
        [Test]
        [Timeout(30_000)]
        public void InfinityIsTruthy() => Truthy(double.PositiveInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "'false' is falsy")]
        [Test]
        [Timeout(30_000)]
        public void FalseIsFalsy() => Falsy(false);

        [PlaywrightTest("expect-builtins.spec.ts", "'null' is falsy")]
        [Test]
        [Timeout(30_000)]
        public void NullIsFalsy() => Falsy((object)null);

        [PlaywrightTest("expect-builtins.spec.ts", "'NaN' is falsy")]
        [Test]
        [Timeout(30_000)]
        public void NaNIsFalsy() => Falsy(double.NaN);

        [PlaywrightTest("expect-builtins.spec.ts", "'0' is falsy")]
        [Test]
        [Timeout(30_000)]
        public void ZeroIsFalsy() => Falsy(0);

        [PlaywrightTest("expect-builtins.spec.ts", "'' is falsy")]
        [Test]
        [Timeout(30_000)]
        public void EmptyStringIsFalsy() => Falsy(string.Empty);

        [PlaywrightTest("expect-builtins.spec.ts", "'undefined' is falsy")]
        [Test]
        [Timeout(30_000)]
        public void UndefinedIsFalsy() => Falsy(Assertions.Undefined);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true}")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNaNPass()
        {
            foreach (double value in new[] { double.NaN, Math.Sqrt(-1), double.PositiveInfinity - double.PositiveInfinity, 0.0 / 0.0 })
            {
                Expect(value).ToBeNaN();
                Expect((Action)(() => Expect(value).Not.ToBeNaN())).ToThrow();
            }
        }

        [PlaywrightTest("expect-builtins.spec.ts", "throws")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNaNThrows()
        {
            foreach (object value in new object[] { 1, string.Empty, null, Assertions.Undefined, new Dictionary<string, object>(), Array.Empty<object>(), 0.2, 0, double.PositiveInfinity, double.NegativeInfinity })
            {
                Expect((Action)(() => Expect(value).ToBeNaN())).ToThrow();
                Expect(value).Not.ToBeNaN();
            }
        }

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '[object Object]'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForObject() => NullFail(new Dictionary<string, object>());

        [PlaywrightTest("expect-builtins.spec.ts", "fails for ''")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForEmptyArray() => NullFail(Array.Empty<object>());

        [PlaywrightTest("expect-builtins.spec.ts", "fails for 'true'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForTrue() => NullFail(true);

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '1'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsFor1() => NullFail(1);

        [PlaywrightTest("expect-builtins.spec.ts", "fails for 'a'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForA() => NullFail("a");

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '0.5'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForHalf() => NullFail(0.5);

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '[object Map]'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForMap() => NullFail(new ExpectMap());

        [PlaywrightTest("expect-builtins.spec.ts", "fails for '() => {}'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForFunction() => NullFail((Action)(() => { }));

        [PlaywrightTest("expect-builtins.spec.ts", "fails for 'Infinity'")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForInfinity() => NullFail(double.PositiveInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "fails for null with .not")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullFailsForNullWithNot() => Expect((Action)(() => Expect((object)null).Not.ToBeNull())).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "pass for null")]
        [Test]
        [Timeout(30_000)]
        public void ToBeNullPassForNull() => Expect((object)null).ToBeNull();

        [PlaywrightTest("expect-builtins.spec.ts", "'[object Object]' is defined")]
        [Test]
        [Timeout(30_000)]
        public void ObjectIsDefined() => Defined(new Dictionary<string, object>());

        [PlaywrightTest("expect-builtins.spec.ts", "'' is defined")]
        [Test]
        [Timeout(30_000)]
        public void EmptyArrayIsDefined() => Defined(Array.Empty<object>());

        [PlaywrightTest("expect-builtins.spec.ts", "'true' is defined")]
        [Test]
        [Timeout(30_000)]
        public void TrueIsDefined() => Defined(true);

        [PlaywrightTest("expect-builtins.spec.ts", "'1' is defined")]
        [Test]
        [Timeout(30_000)]
        public void OneIsDefined() => Defined(1);

        [PlaywrightTest("expect-builtins.spec.ts", "'a' is defined")]
        [Test]
        [Timeout(30_000)]
        public void StringAIsDefined() => Defined("a");

        [PlaywrightTest("expect-builtins.spec.ts", "'0.5' is defined")]
        [Test]
        [Timeout(30_000)]
        public void HalfIsDefined() => Defined(0.5);

        [PlaywrightTest("expect-builtins.spec.ts", "'[object Map]' is defined")]
        [Test]
        [Timeout(30_000)]
        public void MapIsDefined() => Defined(new ExpectMap());

        [PlaywrightTest("expect-builtins.spec.ts", "'() => {}' is defined")]
        [Test]
        [Timeout(30_000)]
        public void FunctionIsDefined() => Defined((Action)(() => { }));

        [PlaywrightTest("expect-builtins.spec.ts", "'Infinity' is defined")]
        [Test]
        [Timeout(30_000)]
        public void InfinityIsDefined() => Defined(double.PositiveInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "undefined is undefined")]
        [Test]
        [Timeout(30_000)]
        public void UndefinedIsUndefined()
        {
            Expect(Assertions.Undefined).ToBeUndefined();
            Expect(Assertions.Undefined).Not.ToBeDefined();
            Expect((Action)(() => Expect(Assertions.Undefined).ToBeDefined())).ToThrow();
            Expect((Action)(() => Expect(Assertions.Undefined).Not.ToBeUndefined())).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "comparing 1 and 2")]
        [Test]
        [Timeout(30_000)]
        public void Comparing1And2() => Compare(1, 2);

        [PlaywrightTest("expect-builtins.spec.ts", "comparing -Infinity and Infinity")]
        [Test]
        [Timeout(30_000)]
        public void ComparingInfinities() => Compare(double.NegativeInfinity, double.PositiveInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "comparing 5e-324 and 1.7976931348623157e+308")]
        [Test]
        [Timeout(30_000)]
        public void ComparingMinAndMax() => Compare(double.Epsilon, double.MaxValue);

        [PlaywrightTest("expect-builtins.spec.ts", "comparing 0.1 and 0.2")]
        [Test]
        [Timeout(30_000)]
        public void ComparingFractions() => Compare(0.1, 0.2);

        [PlaywrightTest("expect-builtins.spec.ts", "can compare BigInt to Numbers")]
        [Test]
        [Timeout(30_000)]
        public void CanCompareBigIntToNumbers()
        {
            BigInteger a = new BigInteger(2);
            Expect(a).ToBeGreaterThan(1);
            Expect(a).ToBeGreaterThanOrEqual(2);
            Expect(2).ToBeLessThanOrEqual(a);
            Expect(a).ToBeLessThan(3);
            Expect(a).ToBeLessThanOrEqual(2);
        }

        [PlaywrightTest("expect-builtins.spec.ts", "equal numbers: [1, 1]")]
        [Test]
        [Timeout(30_000)]
        public void EqualNumbers1() => EqualNumbers(1, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "equal numbers: [5e-324, 5e-324]")]
        [Test]
        [Timeout(30_000)]
        public void EqualNumbersEpsilon() => EqualNumbers(double.Epsilon, double.Epsilon);

        [PlaywrightTest("expect-builtins.spec.ts", "equal numbers: [Infinity, Infinity]")]
        [Test]
        [Timeout(30_000)]
        public void EqualNumbersInfinity() => EqualNumbers(double.PositiveInfinity, double.PositiveInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "equal numbers: [-Infinity, -Infinity]")]
        [Test]
        [Timeout(30_000)]
        public void EqualNumbersNegInfinity() => EqualNumbers(double.NegativeInfinity, double.NegativeInfinity);

        private static void ToEqualFail(object left, object right)
        {
            Expect((Action)(() => Expect(left).ToEqual(right))).ToThrow();
            Expect(left).Not.ToEqual(right);
        }

        private static void ToEqualPass(object left, object right)
        {
            Expect(left).ToEqual(right);
            Expect((Action)(() => Expect(left).Not.ToEqual(right))).ToThrow();
        }

        private static void InstanceOfPass(object value, Type type)
        {
            Expect((Action)(() => Expect(value).Not.ToBeInstanceOf(type))).ToThrow();
            Expect(value).ToBeInstanceOf(type);
        }

        private static void InstanceOfFail(object value, Type type)
        {
            Expect((Action)(() => Expect(value).ToBeInstanceOf(type))).ToThrow();
            Expect(value).Not.ToBeInstanceOf(type);
        }

        private static void Truthy(object value)
        {
            Expect(value).ToBeTruthy();
            Expect(value).Not.ToBeFalsy();
            Expect((Action)(() => Expect(value).Not.ToBeTruthy())).ToThrow();
            Expect((Action)(() => Expect(value).ToBeFalsy())).ToThrow();
        }

        private static void Falsy(object value)
        {
            Expect(value).ToBeFalsy();
            Expect(value).Not.ToBeTruthy();
            Expect((Action)(() => Expect(value).ToBeTruthy())).ToThrow();
            Expect((Action)(() => Expect(value).Not.ToBeFalsy())).ToThrow();
        }

        private static void NullFail(object value)
        {
            Expect(value).Not.ToBeNull();
            Expect((Action)(() => Expect(value).ToBeNull())).ToThrow();
        }

        private static void Defined(object value)
        {
            Expect(value).ToBeDefined();
            Expect(value).Not.ToBeUndefined();
            Expect((Action)(() => Expect(value).Not.ToBeDefined())).ToThrow();
            Expect((Action)(() => Expect(value).ToBeUndefined())).ToThrow();
        }

        private static void Compare(object small, object big)
        {
            Expect(small).ToBeLessThan(big);
            Expect(big).Not.ToBeLessThan(small);
            Expect(big).ToBeGreaterThan(small);
            Expect(small).Not.ToBeGreaterThan(big);
            Expect(small).ToBeLessThanOrEqual(big);
            Expect(big).Not.ToBeLessThanOrEqual(small);
            Expect(big).ToBeGreaterThanOrEqual(small);
            Expect(small).Not.ToBeGreaterThanOrEqual(big);
            Expect((Action)(() => Expect(small).ToBeGreaterThan(big))).ToThrow();
            Expect((Action)(() => Expect(small).Not.ToBeLessThan(big))).ToThrow();
            Expect((Action)(() => Expect(big).Not.ToBeGreaterThan(small))).ToThrow();
            Expect((Action)(() => Expect(big).ToBeLessThan(small))).ToThrow();
            Expect((Action)(() => Expect(small).ToBeGreaterThanOrEqual(big))).ToThrow();
            Expect((Action)(() => Expect(small).Not.ToBeLessThanOrEqual(big))).ToThrow();
            Expect((Action)(() => Expect(big).Not.ToBeGreaterThanOrEqual(small))).ToThrow();
            Expect((Action)(() => Expect(big).ToBeLessThanOrEqual(small))).ToThrow();
        }

        private static void EqualNumbers(object n1, object n2)
        {
            Expect(n1).ToBeGreaterThanOrEqual(n2);
            Expect(n1).ToBeLessThanOrEqual(n2);
            Expect((Action)(() => Expect(n1).Not.ToBeGreaterThanOrEqual(n2))).ToThrow();
            Expect((Action)(() => Expect(n1).Not.ToBeLessThanOrEqual(n2))).ToThrow();
        }

        private static Dictionary<string, object> Obj(params (string Key, object Value)[] pairs)
        {
            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach ((string key, object value) in pairs)
            {
                result[key] = value;
            }

            return result;
        }

        private static Dictionary<string, object> Result(ExpectException error)
        {
            return new Dictionary<string, object>
            {
                ["actual"] = error.MatcherResult.Actual,
                ["expected"] = error.MatcherResult.Expected,
                ["name"] = error.MatcherResult.Name,
            };
        }

        [PlaywrightTest("expect-builtins.spec.ts", "iterable")]
        [Test]
        [Timeout(30_000)]
        public void ToContainIterable()
        {
            IEnumerable<int> Iterable()
            {
                yield return 1;
                yield return 2;
                yield return 3;
            }

            Expect(Iterable()).ToContain(2);
            Expect(Iterable()).ToContainEqual(2);
            Expect((Action)(() => Expect(Iterable()).Not.ToContain(1))).ToThrow();
            Expect((Action)(() => Expect(Iterable()).Not.ToContainEqual(1))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "contains #0")]
        [Test]
        [Timeout(30_000)]
        public void Contains0() => Contains(new object[] { 1, 2, 3, 4 }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "contains #1")]
        [Test]
        [Timeout(30_000)]
        public void Contains1() => Contains(new object[] { "a", "b", "c", "d" }, "a");

        [PlaywrightTest("expect-builtins.spec.ts", "contains #2")]
        [Test]
        [Timeout(30_000)]
        public void Contains2() => Contains(new object[] { Assertions.Undefined, null }, (object)null);

        [PlaywrightTest("expect-builtins.spec.ts", "contains #3")]
        [Test]
        [Timeout(30_000)]
        public void Contains3() => Contains(new object[] { Assertions.Undefined, null }, Assertions.Undefined);

        [PlaywrightTest("expect-builtins.spec.ts", "contains #4")]
        [Test]
        [Timeout(30_000)]
        public void Contains4() => Contains("abcdef", "abc");

        [PlaywrightTest("expect-builtins.spec.ts", "contains #5")]
        [Test]
        [Timeout(30_000)]
        public void Contains5() => Contains("11112111", "2");

        [PlaywrightTest("expect-builtins.spec.ts", "contains #6")]
        [Test]
        [Timeout(30_000)]
        public void Contains6() => Contains(new HashSet<object> { "abc", "def" }, "abc");

        [PlaywrightTest("expect-builtins.spec.ts", "contains #7")]
        [Test]
        [Timeout(30_000)]
        public void Contains7() => Contains(new sbyte[] { 0, 1 }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "does not contain #0")]
        [Test]
        [Timeout(30_000)]
        public void DoesNotContain0() => DoesNotContain(new object[] { 1, 2, 3 }, 4);

        [PlaywrightTest("expect-builtins.spec.ts", "does not contain #1")]
        [Test]
        [Timeout(30_000)]
        public void DoesNotContain1() => DoesNotContain(new object[] { null, Assertions.Undefined }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "does not contain #2")]
        [Test]
        [Timeout(30_000)]
        public void DoesNotContain2() => DoesNotContain(new object[] { new Dictionary<string, object>(), new object[0] }, new object[0]);

        [PlaywrightTest("expect-builtins.spec.ts", "does not contain #3")]
        [Test]
        [Timeout(30_000)]
        public void DoesNotContain3() => DoesNotContain(new object[] { new Dictionary<string, object>(), Array.Empty<object>() }, new Dictionary<string, object>());

        [PlaywrightTest("expect-builtins.spec.ts", "error cases")]
        [Test]
        [Timeout(30_000)]
        public void ToContainErrorCases() => Expect((Action)(() => Expect((object)null).ToContain(1))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "contains a value equal #0")]
        [Test]
        [Timeout(30_000)]
        public void ContainsEqual0() => ContainsEqual(new object[] { 1, 2, 3, 4 }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "contains a value equal #1")]
        [Test]
        [Timeout(30_000)]
        public void ContainsEqual1() => ContainsEqual(new object[] { "a", "b", "c", "d" }, "a");

        [PlaywrightTest("expect-builtins.spec.ts", "contains a value equal #2")]
        [Test]
        [Timeout(30_000)]
        public void ContainsEqual2() => ContainsEqual(new object[] { Assertions.Undefined, null }, (object)null);

        [PlaywrightTest("expect-builtins.spec.ts", "contains a value equal #3")]
        [Test]
        [Timeout(30_000)]
        public void ContainsEqual3() => ContainsEqual(new object[] { Obj(("a", "b")), Obj(("a", "c")) }, Obj(("a", "b")));

        [PlaywrightTest("expect-builtins.spec.ts", "contains a value equal #4")]
        [Test]
        [Timeout(30_000)]
        public void ContainsEqual4() => ContainsEqual(new HashSet<object> { 1, 2, 3, 4 }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "error cases for toContainEqual")]
        [Test]
        [Timeout(30_000)]
        public void ToContainEqualErrorCases() => Expect((Action)(() => Expect((object)null).ToContainEqual(1))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(0).toBeCloseTo(0)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPass00() => CloseToPass(0, 0);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(0).toBeCloseTo(0.001)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPass0001() => CloseToPass(0, 0.001);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(1.23).toBeCloseTo(1.229)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPass1231229() => CloseToPass(1.23, 1.229);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(1.23).toBeCloseTo(1.226)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPass1231226() => CloseToPass(1.23, 1.226);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(1.23).toBeCloseTo(1.234)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPass1231234() => CloseToPass(1.23, 1.234);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(Infinity).toBeCloseTo(Infinity)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPassInfinity() => CloseToPass(double.PositiveInfinity, double.PositiveInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(-Infinity).toBeCloseTo(-Infinity)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPassNegInfinity() => CloseToPass(double.NegativeInfinity, double.NegativeInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect(0).toBeCloseTo(0.01)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToFail001() => CloseToFail(0, 0.01);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect(1).toBeCloseTo(1.23)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToFail123() => CloseToFail(1, 1.23);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect(Infinity).toBeCloseTo(-Infinity)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToFailInfinities() => CloseToFail(double.PositiveInfinity, double.NegativeInfinity);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect(Infinity).toBeCloseTo(1.23)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToFailInfinityNumber() => CloseToFail(double.PositiveInfinity, 1.23);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} expect(-Infinity).toBeCloseTo(-1.23)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToFailNegInfinityNumber() => CloseToFail(double.NegativeInfinity, -1.23);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(0).toBeCloseTo(0.1, 0)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPassPrecision0() => CloseToPass(0, 0.1, 0);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(0).toBeCloseTo(0.0001, 3)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPassPrecision3() => CloseToPass(0, 0.0001, 3);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(0).toBeCloseTo(0.000004, 5)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPassPrecision5() => CloseToPass(0, 0.000004, 5);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(2.0000002).toBeCloseTo(2, 5)")]
        [Test]
        [Timeout(30_000)]
        public void CloseToPassPrecision5B() => CloseToPass(2.0000002, 2, 5);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(foo).toMatch(foo)")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchFoo()
        {
            Expect("foo").ToMatch("foo");
            Expect((Action)(() => Expect("foo").Not.ToMatch("foo"))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} expect(Foo bar).toMatch(/^foo/i)")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchFooBar()
        {
            Regex pattern = new Regex("^foo", RegexOptions.IgnoreCase);
            Expect("Foo bar").ToMatch(pattern);
            Expect((Action)(() => Expect("Foo bar").Not.ToMatch(pattern))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "throws: [bar, foo]")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchThrowsBarFoo() => Expect((Action)(() => Expect("bar").ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws: [bar, /foo/]")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchThrowsBarFooRegex() => Expect((Action)(() => Expect("bar").ToMatch(new Regex("foo")))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #0")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString0() => Expect((Action)(() => Expect(1).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #1")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString1() => Expect((Action)(() => Expect(new Dictionary<string, object>()).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #2")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString2() => Expect((Action)(() => Expect(Array.Empty<object>()).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #3")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString3() => Expect((Action)(() => Expect(true).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #4")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString4() => Expect((Action)(() => Expect(new Regex("foo", RegexOptions.IgnoreCase)).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #5")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString5() => Expect((Action)(() => Expect((Action)(() => { })).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String actual value passed #6")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchNonString6() => Expect((Action)(() => Expect(Assertions.Undefined).ToMatch("foo"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String/RegExp expected value passed #0")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchBadExpected0() => Expect((Action)(() => Expect("foo").ToMatch(1))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String/RegExp expected value passed #1")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchBadExpected1() => Expect((Action)(() => Expect("foo").ToMatch(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String/RegExp expected value passed #2")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchBadExpected2() => Expect((Action)(() => Expect("foo").ToMatch(Array.Empty<object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String/RegExp expected value passed #3")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchBadExpected3() => Expect((Action)(() => Expect("foo").ToMatch(true))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String/RegExp expected value passed #4")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchBadExpected4() => Expect((Action)(() => Expect("foo").ToMatch((Action)(() => { })))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws if non String/RegExp expected value passed #5")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchBadExpected5() => Expect((Action)(() => Expect("foo").ToMatch(Assertions.Undefined))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "escapes strings properly")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchEscapesStrings() => Expect("this?: throws").ToMatch("this?: throws");

        [PlaywrightTest("expect-builtins.spec.ts", "does not maintain RegExp state between calls")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchDoesNotMaintainRegExpState()
        {
            Regex regex = new Regex(@"f\d+", RegexOptions.IgnoreCase);
            Expect("f123").ToMatch(regex);
            Expect("F456").ToMatch(regex);
            Expect(0).ToBe(0);
        }

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveLength(2) #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthPass0() => HaveLengthPass(new object[] { 1, 2 }, 2);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveLength(0) #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthPass1() => HaveLengthPass(Array.Empty<object>(), 0);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveLength(2) #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthPass2() => HaveLengthPass(new object[] { "a", "b" }, 2);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveLength(3) #3")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthPass3() => HaveLengthPass("abc", 3);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveLength(0) #4")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthPass4() => HaveLengthPass(string.Empty, 0);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveLength(0) #5")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthPass5() => HaveLengthPass((Action)(() => { }), 0);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveLength(3) #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthFail0() => HaveLengthFail(new object[] { 1, 2 }, 3);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveLength(1) #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthFail1() => HaveLengthFail(Array.Empty<object>(), 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveLength(99) #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthFail2() => HaveLengthFail(new object[] { "a", "b" }, 99);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveLength(66) #3")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthFail3() => HaveLengthFail("abc", 66);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveLength(1) #4")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthFail4() => HaveLengthFail(string.Empty, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "error cases")]
        [Test]
        [Timeout(30_000)]
        public void ToHaveLengthErrorCases()
        {
            Expect((Action)(() => Expect(Obj(("a", 9))).ToHaveLength(1))).ToThrow();
            Expect((Action)(() => Expect(0).ToHaveLength(1))).ToThrow();
            Expect((Action)(() => Expect(Assertions.Undefined).Not.ToHaveLength(1))).ToThrow();
        }

        private static void Contains(object list, object value)
        {
            Expect(list).ToContain(value);
            Expect((Action)(() => Expect(list).Not.ToContain(value))).ToThrow();
        }

        private static void DoesNotContain(object list, object value)
        {
            Expect(list).Not.ToContain(value);
            Expect((Action)(() => Expect(list).ToContain(value))).ToThrow();
        }

        private static void ContainsEqual(object list, object value)
        {
            Expect(list).ToContainEqual(value);
            Expect((Action)(() => Expect(list).Not.ToContainEqual(value))).ToThrow();
        }

        private static void CloseToPass(double left, double right)
        {
            Expect(left).ToBeCloseTo(right);
            Expect((Action)(() => Expect(left).Not.ToBeCloseTo(right))).ToThrow();
        }

        private static void CloseToPass(double left, double right, int precision)
        {
            Expect(left).ToBeCloseTo(right, precision);
            Expect((Action)(() => Expect(left).Not.ToBeCloseTo(right, precision))).ToThrow();
        }

        private static void CloseToFail(double left, double right)
        {
            Expect(left).Not.ToBeCloseTo(right);
            Expect((Action)(() => Expect(left).ToBeCloseTo(right))).ToThrow();
        }

        private static void HaveLengthPass(object received, int length)
        {
            Expect(received).ToHaveLength(length);
            Expect((Action)(() => Expect(received).Not.ToHaveLength(length))).ToThrow();
        }

        private static void HaveLengthFail(object received, int length)
        {
            Expect(received).Not.ToHaveLength(length);
            Expect((Action)(() => Expect(received).ToHaveLength(length))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue0() => HavePropertyValue(Nest(), "a.b.c.d", 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue1() => HavePropertyValue(Nest(), new object[] { "a", "b", "c", "d" }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue2() => HavePropertyValue(Obj(("a.b.c.d", 1)), new object[] { "a.b.c.d" }, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #3")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue3() => HavePropertyValue(Obj(("a", Obj(("b", new object[] { 1, 2, 3 })))), new object[] { "a", "b", 1 }, 2);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #4")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue4() => HavePropertyValue(Obj(("a", Obj(("b", new object[] { 1, 2, 3 })))), new object[] { "a", "b", 1 }, Assertions.Any(typeof(double)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #5")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue5() => HavePropertyValue(Obj(("a", 0)), "a", 0);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #6")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue6() => HavePropertyValue(Obj(("a", Obj(("b", Assertions.Undefined)))), "a.b", Assertions.Undefined);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #7")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue7() => HavePropertyValue(Obj(("a", Obj(("b", Obj(("c", 5)))))), "a.b", Obj(("c", 5)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #8")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue8() => HavePropertyValue(
            Obj(("a", Obj(("b", new object[] { Obj(("c", new object[] { Obj(("d", 1)) })) })))),
            "a.b[0].c[0].d",
            1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty with value #9")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValue9() => HavePropertyValue(Obj((string.Empty, 1)), string.Empty, 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail0() => HavePropertyValueFail(Nest(), "a.b.ttt.d", 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail1() => HavePropertyValueFail(Nest(), "a.b.c.d", 2);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail2() => HavePropertyValueFail(Obj(("a.b.c.d", 1)), "a.b.c.d", 2);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #3")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail3() => HavePropertyValueFail(Obj(("a", Obj(("b", Obj(("c", new Dictionary<string, object>())))))), "a.b.c.d", 1);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #4")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail4() => HavePropertyValueFail(Obj(("a", 1)), "a.b.c.d", 5);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #5")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail5() => HavePropertyValueFail(new Dictionary<string, object>(), "a", "test");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty with value #6")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyValueFail6() => HavePropertyValueFail(Obj(("a", Obj(("b", 3)))), "a.b", Assertions.Undefined);

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty without value #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPath0() => HaveProperty(Nest(), "a.b.c.d");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty without value #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPath1() => HaveProperty(Nest(), new object[] { "a", "b", "c", "d" });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty without value #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPath2() => HaveProperty(Obj(("a.b.c.d", 1)), new object[] { "a.b.c.d" });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty without value #3")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPath3() => HaveProperty(Obj(("a", 0)), "a");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toHaveProperty without value #4")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPath4() => HaveProperty(Obj(("a", Obj(("b", Assertions.Undefined)))), "a.b");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty without value #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPathFail0() => HavePropertyFail(Obj(("a", Obj(("b", Obj(("c", new Dictionary<string, object>())))))), "a.b.c.d");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty without value #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPathFail1() => HavePropertyFail(Obj(("a", 1)), "a.b.c.d");

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toHaveProperty without value #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyPathFail2() => HavePropertyFail(new Dictionary<string, object>(), "a");

        [PlaywrightTest("expect-builtins.spec.ts", "{error} toHaveProperty #0")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyError0() => Expect((Action)(() => Expect((object)null).ToHaveProperty("a.b"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "{error} toHaveProperty #1")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyError1() => Expect((Action)(() => Expect(Assertions.Undefined).ToHaveProperty("a"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "{error} toHaveProperty #2")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyError2() => Expect((Action)(() => Expect(Obj(("a", Obj(("b", new Dictionary<string, object>()))))).ToHaveProperty(Assertions.Undefined))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "{error} toHaveProperty #3")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyError3() => Expect((Action)(() => Expect(Obj(("a", Obj(("b", new Dictionary<string, object>()))))).ToHaveProperty((object)null))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "{error} toHaveProperty #4")]
        [Test]
        [Timeout(30_000)]
        public void ToHavePropertyError4() => Expect((Action)(() => Expect(Obj(("a", Obj(("b", new Dictionary<string, object>()))))).ToHaveProperty(1))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #0")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject0() => MatchObjectPass(Obj(("a", "b"), ("c", "d")), Obj(("a", "b")));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #1")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject1() => MatchObjectPass(Obj(("a", "b"), ("c", "d")), Obj(("a", "b"), ("c", "d")));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #2")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject2() => MatchObjectPass(
            Obj(("a", "b"), ("t", Obj(("x", Obj(("r", "r"))), ("z", "z")))),
            Obj(("a", "b"), ("t", Obj(("z", "z")))));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #3")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject3() => MatchObjectPass(Obj(("a", new object[] { 3, 4, 5 }), ("b", "b")), Obj(("a", new object[] { 3, 4, 5 })));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #4")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject4() => MatchObjectPass(Obj(("a", 1), ("c", 2)), Obj(("a", Assertions.Any(typeof(double)))));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #5")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject5() => MatchObjectPass(new HashSet<object> { 1, 2 }, new HashSet<object> { 1, 2 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #6")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject6() => MatchObjectPass(new DateTime(2015, 11, 30, 0, 0, 0, DateTimeKind.Utc), new DateTime(2015, 11, 30, 0, 0, 0, DateTimeKind.Utc));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #7")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject7() => MatchObjectPass(Obj(("a", null), ("b", "b")), Obj(("a", null)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #8")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject8() => MatchObjectPass(Obj(("a", Assertions.Undefined), ("b", "b")), Obj(("a", Assertions.Undefined)));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #9")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject9() => MatchObjectPass(new object[] { 1, 2 }, new object[] { 1, 2 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #10")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject10() => MatchObjectPass(new Exception("foo"), new Exception("foo"));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: true} toMatchObject #11")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObject11() => MatchObjectPass(new Exception("bar"), Obj(("message", "bar")));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #0")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail0() => MatchObjectFail(Obj(("a", "b"), ("c", "d")), Obj(("e", "b")));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #1")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail1() => MatchObjectFail(Obj(("a", "b"), ("c", "d")), Obj(("a", "b!"), ("c", "d")));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #2")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail2() => MatchObjectFail(Obj(("a", "a"), ("c", "d")), Obj(("a", Assertions.Any(typeof(double)))));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #3")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail3() => MatchObjectFail(Obj(("a", new object[] { 3, 4, 5 }), ("b", "b")), Obj(("a", new object[] { 3, 4, 5, 6 })));

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #4")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail4() => MatchObjectFail(new object[] { 1, 2 }, new object[] { 1, 3 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #5")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail5() => MatchObjectFail(new HashSet<object> { 1, 2 }, new HashSet<object> { 2 });

        [PlaywrightTest("expect-builtins.spec.ts", "{pass: false} toMatchObject #6")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectFail6() => MatchObjectFail(new Exception("foo"), new Exception("bar"));

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #0")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows0() => Expect((Action)(() => Expect((object)null).ToMatchObject(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #1")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows1() => Expect((Action)(() => Expect(4).ToMatchObject(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #2")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows2() => Expect((Action)(() => Expect("44").ToMatchObject(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #3")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows3() => Expect((Action)(() => Expect(true).ToMatchObject(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #4")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows4() => Expect((Action)(() => Expect(Assertions.Undefined).ToMatchObject(new Dictionary<string, object>()))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #5")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows5() => Expect((Action)(() => Expect(new Dictionary<string, object>()).ToMatchObject((object)null))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "throws toMatchObject #6")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectThrows6() => Expect((Action)(() => Expect(new Dictionary<string, object>()).ToMatchObject(4))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "does not match properties up in the prototype chain")]
        [Test]
        [Timeout(30_000)]
        public void ToMatchObjectDoesNotMatchPrototype()
        {
            Dictionary<string, object> child = Obj(("other", "child"));
            Dictionary<string, object> matcher = new Dictionary<string, object>();
            matcher["other"] = "child";
            matcher["ref"] = matcher;
            Expect(child).Not.ToMatchObject(matcher);
            Expect((Action)(() => Expect(child).ToMatchObject(matcher))).ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "to throw or not to throw")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowOrNotToThrow()
        {
            Expect((Action)(() => throw new CustomError("apple"))).ToThrow();
            Expect((Action)(() => { })).Not.ToThrow();
        }

        [PlaywrightTest("expect-builtins.spec.ts", "substring passes")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowSubstringPasses()
        {
            Expect((Action)(() => throw new CustomError("apple"))).ToThrow("apple");
            Expect((Action)(() => throw new CustomError("banana"))).Not.ToThrow("apple");
            Expect((Action)(() => { })).Not.ToThrow("apple");
        }

        [PlaywrightTest("expect-builtins.spec.ts", "substring fails when did not throw")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowSubstringFailsWhenDidNotThrow()
            => Expect((Action)(() => Expect((Action)(() => { })).ToThrow("apple"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "substring fails when message did not match")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowSubstringFailsWhenMessageDidNotMatch()
            => Expect((Action)(() => Expect((Action)(() => throw new CustomError("apple"))).ToThrow("banana"))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "regexp passes")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowRegexpPasses()
        {
            Expect((Action)(() => throw new CustomError("apple"))).ToThrow(new Regex("apple"));
            Expect((Action)(() => throw new CustomError("banana"))).Not.ToThrow(new Regex("apple"));
            Expect((Action)(() => { })).Not.ToThrow(new Regex("apple"));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "regexp fails when did not throw")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowRegexpFailsWhenDidNotThrow()
            => Expect((Action)(() => Expect((Action)(() => { })).ToThrow(new Regex("apple")))).ToThrow();

        [PlaywrightTest("expect-builtins.spec.ts", "error class passes")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowErrorClassPasses()
        {
            Expect((Action)(() => throw new Err("apple"))).ToThrow(typeof(Err));
            Expect((Action)(() => throw new Err("apple"))).ToThrow(typeof(CustomError));
            Expect((Action)(() => throw new Err("apple"))).Not.ToThrow(typeof(Err2));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "object with message passes")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowObjectWithMessagePasses()
        {
            Expect((Action)(() => throw new CustomError("apple"))).ToThrow(Obj(("message", "apple")));
            Expect((Action)(() => throw new CustomError("banana"))).Not.ToThrow(Obj(("message", "apple")));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "properly escapes strings when matching against errors")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowEscapesStrings()
            => Expect((Action)(() => throw new TypeError("\"this\"? throws."))).ToThrow("\"this\"? throws.");

        [PlaywrightTest("expect-builtins.spec.ts", "error message and cause properties")]
        [Test]
        [Timeout(30_000)]
        public void ToThrowMessageAndCause()
        {
            Exception errorCause = new Exception("cause");
            Exception error = new Exception("message", errorCause);
            Expect((Action)(() => throw error)).ToThrow(Obj(("message", "message"), ("cause", errorCause)));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "Any matches primitives")]
        [Test]
        [Timeout(30_000)]
        public void AnyMatchesPrimitives()
        {
            Expect("jest").ToEqual(Assertions.Any(typeof(string)));
            Expect(1).ToEqual(Assertions.Any(typeof(double)));
            Expect((Action)(() => { })).ToEqual(Assertions.Any(typeof(Delegate)));
            Expect(true).ToEqual(Assertions.Any(typeof(bool)));
            Expect(new Dictionary<string, object>()).ToEqual(Assertions.Any(typeof(object)));
            Expect(Array.Empty<object>()).ToEqual(Assertions.Any(typeof(Array)));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "Any throws when called with empty constructor")]
        [Test]
        [Timeout(30_000)]
        public void AnyThrowsWhenCalledWithEmptyConstructor()
        {
            Expect((Action)(() => Assertions.Any())).ToThrow(
                "any() expects to be passed a constructor function. Please pass one or use anything() to match any object.");
        }

        [PlaywrightTest("expect-builtins.spec.ts", "Anything matches any type")]
        [Test]
        [Timeout(30_000)]
        public void AnythingMatchesAnyType()
        {
            Expect("jest").ToEqual(Assertions.Anything());
            Expect(1).ToEqual(Assertions.Anything());
            Expect((Action)(() => { })).ToEqual(Assertions.Anything());
            Expect(true).ToEqual(Assertions.Anything());
            Expect(new Dictionary<string, object>()).ToEqual(Assertions.Anything());
            Expect(Array.Empty<object>()).ToEqual(Assertions.Anything());
        }

        [PlaywrightTest("expect-builtins.spec.ts", "Anything does not match null and undefined")]
        [Test]
        [Timeout(30_000)]
        public void AnythingDoesNotMatchNullAndUndefined()
        {
            Expect((object)null).Not.ToEqual(Assertions.Anything());
            Expect(Assertions.Undefined).Not.ToEqual(Assertions.Anything());
        }

        [PlaywrightTest("expect-builtins.spec.ts", "ArrayContaining matches")]
        [Test]
        [Timeout(30_000)]
        public void ArrayContainingMatches()
        {
            Expect(new object[] { "foo" }).ToEqual(Assertions.ArrayContaining(new object[] { "foo" }));
            Expect(new object[] { "foo", "bar" }).ToEqual(Assertions.ArrayContaining(new object[] { "foo" }));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "ArrayContaining does not match")]
        [Test]
        [Timeout(30_000)]
        public void ArrayContainingDoesNotMatch()
            => Expect(new object[] { "bar" }).Not.ToEqual(Assertions.ArrayContaining(new object[] { "foo" }));

        [PlaywrightTest("expect-builtins.spec.ts", "ObjectContaining matches")]
        [Test]
        [Timeout(30_000)]
        public void ObjectContainingMatches()
        {
            Expect(Obj(("foo", "foo"), ("jest", "jest"))).ToEqual(Assertions.ObjectContaining(Obj(("foo", "foo"))));
            Expect(Obj(("foo", Assertions.Undefined))).ToEqual(Assertions.ObjectContaining(Obj(("foo", Assertions.Undefined))));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "ObjectContaining does not match")]
        [Test]
        [Timeout(30_000)]
        public void ObjectContainingDoesNotMatch()
        {
            Expect(Obj(("bar", "bar"))).Not.ToEqual(Assertions.ObjectContaining(Obj(("foo", "foo"))));
            Expect(Obj(("foo", "foox"))).Not.ToEqual(Assertions.ObjectContaining(Obj(("foo", "foo"))));
            Expect(new Dictionary<string, object>()).Not.ToEqual(Assertions.ObjectContaining(Obj(("foo", Assertions.Undefined))));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "ObjectContaining throws for non-objects")]
        [Test]
        [Timeout(30_000)]
        public void ObjectContainingThrowsForNonObjects()
        {
            Expect((Action)(() => Expect(new Dictionary<string, object>()).ToEqual(Assertions.ObjectContaining(1337)))).ToThrow(
                "You must provide an object to ObjectContaining, not 'number'.");
        }

        [PlaywrightTest("expect-builtins.spec.ts", "StringContaining matches string against string")]
        [Test]
        [Timeout(30_000)]
        public void StringContainingMatches()
        {
            Expect("queen*").ToEqual(Assertions.StringContaining("en*"));
            Expect("queue").Not.ToEqual(Assertions.StringContaining("en*"));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "StringMatching matches string against regexp")]
        [Test]
        [Timeout(30_000)]
        public void StringMatchingMatchesRegexp()
        {
            Expect("queen").ToEqual(Assertions.StringMatching(new Regex("en")));
            Expect("queue").Not.ToEqual(Assertions.StringMatching(new Regex("en")));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "StringMatching matches string against string")]
        [Test]
        [Timeout(30_000)]
        public void StringMatchingMatchesString()
        {
            Expect("queen").ToEqual(Assertions.StringMatching("en"));
            Expect("queue").Not.ToEqual(Assertions.StringMatching("en"));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "closeTo matches")]
        [Test]
        [Timeout(30_000)]
        public void CloseToMatcherMatches()
        {
            Expect(0).ToEqual(Assertions.CloseTo(0));
            Expect(0.001).ToEqual(Assertions.CloseTo(0));
            Expect(1.229).ToEqual(Assertions.CloseTo(1.23));
            Expect(double.PositiveInfinity).ToEqual(Assertions.CloseTo(double.PositiveInfinity));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "closeTo does not match")]
        [Test]
        [Timeout(30_000)]
        public void CloseToMatcherDoesNotMatch()
        {
            Expect(0.01).Not.ToEqual(Assertions.CloseTo(0));
            Expect(1.23).Not.ToEqual(Assertions.CloseTo(1));
            Expect(double.PositiveInfinity).Not.ToEqual(Assertions.CloseTo(double.NegativeInfinity));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "closeTo with precision")]
        [Test]
        [Timeout(30_000)]
        public void CloseToMatcherWithPrecision()
        {
            Expect(0.1).ToEqual(Assertions.CloseTo(0, 0));
            Expect(0.0001).ToEqual(Assertions.CloseTo(0, 3));
            Expect(0.000004).ToEqual(Assertions.CloseTo(0, 5));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "closeTo throws if expected is not number")]
        [Test]
        [Timeout(30_000)]
        public void CloseToThrowsIfExpectedIsNotNumber()
            => Expect((Action)(() => Assertions.CloseTo("a"))).ToThrow("Expected is not a Number");

        [PlaywrightTest("expect-builtins.spec.ts", "arrayOf matches")]
        [Test]
        [Timeout(30_000)]
        public void ArrayOfMatches()
        {
            Expect(new object[] { 1 }).ToEqual(Assertions.ArrayOf(1));
            Expect(new object[] { 1, 1, 1 }).ToEqual(Assertions.ArrayOf(1));
            Expect(new object[] { Obj(("a", 1)), Obj(("a", 1)) }).ToEqual(Assertions.ArrayOf(Obj(("a", 1))));
            Expect(new object[] { "a", "b", "c" }).ToEqual(Assertions.ArrayOf(Assertions.Any(typeof(string))));
        }

        [PlaywrightTest("expect-builtins.spec.ts", "arrayOf does not match")]
        [Test]
        [Timeout(30_000)]
        public void ArrayOfDoesNotMatch()
        {
            Expect(new object[] { 2 }).Not.ToEqual(Assertions.ArrayOf(1));
            Expect(new object[] { 1, 2 }).Not.ToEqual(Assertions.ArrayOf(1));
            Expect("not an array").Not.ToEqual(Assertions.ArrayOf(1));
            Expect(new Dictionary<string, object>()).Not.ToEqual(Assertions.ArrayOf(1));
            Expect(new object[] { 1, 2 }).Not.ToEqual(Assertions.ArrayOf(Assertions.Any(typeof(string))));
        }

        private static Dictionary<string, object> Nest()
            => Obj(("a", Obj(("b", Obj(("c", Obj(("d", 1))))))));

        private static void HavePropertyValue(object obj, object keyPath, object value)
        {
            Expect(obj).ToHaveProperty(keyPath, value);
            Expect((Action)(() => Expect(obj).Not.ToHaveProperty(keyPath, value))).ToThrow();
        }

        private static void HavePropertyValueFail(object obj, object keyPath, object value)
        {
            Expect((Action)(() => Expect(obj).ToHaveProperty(keyPath, value))).ToThrow();
            Expect(obj).Not.ToHaveProperty(keyPath, value);
        }

        private static void HaveProperty(object obj, object keyPath)
        {
            Expect(obj).ToHaveProperty(keyPath);
            Expect((Action)(() => Expect(obj).Not.ToHaveProperty(keyPath))).ToThrow();
        }

        private static void HavePropertyFail(object obj, object keyPath)
        {
            Expect((Action)(() => Expect(obj).ToHaveProperty(keyPath))).ToThrow();
            Expect(obj).Not.ToHaveProperty(keyPath);
        }

        private static void MatchObjectPass(object a, object b)
        {
            Expect(a).ToMatchObject(b);
            Expect((Action)(() => Expect(a).Not.ToMatchObject(b))).ToThrow();
        }

        private static void MatchObjectFail(object a, object b)
        {
            Expect(a).Not.ToMatchObject(b);
            Expect((Action)(() => Expect(a).ToMatchObject(b))).ToThrow();
        }

        private sealed class Err : CustomError
        {
            public Err(string message)
                : base(message)
            {
            }
        }

        private sealed class Err2 : CustomError
        {
            public Err2(string message)
                : base(message)
            {
            }
        }

        private sealed class TypeError : Exception
        {
            public TypeError(string message)
                : base(message)
            {
            }
        }
    }
}
