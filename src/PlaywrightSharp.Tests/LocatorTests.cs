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
    /// Official <see cref="ILocator"/> foundation on <see cref="IPage"/> / <see cref="IFrame"/>.
    /// </summary>
    [TestFixture]
    public class LocatorTests : PageTestEx
    {
        [PlaywrightTest("locator-query.spec.ts", "Locator clicks a unique match")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldClickAUniqueMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"only\">Go</button>").ConfigureAwait(false);

            await page.Locator("button").ClickAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("only"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Locator click is strict")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenLocatorMatchesTwoNodes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><button>one</button><button>two</button></div>").ConfigureAwait(false);

            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => page.Locator("button").ClickAsync());

            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("strict mode violation"));
            Assert.That(ex.Message, Does.Contain("2 elements"));
        }

        [PlaywrightTest("locator-query.spec.ts", "First and Nth narrow a locator")]
        [Test]
        [Timeout(30_000)]
        public async Task FirstAndNthShouldNarrowTheMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"a\">A</button><button id=\"b\">B</button><button id=\"c\">C</button>").ConfigureAwait(false);

            await page.Locator("button").Nth(1).ClickAsync().ConfigureAwait(false);
            string second = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);

            await page.Locator("button").First.ClickAsync().ConfigureAwait(false);
            string first = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);

            await page.Locator("button").Last.ClickAsync().ConfigureAwait(false);
            string last = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);

            Assert.That(second, Is.EqualTo("b"));
            Assert.That(first, Is.EqualTo("a"));
            Assert.That(last, Is.EqualTo("c"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Nested locator queries descendants")]
        [Test]
        [Timeout(30_000)]
        public async Task NestedLocatorShouldQueryDescendants()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                "<div class=\"card\"><button id=\"a\">A</button></div>" +
                "<div class=\"card\"><button id=\"b\">B</button></div>").ConfigureAwait(false);

            await page.Locator(".card").Nth(1).Locator("button").ClickAsync().ConfigureAwait(false);

            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("b"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Count and All do not wait")]
        [Test]
        [Timeout(30_000)]
        public async Task CountAndAllShouldNotWait()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>one</button><button>two</button>").ConfigureAwait(false);

            ILocator buttons = page.Locator("button");
            Assert.That(await buttons.CountAsync().ConfigureAwait(false), Is.EqualTo(2));
            Assert.That(await buttons.AllAsync().ConfigureAwait(false), Has.Count.EqualTo(2));
            Assert.That(await page.Locator("section").CountAsync().ConfigureAwait(false), Is.EqualTo(0));
        }

        [PlaywrightTest("locator-query.spec.ts", "Fill and TextContent")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFillAndReadText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=\"name\" /><div id=\"label\">Hello</div>").ConfigureAwait(false);

            await page.Locator("#name").FillAsync("Ada").ConfigureAwait(false);
            string value = await page.EvaluateAsync<string>("document.querySelector('#name').value").ConfigureAwait(false);
            string text = await page.Locator("#label").TextContentAsync().ConfigureAwait(false);

            Assert.That(value, Is.EqualTo("Ada"));
            Assert.That(text, Is.EqualTo("Hello"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Locator re-queries after the DOM changes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRequeryAfterDomChange()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            ILocator button = page.Locator("button");
            Assert.That(await button.CountAsync().ConfigureAwait(false), Is.EqualTo(0));

            await page.EvaluateAsync<object>(
                "document.getElementById('host').innerHTML = '<button id=\"late\">Go</button>'").ConfigureAwait(false);

            await button.ClickAsync().ConfigureAwait(false);
            string id = await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("late"));
        }

        [PlaywrightTest("locator-query.spec.ts", "Frame.Locator clicks inside a child frame")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameLocatorShouldClickInsideChildFrame()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IFrame child = await AttachBlankChildFrameAsync(page).ConfigureAwait(false);
            await child.SetContentAsync("<button id=\"inner\">Go</button>").ConfigureAwait(false);

            await child.Locator("button").ClickAsync().ConfigureAwait(false);

            string id = await child.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false);
            Assert.That(child.Locator("button").Page, Is.SameAs(page));
            Assert.That(child.Locator("button").Frame, Is.SameAs(child));
            Assert.That(id, Is.EqualTo("inner"));
        }

        [PlaywrightTest("locator-query.spec.ts", "ElementHandleAsync waits for a match")]
        [Test]
        [Timeout(30_000)]
        public async Task ElementHandleShouldWaitForAMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div id=\"host\"></div>").ConfigureAwait(false);

            Task<IElementHandle> handleTask = page.Locator("#late").ElementHandleAsync();
            await page.EvaluateAsync<object>(
                "document.getElementById('host').innerHTML = '<span id=\"late\">ok</span>'").ConfigureAwait(false);

            IElementHandle handle = await handleTask.ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            Assert.That(await handle.TextContentAsync().ConfigureAwait(false), Is.EqualTo("ok"));
        }

        private static async Task<IFrame> AttachBlankChildFrameAsync(IPage page)
        {
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<bool>(@"
                const iframe = document.createElement('iframe');
                iframe.src = 'about:blank';
                document.body.appendChild(iframe);
                true
            ").ConfigureAwait(false);

            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                foreach (IFrame frame in page.MainFrame.ChildFrames)
                {
                    child = frame;
                    break;
                }

                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            Assert.That(child, Is.Not.Null);

            for (int i = 0; i < 50; i++)
            {
                try
                {
                    if (!await child.EvaluateAsync<bool>("window === window.top").ConfigureAwait(false))
                    {
                        return child;
                    }
                }
                catch (PlaywrightSharpException)
                {
                    // Execution context is not ready yet.
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            throw new TimeoutException("Child frame execution context did not become ready.");
        }
    }
}
