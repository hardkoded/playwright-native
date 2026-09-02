/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Dynamic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.Helpers;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-evaluate.spec.ts</c> parity for <see cref="IPage.EvaluateAsync{T}(string, object)"/>.
    /// Do not edit leftover <c>PageEvaluateTests</c>.
    /// Skipped: Node extra-arg API; Android-only length; PW_CLOCK; WebKit <c>using</c>;
    /// Chromium-only deep-chain 2 when not Chromium.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageEvaluateParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
            int basePort = 19821;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser == null)
            {
                _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            }

            try
            {
                _context = await NewContextOrRecycleAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                _context = await _browser.NewContextAsync().ConfigureAwait(false);
                _page = await _context.NewPageAsync().ConfigureAwait(false);
            }
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            if (_context != null)
            {
                await DisposeQuietlyAsync(_context).ConfigureAwait(false);
                _context = null;
                _page = null;
            }
        }

        private IPage Page => _page;

        [PlaywrightTest("page-evaluate.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            int result = await Page.EvaluateAsync<int>("() => 7 * 3").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(21));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer NaN")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferNaN()
        {
            double result = await Page.EvaluateAsync<double>("a => a", double.NaN).ConfigureAwait(false);
            Assert.That(double.IsNaN(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer -0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferNegative0()
        {
            double result = await Page.EvaluateAsync<double>("a => a", -0d).ConfigureAwait(false);
            Assert.That(IsNegativeZero(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer Infinity")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferInfinity()
        {
            double result = await Page.EvaluateAsync<double>("a => a", double.PositiveInfinity).ConfigureAwait(false);
            Assert.That(double.IsPositiveInfinity(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer -Infinity")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferNegativeInfinity()
        {
            double result = await Page.EvaluateAsync<double>("a => a", double.NegativeInfinity).ConfigureAwait(false);
            Assert.That(double.IsNegativeInfinity(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should roundtrip unserializable values")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripUnserializableValues()
        {
            var value = new Dictionary<string, double>
            {
                ["infinity"] = double.PositiveInfinity,
                ["nInfinity"] = double.NegativeInfinity,
                ["nZero"] = -0d,
                ["nan"] = double.NaN,
            };
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("value => value", value)
                .ConfigureAwait(false);
            Assert.That(double.IsPositiveInfinity(Convert.ToDouble(result["infinity"], CultureInfo.InvariantCulture)), Is.True);
            Assert.That(double.IsNegativeInfinity(Convert.ToDouble(result["nInfinity"], CultureInfo.InvariantCulture)), Is.True);
            Assert.That(IsNegativeZero(Convert.ToDouble(result["nZero"], CultureInfo.InvariantCulture)), Is.True);
            Assert.That(double.IsNaN(Convert.ToDouble(result["nan"], CultureInfo.InvariantCulture)), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should roundtrip promise to value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripPromiseToValue()
        {
            object nullResult = await Page.EvaluateAsync<object>("value => Promise.resolve(value)", JsonSerializer.Deserialize<JsonElement>("null")).ConfigureAwait(false);
            Assert.That(nullResult, Is.Null);

            double infinity = await Page.EvaluateAsync<double>("value => Promise.resolve(value)", double.PositiveInfinity).ConfigureAwait(false);
            Assert.That(double.IsPositiveInfinity(infinity), Is.True);

            double nzero = await Page.EvaluateAsync<double>("value => Promise.resolve(value)", -0d).ConfigureAwait(false);
            Assert.That(IsNegativeZero(nzero), Is.True);

            object undef = await Page.EvaluateAsync<object>("value => Promise.resolve(value)").ConfigureAwait(false);
            Assert.That(undef, Is.Null);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should roundtrip promise to unserializable values")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripPromiseToUnserializableValues()
        {
            var value = new Dictionary<string, double>
            {
                ["infinity"] = double.PositiveInfinity,
                ["nInfinity"] = double.NegativeInfinity,
                ["nZero"] = -0d,
                ["nan"] = double.NaN,
            };
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("value => Promise.resolve(value)", value)
                .ConfigureAwait(false);
            Assert.That(double.IsPositiveInfinity(Convert.ToDouble(result["infinity"], CultureInfo.InvariantCulture)), Is.True);
            Assert.That(double.IsNegativeInfinity(Convert.ToDouble(result["nInfinity"], CultureInfo.InvariantCulture)), Is.True);
            Assert.That(IsNegativeZero(Convert.ToDouble(result["nZero"], CultureInfo.InvariantCulture)), Is.True);
            Assert.That(double.IsNaN(Convert.ToDouble(result["nan"], CultureInfo.InvariantCulture)), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer arrays")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferArrays()
        {
            int[] result = await Page.EvaluateAsync<int[]>("a => a", new[] { 1, 2, 3 }).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer arrays as arrays, not objects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferArraysAsArraysNotObjects()
        {
            bool result = await Page.EvaluateAsync<bool>("a => Array.isArray(a)", new[] { 1, 2, 3 }).ConfigureAwait(false);
            Assert.That(result, Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer typed arrays")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferTypedArrays()
        {
            Assert.That(await Page.EvaluateAsync<sbyte[]>("() => new Int8Array([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new sbyte[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<byte[]>("() => new Uint8Array([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<byte[]>("() => new Uint8ClampedArray([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new byte[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<short[]>("() => new Int16Array([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new short[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<ushort[]>("() => new Uint16Array([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new ushort[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<int[]>("() => new Int32Array([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<uint[]>("() => new Uint32Array([1, 2, 3])").ConfigureAwait(false), Is.EqualTo(new uint[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<float[]>("() => new Float32Array([1.1, 2.2, 3.3])").ConfigureAwait(false), Is.EqualTo(new[] { 1.1f, 2.2f, 3.3f }).Within(0.01f));
            Assert.That(await Page.EvaluateAsync<double[]>("() => new Float64Array([1.1, 2.2, 3.3])").ConfigureAwait(false), Is.EqualTo(new[] { 1.1d, 2.2d, 3.3d }).Within(0.01d));
            Assert.That(await Page.EvaluateAsync<long[]>("() => new BigInt64Array([1n, 2n, 3n])").ConfigureAwait(false), Is.EqualTo(new long[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<ulong[]>("() => new BigUint64Array([1n, 2n, 3n])").ConfigureAwait(false), Is.EqualTo(new ulong[] { 1, 2, 3 }));

            Assert.That(await Page.EvaluateAsync<sbyte[]>("a => a", new sbyte[] { 1, 2, 3 }).ConfigureAwait(false), Is.EqualTo(new sbyte[] { 1, 2, 3 }));
            Assert.That(await Page.EvaluateAsync<byte[]>("a => a", new byte[] { 1, 2, 3 }).ConfigureAwait(false), Is.EqualTo(new byte[] { 1, 2, 3 }));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer bigint")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferBigint()
        {
            Assert.That(await Page.EvaluateAsync<BigInteger>("() => 42n").ConfigureAwait(false), Is.EqualTo(new BigInteger(42)));
            Assert.That(await Page.EvaluateAsync<BigInteger>("a => a", new BigInteger(17)).ConfigureAwait(false), Is.EqualTo(new BigInteger(17)));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer maps as empty objects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTransferMapsAsEmptyObjects()
        {
            string result = await Page.EvaluateAsync<string>(
                "a => a.x.constructor.name + ' ' + JSON.stringify(a.x)",
                new Dictionary<string, object> { ["x"] = new Dictionary<int, int> { [1] = 2 } })
                .ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Object {}"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should modify global environment")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldModifyGlobalEnvironment()
        {
            await Page.EvaluateAsync<object>("() => window['globalVar'] = 123").ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("globalVar").ConfigureAwait(false), Is.EqualTo(123));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate in the page context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateInThePageContext()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/global-var.html").ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("globalVar").ConfigureAwait(false), Is.EqualTo(123));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return undefined for objects with symbols")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnUndefinedForObjectsWithSymbols()
        {
            object[] symbols = await Page.EvaluateAsync<object[]>("() => [Symbol('foo4')]").ConfigureAwait(false);
            Assert.That(symbols, Is.EqualTo(new object[] { null }));

            IDictionary<string, object> empty = await Page.EvaluateAsync<ExpandoObject>(@"() => {
    const a = {};
    a[Symbol('foo4')] = 42;
    return a;
  }").ConfigureAwait(false);
            Assert.That(empty.Keys, Is.Empty);

            IDictionary<string, object> nested = await Page.EvaluateAsync<ExpandoObject>(@"() => {
    return { foo: [{ a: Symbol('foo4') }] };
  }").ConfigureAwait(false);
            object[] foo = (object[])nested["foo"];
            IDictionary<string, object> inner = (IDictionary<string, object>)foo[0];
            Assert.That(inner.ContainsKey("a"), Is.True);
            Assert.That(inner["a"], Is.Null);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with function shorthands")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithFunctionShorthands()
        {
            Assert.That(await Page.EvaluateAsync<int>("([a, b]) => a + b", new[] { 1, 2 }).ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await Page.EvaluateAsync<int>("async ([a, b]) => a * b", new[] { 2, 4 }).ConfigureAwait(false), Is.EqualTo(8));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with unicode chars")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithUnicodeChars()
        {
            int result = await Page.EvaluateAsync<int>("a => a['中文字符']", new Dictionary<string, int> { ["中文字符"] = 42 }).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with large strings")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLargeStrings()
        {
            string expected = new string('x', 40000);
            Assert.That(await Page.EvaluateAsync<string>("data => data", expected).ConfigureAwait(false), Is.EqualTo(expected));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with large unicode strings")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLargeUnicodeStrings()
        {
            string expected = string.Concat(Enumerable.Repeat("🎭", 10000));
            Assert.That(await Page.EvaluateAsync<string>("data => data", expected).ConfigureAwait(false), Is.EqualTo(expected));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw when evaluation triggers reload")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenEvaluationTriggersReload()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(@"() => {
    location.reload();
    return new Promise(() => { });
  }"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("navigation"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should await promise")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitPromise()
        {
            int result = await Page.EvaluateAsync<int>("() => Promise.resolve(8 * 7)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(56));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work right after framenavigated")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkRightAfterFramenavigated()
        {
            EnsureServer();
            Task<int> frameEvaluation = null;
            Page.FrameNavigated += (_, frame) =>
            {
                frameEvaluation = frame.EvaluateAsync<int>("() => 6 * 7");
            };
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await frameEvaluation.ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work right after a cross-origin navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkRightAfterACrossOriginNavigation()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<int> frameEvaluation = null;
            Page.FrameNavigated += (_, frame) =>
            {
                frameEvaluation = frame.EvaluateAsync<int>("() => 6 * 7");
            };
            await Page.GoToAsync(CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            Assert.That(await frameEvaluation.ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work from-inside an exposed function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkFromInsideAnExposedFunction()
        {
            IPage page = Page;
            await page.ExposeFunctionAsync("callController", async (int a, int b) =>
            {
                return await page.EvaluateAsync<int>("({ a, b }) => a * b", new { a, b }).ConfigureAwait(false);
            }).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>(@"async function() {
    return await window['callController'](9, 3);
  }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(27));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should reject promise with exception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRejectPromiseWithException()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("() => not_existing_object.property"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("not_existing_object"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should support thrown strings as error messages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldSupportThrownStringsAsErrorMessages()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("() => { throw 'qwerty'; }"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("qwerty"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should support thrown numbers as error messages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldSupportThrownNumbersAsErrorMessages()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("() => { throw 100500; }"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("100500"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return complex objects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnComplexObjects()
        {
            var obj = new Dictionary<string, string> { ["foo"] = "bar!" };
            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>("a => a", obj).ConfigureAwait(false);
            Assert.That(ReferenceEquals(result, obj), Is.False);
            Assert.That(Convert.ToString(result["foo"], CultureInfo.InvariantCulture), Is.EqualTo("bar!"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return NaN")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnNaN()
        {
            double result = await Page.EvaluateAsync<double>("() => NaN").ConfigureAwait(false);
            Assert.That(double.IsNaN(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return -0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnNegative0()
        {
            double result = await Page.EvaluateAsync<double>("() => -0").ConfigureAwait(false);
            Assert.That(IsNegativeZero(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return Infinity")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnInfinity()
        {
            double result = await Page.EvaluateAsync<double>("() => Infinity").ConfigureAwait(false);
            Assert.That(double.IsPositiveInfinity(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return -Infinity")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnNegativeInfinity()
        {
            double result = await Page.EvaluateAsync<double>("() => -Infinity").ConfigureAwait(false);
            Assert.That(double.IsNegativeInfinity(result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with overwritten Promise")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithOverwrittenPromise()
        {
            await Page.EvaluateAsync<object>(@"() => {
    const originalPromise = window.Promise;
    class Promise2 {
      constructor(f) {
        this._promise = new originalPromise(f);
      }
      static all(arg) { return wrap(originalPromise.all(arg)); }
      static race(arg) { return wrap(originalPromise.race(arg)); }
      static resolve(arg) { return wrap(originalPromise.resolve(arg)); }
      then(f, r) { return wrap(this._promise.then(f, r)); }
      catch(f) { return wrap(this._promise.catch(f)); }
      finally(f) { return wrap(this._promise.finally(f)); }
    }
    const wrap = p => {
      const result = new Promise2(() => { });
      result._promise = p;
      return result;
    };
    window.Promise = Promise2;
    window['__Promise2'] = Promise2;
  }").ConfigureAwait(false);

            Assert.That(await Page.EvaluateAsync<bool>(@"() => {
    const p = Promise.all([Promise.race([]), new Promise(() => { }).then(() => { })]);
    return p instanceof window['__Promise2'];
  }").ConfigureAwait(false), Is.True);
            Assert.That(await Page.EvaluateAsync<int>("() => Promise.resolve(42)").ConfigureAwait(false), Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw when passed more than one parameter")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenPassedMoreThanOneParameter()
        {
            Assert.Ignore("C# evaluate API accepts a single argument (same as playwright-dotnet).");
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should accept \"undefined\" as one of multiple parameters")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptUndefinedAsOneOfMultipleParameters()
        {
            bool result = await Page.EvaluateAsync<bool>(
                "({ a, b }) => Object.is(a, undefined) && Object.is(b, 'foo')",
                new Dictionary<string, object> { ["a"] = EvaluateSerialization.Undefined, ["b"] = "foo" })
                .ConfigureAwait(false);
            Assert.That(result, Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should properly serialize undefined arguments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlySerializeUndefinedArguments()
        {
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("x => ({ a: x })")
                .ConfigureAwait(false);
            Assert.That(DefinedEntries(result), Is.Empty);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should properly serialize undefined fields")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlySerializeUndefinedFields()
        {
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("() => ({ a: undefined })")
                .ConfigureAwait(false);
            Assert.That(DefinedEntries(result), Is.Empty);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return undefined properties")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnUndefinedProperties()
        {
            IDictionary<string, object> value = await Page
                .EvaluateAsync<ExpandoObject>("() => ({ a: undefined })")
                .ConfigureAwait(false);
            Assert.That(value.ContainsKey("a"), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should properly serialize null arguments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlySerializeNullArguments()
        {
            object result = await Page.EvaluateAsync<object>("x => x", JsonSerializer.Deserialize<JsonElement>("null")).ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should properly serialize null fields")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlySerializeNullFields()
        {
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("() => ({ a: null })")
                .ConfigureAwait(false);
            Assert.That(result.ContainsKey("a"), Is.True);
            Assert.That(result["a"], Is.Null);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should properly serialize PerformanceMeasure object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlySerializePerformanceMeasureObject()
        {
            object[] entries = await Page.EvaluateAsync<object[]>(@"() => {
    const performance = (window.builtins && window.builtins.performance) || window.performance;
    performance.mark('start');
    performance.mark('end');
    performance.measure('my-measure', 'start', 'end');
    return performance.getEntriesByType('measure');
  }").ConfigureAwait(false);
            Assert.That(entries, Is.Not.Empty);
            IDictionary<string, object> entry = (IDictionary<string, object>)entries[0];
            Assert.That(Convert.ToString(entry["entryType"], CultureInfo.InvariantCulture), Is.EqualTo("measure"));
            Assert.That(Convert.ToString(entry["name"], CultureInfo.InvariantCulture), Is.EqualTo("my-measure"));
            Assert.That(entry["duration"], Is.InstanceOf<IConvertible>());
            Assert.That(entry["startTime"], Is.InstanceOf<IConvertible>());
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should properly serialize window.performance object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldProperlySerializeWindowPerformanceObject()
        {
            if (!string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PW_CLOCK")))
            {
                Assert.Ignore("PW_CLOCK");
            }

            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>("() => performance").ConfigureAwait(false);
            Assert.That(result.ContainsKey("timeOrigin"), Is.True);
            Assert.That(result.ContainsKey("navigation"), Is.True);
            Assert.That(result.ContainsKey("timing"), Is.True);
            IDictionary<string, object> navigation = (IDictionary<string, object>)result["navigation"];
            Assert.That(navigation.ContainsKey("redirectCount"), Is.True);
            Assert.That(navigation.ContainsKey("type"), Is.True);
            IDictionary<string, object> timing = (IDictionary<string, object>)result["timing"];
            foreach (string key in new[]
            {
                "connectEnd", "connectStart", "domComplete", "domContentLoadedEventEnd",
                "domContentLoadedEventStart", "domInteractive", "domLoading", "domainLookupEnd",
                "domainLookupStart", "fetchStart", "loadEventEnd", "loadEventStart", "navigationStart",
                "redirectEnd", "redirectStart", "requestStart", "responseEnd", "responseStart",
                "secureConnectionStart", "unloadEventEnd", "unloadEventStart",
            })
            {
                Assert.That(timing.ContainsKey(key), Is.True, key);
            }
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should return undefined for non-serializable objects")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnUndefinedForNonSerializableObjects()
        {
            object result = await Page.EvaluateAsync<object>("() => function() {}").ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw for too deep reference chain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowForTooDeepReferenceChain()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(@"depth => {
    const obj = {};
    let temp = obj;
    for (let i = 0; i < depth; i++) {
      temp[i] = {};
      temp = temp[i];
    }
    return obj;
  }", 1000));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Cannot serialize result: object reference chain is too long."));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw for too deep reference chain 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowForTooDeepReferenceChain2()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("this is a chromium-only limitation");
            }

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(@"depth => {
    let node = {};
    for (let i = 0; i < depth; i++)
      node = { child: node };
    return node;
  }", 200));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Cannot serialize result: object reference chain is too long."));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw usable message for unserializable shallow function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowUsableMessageForUnserializableShallowFunction()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("arg => arg", (Action)(() => { })));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match(@"Attempting to serialize unexpected value: \(\) => \{\}"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw usable message for unserializable object one deep function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowUsableMessageForUnserializableObjectOneDeepFunction()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("arg => arg", new Dictionary<string, object> { ["aProperty"] = (Action)(() => { }) }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match(@"Attempting to serialize unexpected value at position ""aProperty"": \(\) => \{\}"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw usable message for unserializable object nested function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowUsableMessageForUnserializableObjectNestedFunction()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(
                    "arg => arg",
                    new Dictionary<string, object>
                    {
                        ["a"] = new Dictionary<string, object>
                        {
                            ["inner"] = new Dictionary<string, object> { ["property"] = (Action)(() => { }) },
                        },
                    }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match(@"Attempting to serialize unexpected value at position ""a\.inner\.property"": \(\) => \{\}"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw usable message for unserializable array nested function")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowUsableMessageForUnserializableArrayNestedFunction()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(
                    "arg => arg",
                    new Dictionary<string, object>
                    {
                        ["a"] = new Dictionary<string, object>
                        {
                            ["inner"] = new object[] { "firstValue", new Dictionary<string, object> { ["property"] = (Action)(() => { }) } },
                        },
                    }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match(@"Attempting to serialize unexpected value at position ""a\.inner\[1\]\.property"": \(\) => \{\}"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should alias Window, Document and Node")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAliasWindowDocumentAndNode()
        {
            object[] result = await Page.EvaluateAsync<object[]>("[window, document, document.body]").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new object[] { "ref: <Window>", "ref: <Document>", "ref: <Node>" }));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work for circular object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForCircularObject()
        {
            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>(@"() => {
    const a = {};
    a.b = a;
    return a;
  }").ConfigureAwait(false);
            Assert.That(ReferenceEquals(result["b"], result), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should be able to throw a tricky error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeAbleToThrowATrickyError()
        {
            IJSHandle windowHandle = await Page.EvaluateHandleAsync("() => window").ConfigureAwait(false);
            string errorText;
            try
            {
                await windowHandle.JsonValueAsync<object>().ConfigureAwait(false);
                errorText = "ref: <Window>";
            }
            catch (Exception ex)
            {
                errorText = ex.Message;
            }

            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("errorText => { throw new Error(errorText); }", errorText));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(errorText));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should accept a string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptAString()
        {
            Assert.That(await Page.EvaluateAsync<int>("1 + 2").ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should accept a string with semi colons")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptAStringWithSemiColons()
        {
            Assert.That(await Page.EvaluateAsync<int>("1 + 5;").ConfigureAwait(false), Is.EqualTo(6));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should accept a string with comments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptAStringWithComments()
        {
            Assert.That(await Page.EvaluateAsync<int>("2 + 5;\n// do some math!").ConfigureAwait(false), Is.EqualTo(7));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should accept element handle as an argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptElementHandleAsAnArgument()
        {
            await Page.SetContentAsync("<section>42</section>").ConfigureAwait(false);
            IElementHandle element = await Page.QuerySelectorAsync("section").ConfigureAwait(false);
            string text = await Page.EvaluateAsync<string>("e => e.textContent", element).ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("42"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw if underlying element was disposed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowIfUnderlyingElementWasDisposed()
        {
            await Page.SetContentAsync("<section>39</section>").ConfigureAwait(false);
            IElementHandle element = await Page.QuerySelectorAsync("section").ConfigureAwait(false);
            Assert.That(element, Is.Not.Null);
            await element.DisposeAsync().ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<string>("e => e.textContent", element));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("no object with guid"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should simulate a user gesture")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSimulateAUserGesture()
        {
            bool result = await Page.EvaluateAsync<bool>(@"() => {
    document.body.appendChild(document.createTextNode('test'));
    document.execCommand('selectAll');
    return document.execCommand('copy');
  }").ConfigureAwait(false);
            Assert.That(result, Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw a nice error after a navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowANiceErrorAfterANavigation()
        {
            Task<object> errorTask = Page.EvaluateAsync<object>("() => new Promise(f => window['__resolve'] = f)");
            await Task.WhenAll(
                Page.WaitForNavigationAsync(),
                Page.EvaluateAsync<object>(@"() => {
    window.location.reload();
    setTimeout(() => window['__resolve'](42), 1000);
  }")).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(async () => await errorTask.ConfigureAwait(false));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("navigation"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not throw an error when evaluation does a navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowAnErrorWhenEvaluationDoesANavigation()
        {
            EnsureServer();
            await Page.GoToAsync(Prefix + "/one-style.html").ConfigureAwait(false);
            int[] result = await Page.EvaluateAsync<int[]>(@"() => {
    window.location.href = '/empty.html';
    return [42];
  }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 42 }));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not throw an error when evaluation does a synchronous navigation and returns an object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowAnErrorWhenEvaluationDoesASynchronousNavigationAndReturnsAnObject()
        {
            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>(@"() => {
    window.location.reload();
    return { a: 42 };
  }").ConfigureAwait(false);
            Assert.That(Convert.ToInt32(result["a"], CultureInfo.InvariantCulture), Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not throw an error when evaluation does a synchronous navigation and returns undefined")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowAnErrorWhenEvaluationDoesASynchronousNavigationAndReturnsUndefined()
        {
            object result = await Page.EvaluateAsync<object>(@"() => {
    window.location.reload();
    return undefined;
  }").ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should transfer 100Mb of data from page to node.js")]
        [Test]
        [Timeout(120_000)]
        public async Task ShouldTransfer100MbOfDataFromPageToNodeJs()
        {
            string a = await Page.EvaluateAsync<string>("() => Array(100 * 1024 * 1024 + 1).join('a')").ConfigureAwait(false);
            Assert.That(a.Length, Is.EqualTo(100 * 1024 * 1024));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw error with detailed information on exception inside promise ")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowErrorWithDetailedInformationOnExceptionInsidePromise()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(@"() => new Promise(() => {
    throw new Error('Error in promise');
  })"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Error in promise"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work even when JSON is set to null")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkEvenWhenJsonIsSetToNull()
        {
            await Page.EvaluateAsync<object>("() => { window.JSON.stringify = null; window.JSON = null; }").ConfigureAwait(false);
            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>("() => ({ abc: 123 })").ConfigureAwait(false);
            Assert.That(Convert.ToInt32(result["abc"], CultureInfo.InvariantCulture), Is.EqualTo(123));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should await promise from popup")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitPromiseFromPopup()
        {
            EnsureServer();
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            int result = await Page.EvaluateAsync<int>(@"() => {
    const win = window.open('about:blank');
    return new win['Promise'](f => f(42));
  }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with new Function() and CSP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNewFunctionAndCsp()
        {
            EnsureServer();
            Server.SetCSP("/csp-function.html", "script-src " + Prefix);
            await Page.GoToAsync(Prefix + "/csp-function.html").ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<bool>("() => new Function('return true')()").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with non-strict expressions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithNonStrictExpressions()
        {
            double result = await Page.EvaluateAsync<double>(@"() => {
    y = 3.14;
    return y;
  }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(3.14).Within(0.0001));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should respect use strict expression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRespectUseStrictExpression()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>(@"() => {
    'use strict';
    variableY = 3.14;
    return variableY;
  }"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("variableY"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not leak utility script")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotLeakUtilityScript()
        {
            Assert.That(await Page.EvaluateAsync<bool>("this === window").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not leak handles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotLeakHandles()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("handles.length"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain(" handles"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with CSP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithCsp()
        {
            EnsureServer();
            Server.SetCSP("/empty.html", "script-src 'self'");
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("() => 2 + 2").ConfigureAwait(false), Is.EqualTo(4));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate exception with a function on the stack")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateExceptionWithAFunctionOnTheStack()
        {
            JavaScriptEvalError error = await Page.EvaluateAsync<JavaScriptEvalError>(@"() => {
    return (function functionOnStack() {
      return new Error('error message');
    })();
  }").ConfigureAwait(false);
            Assert.That(error.Message, Is.EqualTo("error message"));
            Assert.That(error.Stack, Does.Contain("functionOnStack"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate exception")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateException()
        {
            object error = await Page.EvaluateAsync<object>(@"() => {
    function innerFunction() {
      const e = new Error('error message');
      e.name = 'foobar';
      return e;
    }
    return innerFunction();
  }").ConfigureAwait(false);
            Assert.That(error, Is.InstanceOf<JavaScriptEvalError>());
            JavaScriptEvalError js = (JavaScriptEvalError)error;
            Assert.That(js.Message, Is.EqualTo("error message"));
            Assert.That(js.Name, Is.EqualTo("foobar"));
            Assert.That(js.Stack, Does.Contain("innerFunction"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should pass exception argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPassExceptionArgument()
        {
            JavaScriptEvalError thrown = new JavaScriptEvalError
            {
                Name = "foobar",
                Message = "error message",
                Stack = "foobar: error message\n    at innerFunction",
            };
            IDictionary<string, object> received = await Page.EvaluateAsync<ExpandoObject>(
                "e => ({ message: e.message, name: e.name, stack: e.stack })",
                thrown).ConfigureAwait(false);
            Assert.That(Convert.ToString(received["message"], CultureInfo.InvariantCulture), Is.EqualTo("error message"));
            Assert.That(Convert.ToString(received["name"], CultureInfo.InvariantCulture), Is.EqualTo("foobar"));
            Assert.That(Convert.ToString(received["stack"], CultureInfo.InvariantCulture), Does.Contain("innerFunction"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate date")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateDate()
        {
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("() => ({ date: new Date('2020-05-27T01:31:38.506Z') })")
                .ConfigureAwait(false);
            Assert.That(result["date"], Is.EqualTo(DateTime.Parse("2020-05-27T01:31:38.506Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should roundtrip date")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripDate()
        {
            DateTime date = DateTime.Parse("2020-05-27T01:31:38.506Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);
            DateTime result = await Page.EvaluateAsync<DateTime>("date => date", date).ConfigureAwait(false);
            Assert.That(result.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture), Is.EqualTo(date.ToUniversalTime().ToString("r", CultureInfo.InvariantCulture)));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should roundtrip regex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripRegex()
        {
            Regex regex = new Regex("hello", RegexOptions.IgnoreCase | RegexOptions.Multiline);
            Regex result = await Page.EvaluateAsync<Regex>("regex => regex", regex).ConfigureAwait(false);
            Assert.That(result.ToString(), Is.EqualTo(regex.ToString()));
            Assert.That((result.Options & RegexOptions.IgnoreCase) != 0, Is.True);
            Assert.That((result.Options & RegexOptions.Multiline) != 0, Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should jsonValue() date")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldJsonValueDate()
        {
            IJSHandle handle = await Page.EvaluateHandleAsync("() => ({ date: new Date('2020-05-27T01:31:38.506Z') })").ConfigureAwait(false);
            IDictionary<string, object> result = await handle.JsonValueAsync<ExpandoObject>().ConfigureAwait(false);
            Assert.That(result["date"], Is.EqualTo(DateTime.Parse("2020-05-27T01:31:38.506Z", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateUrl()
        {
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("() => ({ url: new URL('https://example.com') })")
                .ConfigureAwait(false);
            Assert.That(result["url"], Is.EqualTo(new Uri("https://example.com")));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should roundtrip url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRoundtripUrl()
        {
            Uri url = new Uri("https://example.com");
            Uri result = await Page.EvaluateAsync<Uri>("url => url", url).ConfigureAwait(false);
            Assert.That(result.ToString(), Is.EqualTo(url.ToString()));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should jsonValue() url")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldJsonValueUrl()
        {
            IJSHandle handle = await Page.EvaluateHandleAsync("() => ({ url: new URL('https://example.com') })").ConfigureAwait(false);
            IDictionary<string, object> result = await handle.JsonValueAsync<ExpandoObject>().ConfigureAwait(false);
            Assert.That(result["url"], Is.EqualTo(new Uri("https://example.com")));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not use toJSON when evaluating")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotUseToJsonWhenEvaluating()
        {
            IDictionary<string, object> result = await Page
                .EvaluateAsync<ExpandoObject>("() => ({ toJSON: () => 'string', data: 'data' })")
                .ConfigureAwait(false);
            Assert.That(Convert.ToString(result["data"], CultureInfo.InvariantCulture), Is.EqualTo("data"));
            Assert.That(result["toJSON"], Is.InstanceOf<IDictionary<string, object>>());
            Assert.That(((IDictionary<string, object>)result["toJSON"]).Count, Is.EqualTo(0));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not use Array.prototype.toJSON when evaluating")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotUseArrayPrototypeToJsonWhenEvaluating()
        {
            int[] result = await Page.EvaluateAsync<int[]>(@"() => {
    Array.prototype.toJSON = () => 'busted';
    return [1, 2, 3];
  }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 1, 2, 3 }));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not add a toJSON property to newly created Arrays after evaluation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotAddAToJsonPropertyToNewlyCreatedArraysAfterEvaluation()
        {
            await Page.EvaluateAsync<object>("() => []").ConfigureAwait(false);
            bool hasToJson = await Page.EvaluateAsync<bool>("() => 'toJSON' in []").ConfigureAwait(false);
            Assert.That(hasToJson, Is.False);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not use toJSON in jsonValue")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotUseToJsonInJsonValue()
        {
            IJSHandle handle = await Page.EvaluateHandleAsync("() => ({ toJSON: () => 'string', data: 'data' })").ConfigureAwait(false);
            IDictionary<string, object> result = await handle.JsonValueAsync<ExpandoObject>().ConfigureAwait(false);
            Assert.That(Convert.ToString(result["data"], CultureInfo.InvariantCulture), Is.EqualTo("data"));
            Assert.That(result["toJSON"], Is.InstanceOf<IDictionary<string, object>>());
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should ignore buggy toJSON")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreBuggyToJson()
        {
            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>(@"() => {
    class Foo {
      toJSON() { throw new Error('Bad'); }
    }
    class Bar {
      get toJSON() { throw new Error('Also bad'); }
    }
    return { foo: new Foo(), bar: new Bar() };
  }").ConfigureAwait(false);
            Assert.That(((IDictionary<string, object>)result["foo"]).Count, Is.EqualTo(0));
            Assert.That(((IDictionary<string, object>)result["bar"]).Count, Is.EqualTo(0));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should not expose the injected script export")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotExposeTheInjectedScriptExport()
        {
            Assert.That(await Page.EvaluateAsync<bool>("typeof pwExport === \"undefined\"").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw when frame is detached")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenFrameIsDetached()
        {
            EnsureServer();
            await AttachFrameAsync(Page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = Page.Frames.ElementAt(1);
            Task<object> promise = frame.EvaluateAsync<object>("() => new Promise(() => {})");
            await DetachFrameAsync(Page, "frame1").ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(async () => await promise.ConfigureAwait(false));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Match(@"frame\.evaluate: (Frame was detached|Execution context was destroyed)"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with overridden Object.defineProperty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithOverriddenObjectDefineProperty()
        {
            EnsureServer();
            Server.SetRoute("/test", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(@"<script>
    Object.create = null;
    Object.defineProperty = null;
    Object.getOwnPropertyDescriptor = null;
    Object.getOwnPropertyNames = null;
    Object.getPrototypeOf = null;
    Object.prototype.hasOwnProperty = null;
    </script>").ConfigureAwait(false);
            });
            await Page.GoToAsync(Prefix + "/test").ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("1+2").ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with busted Array.prototype.map/push")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithBustedArrayPrototypeMapPush()
        {
            EnsureServer();
            Server.SetRoute("/test-busted-array", async context =>
            {
                context.Response.ContentType = "text/html";
                await context.Response.WriteAsync(@"<script>
      Array.prototype.map = null;
      Array.prototype.push = null;
    </script>").ConfigureAwait(false);
            });
            await Page.GoToAsync(Prefix + "/test-busted-array").ConfigureAwait(false);
            Assert.That(await Page.EvaluateAsync<int>("1+2").ConfigureAwait(false), Is.EqualTo(3));
            IJSHandle handle = await Page.EvaluateHandleAsync("1+2").ConfigureAwait(false);
            Assert.That(await handle.JsonValueAsync<int>().ConfigureAwait(false), Is.EqualTo(3));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with overridden globalThis.Window/Document/Node")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithOverriddenGlobalThisWindowDocumentNode()
        {
            EnsureServer();
            string[] cases =
            {
                "() => globalThis.Window = {}",
                "() => globalThis.Document = {}",
                "() => globalThis.Node = {}",
                "() => globalThis.Window = null",
                "() => globalThis.Document = null",
                "() => globalThis.Node = null",
            };
            foreach (string testCase in cases)
            {
                await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await Page.EvaluateAsync<object>(testCase).ConfigureAwait(false);
                Assert.That(await Page.EvaluateAsync<int>("1+2").ConfigureAwait(false), Is.EqualTo(3), testCase);
                Assert.That(await Page.EvaluateAsync<string[]>("() => ['foo']").ConfigureAwait(false), Is.EqualTo(new[] { "foo" }), testCase);
            }
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with overridden URL/Date/RegExp")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithOverriddenUrlDateRegExp()
        {
            EnsureServer();
            string[] cases =
            {
                "() => globalThis.URL = 'foo'",
                "() => globalThis.RegExp = 'foo'",
                "() => globalThis.Date = 'foo'",
            };
            foreach (string testCase in cases)
            {
                await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
                await Page.EvaluateAsync<object>(testCase).ConfigureAwait(false);
                Assert.That(await Page.EvaluateAsync<int>("1+2").ConfigureAwait(false), Is.EqualTo(3), testCase);
                IDictionary<string, object> obj = await Page.EvaluateAsync<ExpandoObject>("() => ({ 'a': 2023 })").ConfigureAwait(false);
                Assert.That(Convert.ToInt32(obj["a"], CultureInfo.InvariantCulture), Is.EqualTo(2023), testCase);
            }
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with Array.from/map")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithArrayFromMap()
        {
            string result = await Page.EvaluateAsync<string>(@"() => {
    const r = (str, amount) => Array.from(Array(amount)).map(() => str).join('');
    return r('([a-f0-9]{2})', 3);
  }").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("([a-f0-9]{2})([a-f0-9]{2})([a-f0-9]{2})"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should work with a using declaration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithAUsingDeclaration()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit does not support using declarations");
            }

            bool disposed = await Page.EvaluateAsync<bool>(@"() => {
    let disposed = false;
    {
      using r = { [Symbol.dispose]: () => { disposed = true; } };
      void r;
    }
    return disposed;
  }").ConfigureAwait(false);
            Assert.That(disposed, Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should ignore dangerous object keys")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreDangerousObjectKeys()
        {
            var input = new Dictionary<string, object>
            {
                ["__proto__"] = new Dictionary<string, object> { ["polluted"] = true },
                ["safeKey"] = "safeValue",
            };
            IDictionary<string, object> result = await Page.EvaluateAsync<ExpandoObject>("arg => arg", input).ConfigureAwait(false);
            Assert.That(result.ContainsKey("safeKey"), Is.True);
            Assert.That(Convert.ToString(result["safeKey"], CultureInfo.InvariantCulture), Is.EqualTo("safeValue"));
            Assert.That(result.ContainsKey("__proto__"), Is.False);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static IEnumerable<KeyValuePair<string, object>> DefinedEntries(IDictionary<string, object> result)
            => result.Where(pair => pair.Value != null);

        private static bool IsNegativeZero(double value)
            => value == 0d && BitConverter.DoubleToInt64Bits(value) < 0;

        private async Task<IBrowserContext> NewContextOrRecycleAsync()
        {
            Task<IBrowserContext> create = _browser.NewContextAsync();
            Task finished = await Task.WhenAny(create, Task.Delay(5000)).ConfigureAwait(false);
            if (!ReferenceEquals(finished, create))
            {
                await RecycleBrowserAsync().ConfigureAwait(false);
                return await _browser.NewContextAsync().ConfigureAwait(false);
            }

            return await create.ConfigureAwait(false);
        }

        private async Task RecycleBrowserAsync()
        {
            IBrowser previous = _browser;
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            if (previous != null)
            {
                await DisposeQuietlyAsync(previous).ConfigureAwait(false);
            }
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.createElement('iframe'); f.name = " +
                nameJson + "; f.id = " + nameJson + "; f.src = " + urlJson +
                "; document.body.appendChild(f); })()").ConfigureAwait(false);
            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named != null && !named.IsDetached)
                {
                    return named;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for frame " + name);
            return null;
        }

        private static async Task DetachFrameAsync(IPage page, string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            await page.EvaluateAsync<object>(
                "(() => { const f = document.querySelector('iframe[name=' + JSON.stringify(" +
                nameJson + ") + ']'); if (f) f.remove(); })()").ConfigureAwait(false);
        }
    }
}
