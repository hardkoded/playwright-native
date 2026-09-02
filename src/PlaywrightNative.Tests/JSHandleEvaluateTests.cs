/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>jshandle-evaluate.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class JSHandleEvaluateTests : PageTestEx
    {
        [PlaywrightTest("jshandle-evaluate.spec.ts", "should work with function")]
        [PlaywrightTest("jshandle-evaluate.spec.ts", "should work with function @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithFunction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle windowHandle = await page.EvaluateHandleAsync("() => { window.foo = [1, 2]; return window; }").ConfigureAwait(false);
            int[] result = await windowHandle.EvaluateAsync<int[]>("w => w.foo").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
        }

        [PlaywrightTest("jshandle-evaluate.spec.ts", "should work with expression")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithExpression()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IJSHandle windowHandle = await page.EvaluateHandleAsync("() => { window.foo = [1, 2]; return window; }").ConfigureAwait(false);
            int[] result = await windowHandle.EvaluateAsync<int[]>("window.foo").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(new[] { 1, 2 }));
        }
    }
}
