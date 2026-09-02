/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator-wait-for-function.spec.ts</c> parity for
    /// <see cref="ILocator.WaitForFunctionAsync"/>.
    /// </summary>
    [TestFixture]
    public class LocatorWaitForFunctionParityTests : PageTestEx
    {
        [PlaywrightTest("locator-wait-for-function.spec.ts", "should wait for an attribute to appear")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForAnAttributeToAppear()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button id=toggle>Menu</button>").ConfigureAwait(false);
                await page.EvaluateAsync(
                    "() => setTimeout(() => document.querySelector('#toggle').setAttribute('aria-expanded', 'true'), 1000)").ConfigureAwait(false);
                await page.Locator("#toggle").WaitForFunctionAsync("element => element.hasAttribute('aria-expanded')").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should return immediately when already truthy")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnImmediatelyWhenAlreadyTruthy()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=target>yes</div>").ConfigureAwait(false);
                await page.Locator("#target").WaitForFunctionAsync("element => element.textContent === 'yes'").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should accept ElementHandle arguments")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptElementHandleArguments()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=a></div><div id=b>value</div>").ConfigureAwait(false);
                IElementHandle handle = await page.QuerySelectorAsync("#b").ConfigureAwait(false);
                await page.Locator("#a").WaitForFunctionAsync("(element, other) => other.textContent === 'value'", handle).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should accept string expression")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptStringExpression()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=target>yes</div>").ConfigureAwait(false);
                await page.Locator("#target").WaitForFunctionAsync("element => element.textContent === 'yes'").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should resolve a promise returned by the predicate")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveAPromiseReturnedByThePredicate()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=target>yes</div>").ConfigureAwait(false);
                await page.Locator("#target").WaitForFunctionAsync("async element => element.textContent === 'yes'").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should wait for element to appear and survive rerender")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForElementToAppearAndSurviveRerender()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<span>nothing here</span>").ConfigureAwait(false);
                await page.EvaluateAsync(
                    "() => {\n" +
                    "  let count = 0;\n" +
                    "  let prev = null;\n" +
                    "  const tick = () => {\n" +
                    "    ++count;\n" +
                    "    const next = document.createElement('div');\n" +
                    "    next.id = 'target';\n" +
                    "    next.textContent = String(count);\n" +
                    "    if (prev)\n" +
                    "      prev.remove();\n" +
                    "    document.body.appendChild(next);\n" +
                    "    prev = next;\n" +
                    "    if (count < 3)\n" +
                    "      setTimeout(tick, 500);\n" +
                    "  };\n" +
                    "  setTimeout(tick, 500);\n" +
                    "}").ConfigureAwait(false);
                await page.Locator("#target").WaitForFunctionAsync("element => element.textContent === '3'").ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should throw when predicate throws")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenPredicateThrows()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=target>no</div>").ConfigureAwait(false);
                PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                    () => page.Locator("#target").WaitForFunctionAsync("() => { throw new Error('oh my'); }"));
                Assert.That(error.Message, Does.Contain("oh my"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should throw on strict mode violation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnStrictModeViolation()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div class=x>1</div><div class=x>2</div>").ConfigureAwait(false);
                PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                    () => page.Locator("div.x").WaitForFunctionAsync("() => true"));
                Assert.That(error.Message, Does.Contain("strict mode violation"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should abort via signal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortViaSignal()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=target>no</div>").ConfigureAwait(false);
                AbortController controller = new AbortController();
                Task wait = page.Locator("#target").WaitForFunctionAsync("element => element.textContent === 'yes'", options: new LocatorWaitForFunctionOptions { Arg = null, Timeout = 0 });
                await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
                Exception reason = new Exception("Aborted by user");
                controller.Abort(reason);
                Exception error = Assert.CatchAsync(() => wait);
                Assert.That(error, Is.InstanceOf<AbortError>());
                Assert.That(((AbortError)error).Cause, Is.SameAs(reason));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-wait-for-function.spec.ts", "should abort via already-aborted signal")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAbortViaAlreadyAbortedSignal()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div id=target>no</div>").ConfigureAwait(false);
                AbortController controller = new AbortController();
                controller.Abort("already aborted");
                Exception error = Assert.CatchAsync(
                    () => page.Locator("#target").WaitForFunctionAsync("() => true", new LocatorWaitForFunctionOptions { Arg = null }));
                Assert.That(error, Is.InstanceOf<AbortError>());
                Assert.That(((AbortError)error).Cause, Is.EqualTo("already aborted"));
            }).ConfigureAwait(false);
        }

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }
    }
}
