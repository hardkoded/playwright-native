/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>selectors-register.spec.ts</c> parity for custom engines
    /// and atomic selector reads. Do not edit leftover
    /// <c>SelectorsRegisterTests.cs</c>.
    /// </summary>
    [TestFixture]
    public class SelectorsRegisterParityTests : PageTestEx
    {
        private const string MutateTextEngine = @"{
  query(root, selector) {
    const result = root.querySelector(selector);
    if (result)
      void Promise.resolve().then(() => result.textContent = 'modified');
    return result;
  },
  queryAll(root, selector) {
    const result = Array.from(root.querySelectorAll(selector));
    for (const e of result)
      void Promise.resolve().then(() => e.textContent = 'modified');
    return result;
  }
}";

        private const string MutateAttributeEngine = @"{
  query(root, selector) {
    const result = root.querySelector(selector);
    if (result)
      void Promise.resolve().then(() => result.setAttribute('foo', 'modified'));
    return result;
  },
  queryAll(root, selector) {
    const result = Array.from(root.querySelectorAll(selector));
    for (const e of result)
      void Promise.resolve().then(() => e.setAttribute('foo', 'modified'));
    return result;
  }
}";

        private const string MutateDisplayEngine = @"{
  query(root, selector) {
    const result = root.querySelector(selector);
    if (result)
      void Promise.resolve().then(() => result.style.display = 'none');
    return result;
  },
  queryAll(root, selector) {
    const result = Array.from(root.querySelectorAll(selector));
    for (const e of result)
      void Promise.resolve().then(() => e.style.display = 'none');
    return result;
  }
}";

        private const string ObjectLiteralEngine = @"{
    query(root, selector) {
      return root.querySelector(selector);
    },
    queryAll(root, selector) {
      return root.querySelectorAll(selector);
    }
  }";

        private static async Task RegisterEngineAsync(string name, string script)
        {
            try
            {
                await Playwright.Selectors.RegisterAsync(name, script).ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex)
                when (ex.Message.IndexOf("already registered", StringComparison.Ordinal) >= 0)
            {
            }
        }

        [PlaywrightTest("selectors-register.spec.ts", "textContent should be atomic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TextContentShouldBeAtomic()
        {
            await RegisterEngineAsync("textContent", MutateTextEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>Hello</div>").ConfigureAwait(false);
            string tc = await page.TextContentAsync("textContent=div").ConfigureAwait(false);
            Assert.That(tc, Is.EqualTo("Hello"));
            Assert.That(
                await page.EvaluateAsync<string>("() => document.querySelector('div').textContent").ConfigureAwait(false),
                Is.EqualTo("modified"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "innerText should be atomic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InnerTextShouldBeAtomic()
        {
            await RegisterEngineAsync("innerText", MutateTextEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>Hello</div>").ConfigureAwait(false);
            string tc = await page.InnerTextAsync("innerText=div").ConfigureAwait(false);
            Assert.That(tc, Is.EqualTo("Hello"));
            Assert.That(
                await page.EvaluateAsync<string>("() => document.querySelector('div').innerText").ConfigureAwait(false),
                Is.EqualTo("modified"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "innerHTML should be atomic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InnerHTMLShouldBeAtomic()
        {
            await RegisterEngineAsync("innerHTML", MutateTextEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>Hello<span>world</span></div>").ConfigureAwait(false);
            string tc = await page.InnerHTMLAsync("innerHTML=div").ConfigureAwait(false);
            Assert.That(tc, Is.EqualTo("Hello<span>world</span>"));
            Assert.That(
                await page.EvaluateAsync<string>("() => document.querySelector('div').innerHTML").ConfigureAwait(false),
                Is.EqualTo("modified"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "getAttribute should be atomic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task GetAttributeShouldBeAtomic()
        {
            await RegisterEngineAsync("getAttribute", MutateAttributeEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div foo=hello></div>").ConfigureAwait(false);
            string tc = await page.GetAttributeAsync("getAttribute=div", "foo").ConfigureAwait(false);
            Assert.That(tc, Is.EqualTo("hello"));
            Assert.That(
                await page.EvaluateAsync<string>("() => document.querySelector('div').getAttribute('foo')").ConfigureAwait(false),
                Is.EqualTo("modified"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "isVisible should be atomic")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IsVisibleShouldBeAtomic()
        {
            await RegisterEngineAsync("isVisible", MutateDisplayEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>Hello</div>").ConfigureAwait(false);
            bool result = await page.IsVisibleAsync("isVisible=div").ConfigureAwait(false);
            Assert.That(result, Is.True);
            Assert.That(
                await page.EvaluateAsync<string>("() => document.querySelector('div').style.display").ConfigureAwait(false),
                Is.EqualTo("none"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "should take java-style string")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTakeJavaStyleString()
        {
            await RegisterEngineAsync("objectLiteral", ObjectLiteralEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>Hello</div>").ConfigureAwait(false);
            await page.TextContentAsync("objectLiteral=div").ConfigureAwait(false);
        }
    }
}
