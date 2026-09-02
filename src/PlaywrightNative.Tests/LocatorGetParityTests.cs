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
    /// Official <c>locator-get.spec.ts</c> parity for <see cref="IPage.Get(IBy)"/>,
    /// <see cref="IFrame.Get(IBy)"/>, <see cref="ILocator.Get(IBy)"/>,
    /// <see cref="IFrameLocator.Get(IBy)"/>, and the page-free <see cref="By"/>
    /// builder.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LocatorGetParityTests : PageTestEx
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
            int basePort = 19758;
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

        [PlaywrightTest("locator-query.spec.ts", "should build the same locators the getBy* factories build")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBuildTheSameLocatorsTheGetByFactoriesBuild()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.Get(By.AltText("Alt")).ToString(), Is.EqualTo(page.GetByAltText("Alt").ToString()));
            Assert.That(page.Get(By.Label("Label")).ToString(), Is.EqualTo(page.GetByLabel("Label").ToString()));
            Assert.That(page.Get(By.Placeholder("Placeholder")).ToString(), Is.EqualTo(page.GetByPlaceholder("Placeholder").ToString()));
            Assert.That(page.Get(By.Role("button", name: "Save")).ToString(), Is.EqualTo(page.GetByRole("button", name: "Save").ToString()));
            Assert.That(page.Get(By.TestId("id")).ToString(), Is.EqualTo(page.GetByTestId("id").ToString()));
            Assert.That(page.Get(By.Text("Text", exact: true)).ToString(), Is.EqualTo(page.GetByText("Text", exact: true).ToString()));
            Assert.That(page.Get(By.Title("Title")).ToString(), Is.EqualTo(page.GetByTitle("Title").ToString()));
        }

        [PlaywrightTest("locator-query.spec.ts", "should chain the same way locators chain")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldChainTheSameWayLocatorsChain()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            ILocator chained = page.GetByTestId("list").GetByRole("listitem").Filter("Row").First;
            Assert.That(
                page.Get(By.TestId("list").Role("listitem").Filter(hasText: "Row").First).ToString(),
                Is.EqualTo(chained.ToString()));
        }

        [PlaywrightTest("locator-query.spec.ts", "should compose get() the same way as chaining")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldComposeGetTheSameWayAsChaining()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IBy row = By.Role("listitem");
            IBy label = By.Text("Label");
            Assert.That(
                page.Get(By.TestId("list").Get(row.Get(label))).ToString(),
                Is.EqualTo(page.Get(By.TestId("list").Get(row).Get(label)).ToString()));
        }

        [PlaywrightTest("locator-query.spec.ts", "should accept a selector in get()")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptASelectorInGet()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(page.Get(By.Get("#outer")).ToString(), Is.EqualTo(page.Locator("#outer").ToString()));
            Assert.That(
                page.Get(By.Get("#outer").Get("span")).ToString(),
                Is.EqualTo(page.Locator("#outer").Locator("span").ToString()));

            await page.SetContentAsync("<div id=outer><span>Hello</span><span>World</span></div>").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get("#outer").Get("span"))).ToHaveTextAsync(new[] { "Hello", "World" }).ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get("#outer").Get(By.Text("World")))).ToHaveTextAsync("World").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Text("World").Get(".."))).ToHaveIdAsync("outer").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should work on page, frame and locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkOnPageFrameAndLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=outer><div data-testid=\"Hello\">Hello world</div></div>").ConfigureAwait(false);
            IBy hello = By.TestId("Hello");
            await Assertions.Expect(page.Get(hello)).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.MainFrame.Get(hello)).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("#outer").Get(hello)).ToHaveTextAsync("Hello world").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should work inside a frame locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkInsideAFrameLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            await Assertions.Expect(page.FrameLocator("iframe").Get(By.Get("div"))).ToHaveTextAsync("Hi, I'm frame").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should resolve the test id attribute when bound, not when built")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveTheTestIdAttributeWhenBoundNotWhenBuilt()
        {
            IBy hello = By.TestId("Hello");
            Playwright.Selectors.SetTestIdAttribute("data-my-custom-testid");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<div data-my-custom-testid=\"Hello\">Hello world</div>").ConfigureAwait(false);
                await Assertions.Expect(page.Get(hello)).ToHaveTextAsync("Hello world").ConfigureAwait(false);
            }
            finally
            {
                Playwright.Selectors.SetTestIdAttribute("data-testid");
            }
        }

        [PlaywrightTest("locator-query.spec.ts", "should resolve the test id attribute in nested and filter positions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveTheTestIdAttributeInNestedAndFilterPositions()
        {
            IBy row = By.Get("li").Filter(has: By.TestId("unread")).Get(By.Text("Subject"));
            Playwright.Selectors.SetTestIdAttribute("data-pw");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync(@"
    <ul>
      <li><span>Subject</span></li>
      <li><i data-pw=""unread""></i><span>Subject</span></li>
    </ul>
  ").ConfigureAwait(false);
                await Assertions.Expect(page.Get(row)).ToHaveCountAsync(1).ConfigureAwait(false);
            }
            finally
            {
                Playwright.Selectors.SetTestIdAttribute("data-testid");
            }
        }

        [PlaywrightTest("locator-query.spec.ts", "should filter by has, hasNot, hasText, hasNotText and visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterByHasHasNotHasTextHasNotTextAndVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div class=item><span>one</span><b>keep</b></div>
    <div class=item><span>two</span></div>
    <div class=item style=""display: none""><span>one</span><b>keep</b></div>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get(".item").Filter(has: By.Get("b")))).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get(".item").Filter(hasNot: By.Get("b")))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get(".item").Filter(hasText: "two"))).ToHaveCountAsync(1).ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get(".item").Filter(hasNotText: "two"))).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Get(".item").Filter(visible: true))).ToHaveCountAsync(2).ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should support and, or, nth, first and last")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportAndOrNthFirstAndLast()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button title=Subscribe>one</button>
    <button>two</button>
    <div role=button>three</div>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Role("button").And(By.Title("Subscribe")))).ToHaveTextAsync("one").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Role("button").Or(By.Get("div")))).ToHaveCountAsync(3).ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Role("button").Nth(1))).ToHaveTextAsync("two").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Role("button").First)).ToHaveTextAsync("one").ConfigureAwait(false);
            await Assertions.Expect(page.Get(By.Role("button").Last)).ToHaveTextAsync("three").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should describe the locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDescribeTheLocator()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Save</button>").ConfigureAwait(false);
            ILocator saveButton = page.Get(By.Role("button").Describe("save button"));
            await Assertions.Expect(saveButton).ToHaveTextAsync("Save").ConfigureAwait(false);
            Assert.That(saveButton.Description, Is.EqualTo("save button"));
        }

        [PlaywrightTest("locator-query.spec.ts", "should be reusable and never mutated by chaining")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeReusableAndNeverMutatedByChaining()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<ul><li>A</li><li>B</li></ul>").ConfigureAwait(false);
            IBy list = By.Get("ul");
            await Assertions.Expect(page.Get(list.Text("A"))).ToHaveTextAsync("A").ConfigureAwait(false);
            await Assertions.Expect(page.Get(list.Text("B"))).ToHaveTextAsync("B").ConfigureAwait(false);
            await Assertions.Expect(page.Get(list)).ToHaveTextAsync("AB").ConfigureAwait(false);
        }

        [PlaywrightTest("locator-query.spec.ts", "should throw for an empty by")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForAnEmptyBy()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightNativeException ex = Assert.Throws<PlaywrightNativeException>(() => page.Get(By.Empty));
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Match("Empty \"by\" locator"));
        }
    }
}
