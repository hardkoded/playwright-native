/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>locator-evaluate.spec.ts</c> coverage for <see cref="ILocator.EvaluateAsync{T}"/>
    /// and <see cref="ILocator.EvaluateAllAsync{T}"/>.
    /// </summary>
    [TestFixture]
    public class LocatorEvaluateParityTests : PageTestEx
    {
        [PlaywrightTest("locator-evaluate.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"tweet\"><div class=\"like\">100</div><div class=\"retweets\">10</div></div></body></html>").ConfigureAwait(false);
            ILocator tweet = page.Locator(".tweet .like");
            string content = await tweet.EvaluateAsync<string>("node => node.innerText").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("100"));
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "should retrieve content from subtree")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetrieveContentFromSubtree()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"><div class=\"a\">a-child-div</div></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            ILocator elementHandle = page.Locator("#myId .a");
            string content = await elementHandle.EvaluateAsync<string>("node => node.innerText").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("a-child-div"));
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "should work for all")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"tweet\"><div class=\"like\">100</div><div class=\"like\">10</div></div></body></html>").ConfigureAwait(false);
            ILocator tweet = page.Locator(".tweet .like");
            string[] content = await tweet.EvaluateAllAsync<string[]>("nodes => nodes.map(n => n.innerText)").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo(new[] { "100", "10" }));
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "should retrieve content from subtree for all")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRetrieveContentFromSubtreeForAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"><div class=\"a\">a1-child-div</div><div class=\"a\">a2-child-div</div></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            ILocator element = page.Locator("#myId .a");
            string[] content = await element.EvaluateAllAsync<string[]>("nodes => nodes.map(n => n.innerText)").ConfigureAwait(false);
            Assert.That(content, Is.EqualTo(new[] { "a1-child-div", "a2-child-div" }));
        }

        [PlaywrightTest("locator-evaluate.spec.ts", "should not throw in case of missing selector for all")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowInCaseOfMissingSelectorForAll()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string htmlContent = "<div class=\"a\">not-a-child-div</div><div id=\"myId\"></div>";
            await page.SetContentAsync(htmlContent).ConfigureAwait(false);
            ILocator element = page.Locator("#myId .a");
            int nodesLength = await element.EvaluateAllAsync<int>("nodes => nodes.length").ConfigureAwait(false);
            Assert.That(nodesLength, Is.EqualTo(0));
        }
    }
}
