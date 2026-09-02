/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.ExposeFunctionAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRExposeFunctionTests : CRTestBase
    {
        [PlaywrightTest("page-expose-function.spec.ts", "should call exposed function with no args")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCallExposedFunctionWithNoArgs()
        {
            await Page.ExposeFunctionAsync("getFortyTwo", _ => Task.FromResult<object>(42)).ConfigureAwait(false);

            int result = await Page.EvaluateAsync<int>("window.getFortyTwo()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(42));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should call exposed function with arguments")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCallExposedFunctionWithArguments()
        {
            await Page.ExposeFunctionAsync("add", args =>
            {
                int a = args[0].GetInt32();
                int b = args[1].GetInt32();
                return Task.FromResult<object>(a + b);
            }).ConfigureAwait(false);

            int result = await Page.EvaluateAsync<int>("window.add(3, 4)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(7));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should return complex objects")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnComplexObjects()
        {
            await Page.ExposeFunctionAsync("makeUser", _ =>
                Task.FromResult<object>(new { name = "Alice", age = 30 })).ConfigureAwait(false);

            JsonElement? resultJson = await Page.EvaluateAsync(
                "(async () => { const u = await window.makeUser(); return { n: u.name, a: u.age }; })()").ConfigureAwait(false);

            JsonElement result = resultJson.Value.GetProperty("value");
            Assert.That(result.GetProperty("n").GetString(), Is.EqualTo("Alice"));
            Assert.That(result.GetProperty("a").GetInt32(), Is.EqualTo(30));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should support async handler")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportAsyncHandler()
        {
            await Page.ExposeFunctionAsync("slowDouble", async args =>
            {
                await Task.Delay(50).ConfigureAwait(false);
                return (object)(args[0].GetInt32() * 2);
            }).ConfigureAwait(false);

            int result = await Page.EvaluateAsync<int>("window.slowDouble(5)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(10));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should support multiple exposed functions")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipleExposedFunctions()
        {
            await Page.ExposeFunctionAsync("one", _ => Task.FromResult<object>(1)).ConfigureAwait(false);
            await Page.ExposeFunctionAsync("two", _ => Task.FromResult<object>(2)).ConfigureAwait(false);

            int a = await Page.EvaluateAsync<int>("window.one()").ConfigureAwait(false);
            int b = await Page.EvaluateAsync<int>("window.two()").ConfigureAwait(false);

            Assert.That(a, Is.EqualTo(1));
            Assert.That(b, Is.EqualTo(2));
        }

        [PlaywrightTest("page-expose-function.spec.ts", "should survive navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveNavigation()
        {
            await Page.ExposeFunctionAsync("persistent", _ => Task.FromResult<object>("still-here")).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.persistent()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("still-here"));
        }
    }
}
