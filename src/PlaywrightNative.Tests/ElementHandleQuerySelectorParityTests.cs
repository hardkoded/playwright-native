/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-query-selector.spec.ts</c> parity for
    /// <see cref="IElementHandle.QuerySelectorAsync"/>,
    /// <see cref="IElementHandle.QuerySelectorAllAsync"/>, and
    /// <see cref="IElementHandle.EvalOnSelectorAsync{T}(string, string, object)"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleQuerySelectorParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19764;
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
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should query existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/playground.html").ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">A</div></div></body></html>").ConfigureAwait(false);
            IElementHandle html = await page.QuerySelectorAsync("html").ConfigureAwait(false);
            IElementHandle second = await html.QuerySelectorAsync(".second").ConfigureAwait(false);
            IElementHandle inner = await second.QuerySelectorAsync(".inner").ConfigureAwait(false);
            string content = await page.EvaluateAsync<string>("e => e.textContent", inner).ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("A"));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should return null for non-existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullForNonExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">B</div></div></body></html>").ConfigureAwait(false);
            IElementHandle html = await page.QuerySelectorAsync("html").ConfigureAwait(false);
            IElementHandle second = await html.QuerySelectorAsync(".third").ConfigureAwait(false);
            Assert.That(second, Is.Null);
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should work for adopted elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForAdoptedElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            Task<IPage> popupTask = page.WaitForPopupAsync();
            Task evaluateTask = page.EvaluateAsync(
                "url => { window['__popup'] = window.open(url); }",
                EmptyPage);
            await Task.WhenAll(popupTask, evaluateTask).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);

            IJSHandle divRemote = await page.EvaluateHandleAsync(@"() => {
    const div = document.createElement('div');
    document.body.appendChild(div);
    const span = document.createElement('span');
    span.textContent = 'hello';
    div.appendChild(span);
    return div;
  }").ConfigureAwait(false);
            IElementHandle divHandle = divRemote.AsElement();
            Assert.That(await divHandle.QuerySelectorAsync("span").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await divHandle.EvalOnSelectorAsync<string>("span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("hello"));

            await popup.WaitForLoadStateAsync(LoadState.DOMContentLoaded).ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
    const div = document.querySelector('div');
    window['__popup'].document.body.appendChild(div);
  })()").ConfigureAwait(false);
            Assert.That(await divHandle.QuerySelectorAsync("span").ConfigureAwait(false), Is.Not.Null);
            Assert.That(await divHandle.EvalOnSelectorAsync<string>("span", "e => e.textContent").ConfigureAwait(false), Is.EqualTo("hello"));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should query existing elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldQueryExistingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div>A</div><br/><div>B</div></body></html>").ConfigureAwait(false);
            IElementHandle html = await page.QuerySelectorAsync("html").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await html.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(2));
            Task<string>[] promises = new Task<string>[elements.Count];
            for (int i = 0; i < elements.Count; i++)
            {
                promises[i] = page.EvaluateAsync<string>("e => e.textContent", elements[i]);
            }

            string[] texts = await Task.WhenAll(promises).ConfigureAwait(false);
            Assert.That(texts, Is.EqualTo(new[] { "A", "B" }));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "should return empty array for non-existing elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyArrayForNonExistingElements()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><span>A</span><br/><span>B</span></body></html>").ConfigureAwait(false);
            IElementHandle html = await page.QuerySelectorAsync("html").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> elements = await html.QuerySelectorAllAsync("div").ConfigureAwait(false);
            Assert.That(elements.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "xpath should query existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldQueryExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/playground.html").ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">A</div></div></body></html>").ConfigureAwait(false);
            IElementHandle html = await page.QuerySelectorAsync("html").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> second = await html.QuerySelectorAllAsync("xpath=./body/div[contains(@class, 'second')]").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> inner = await second[0].QuerySelectorAllAsync("xpath=./div[contains(@class, 'inner')]").ConfigureAwait(false);
            string content = await page.EvaluateAsync<string>("e => e.textContent", inner[0]).ConfigureAwait(false);
            Assert.That(content, Is.EqualTo("A"));
        }

        [PlaywrightTest("elementhandle-query-selector.spec.ts", "xpath should return null for non-existing element")]
        [Test]
        [Timeout(30_000)]
        public async Task XpathShouldReturnNullForNonExistingElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<html><body><div class=\"second\"><div class=\"inner\">B</div></div></body></html>").ConfigureAwait(false);
            IElementHandle html = await page.QuerySelectorAsync("html").ConfigureAwait(false);
            IReadOnlyList<IElementHandle> second = await html.QuerySelectorAllAsync("xpath=/div[contains(@class, 'third')]").ConfigureAwait(false);
            Assert.That(second, Is.Empty);
        }
    }
}
