/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-click.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class ElementHandleClickParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19441;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
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

        [PlaywrightTest("elementhandle-click.spec.ts", "should work")]
        [PlaywrightTest("elementhandle-click.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should work with Node removed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWithNodeRemoved()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvaluateAsync("(() => delete window['Node'])()").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should work for Shadow DOM v1")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForShadowDOMV1()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/shadow.html").ConfigureAwait(false);
            IJSHandle buttonHandle = await page.EvaluateHandleAsync("() => window['button']").ConfigureAwait(false);
            await buttonHandle.AsElement().ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("clicked").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should work for TextNodes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForTextNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div id='outer' onclick=""window['result'] = 'Clicked ' + event.target.id;"">
      <div id='inner' style=""max-width: 50px"">Lorem ipsum dolor sit amet consectetur adipiscing elit proin, integer curabitur imperdiet rhoncus cursus tincidunt bibendum, consequat sed magnis laoreet luctus mollis tellus. Nisl parturient mus accumsan feugiat sem laoreet magnis nisi, aptent per sollicitudin gravida orci ac blandit, viverra eros praesent auctor vivamus semper bibendum. Consequat sed habitasse luctus dictumst gravida platea semper phasellus, nascetur ridiculus purus est varius quisque et scelerisque, id vehicula eleifend montes sollicitudin dis velit. Pellentesque ridiculus per natoque et eleifend taciti nunc, laoreet auctor at condimentum imperdiet ante, conubia mi cubilia scelerisque sociosqu sem.</div>
      Custom Text.
    </div>
  ").ConfigureAwait(false);
            IJSHandle buttonTextNode = await page.EvaluateHandleAsync("() => document.querySelector('#outer').lastChild").ConfigureAwait(false);
            await buttonTextNode.AsElement().ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("Clicked outer"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should throw for detached nodes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForDetachedNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.EvaluateAsync("button => button.remove()").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => button.ClickAsync());
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is not attached to the DOM"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should throw for hidden nodes with force")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForHiddenNodesWithForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.EvaluateAsync("button => button.style.display = 'none'").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => button.ClickAsync(new() { Force = true }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is not visible"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should throw for recursively hidden nodes with force")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForRecursivelyHiddenNodesWithForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.EvaluateAsync("button => button.parentElement.style.display = 'none'").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => button.ClickAsync(new() { Force = true }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is not visible"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should throw for <br> elements with force")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowForBrElementsWithForce()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("hello<br>goodbye").ConfigureAwait(false);
            IElementHandle br = await page.QuerySelectorAsync("br").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => br.ClickAsync(new() { Force = true }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is outside of the viewport"));
        }

        [PlaywrightTest("elementhandle-click.spec.ts", "should double click the button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDoubleClickTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/input/button.html").ConfigureAwait(false);
            await page.EvaluateAsync(@"(() => {
    window['double'] = false;
    const button = document.querySelector('button');
    button.addEventListener('dblclick', event => {
      window['double'] = true;
    });
})()").ConfigureAwait(false);
            IElementHandle button = await page.QuerySelectorAsync("button").ConfigureAwait(false);
            await button.DblClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("double").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvaluateAsync<string>("result").ConfigureAwait(false), Is.EqualTo("Clicked"));
        }
    }
}
