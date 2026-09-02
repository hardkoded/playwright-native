using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for the direct Chromium CDP connection layer.
    /// These tests verify that we can launch Chromium, establish a CDP connection,
    /// retrieve the browser version, and close cleanly — all without the Node.js driver.
    /// </summary>
    /// <remarks>
    /// These tests are independent of the global TestServerSetup and the old driver.
    /// They test the new direct browser connection layer in isolation.
    /// Use: dotnet test --filter "FullyQualifiedName~CRBrowserConnectionTests"
    /// after downloading drivers with the tooling project.
    /// </remarks>
    [TestFixture]
    public class CRBrowserConnectionTests
    {
        [OneTimeSetUp]
        public Task EnsureChromiumAsync() => BrowserExecutable.EnsureAsync("chromium");

        [PlaywrightTest("connect-over-cdp.spec.ts", "should connect and get version")]
        [Test, Timeout(30_000)]
        public async Task ShouldConnectAndGetVersion()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            Assert.That(browser, Is.Not.Null, "Browser should not be null");
            Assert.That(browser.IsConnected, Is.True, "Browser should be connected");
            Assert.That(browser.Version, Is.Not.Null.And.Not.Empty, "Browser version should not be empty");

            TestContext.Out.WriteLine($"Browser version: {browser.Version}");
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should close gracefully")]
        [Test, Timeout(30_000)]
        public async Task ShouldCloseGracefully()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            Assert.That(browser.IsConnected, Is.True, "Browser should be connected before close");

            await browser.CloseAsync().ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);
            await browser.DisposeAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should send cdp command via session")]
        [Test, Timeout(30_000)]
        public async Task ShouldSendCDPCommandViaSession()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            // Send a simple CDP command directly on the root session.
            System.Text.Json.JsonElement? result = await browser.Connection.RootSession
                .SendAsync("Browser.getVersion").ConfigureAwait(false);

            Assert.That(result, Is.Not.Null, "Browser.getVersion response should not be null");
            Assert.That(result.Value.TryGetProperty("product", out _), Is.True, "Response should contain 'product' property");
            Assert.That(result.Value.TryGetProperty("userAgent", out _), Is.True, "Response should contain 'userAgent' property");

            TestContext.Out.WriteLine($"Full version info: {result.Value}");
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should create context and page")]
        [Test, Timeout(30_000)]
        public async Task ShouldCreateContextAndPage()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            // Create a new browser context.
            CRBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(context, Is.Not.Null, "Context should not be null");
            Assert.That(context.BrowserContextId, Is.Not.Null.And.Not.Empty, "Context should have an ID");

            // Create a new page in the context.
            CRPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page, Is.Not.Null, "Page should not be null");
            Assert.That(page.TargetId, Is.Not.Null.And.Not.Empty, "Page should have a target ID");

            TestContext.Out.WriteLine($"Context: {context.BrowserContextId}, Page target: {page.TargetId}");
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should close page and context")]
        [Test, Timeout(30_000)]
        public async Task ShouldClosePageAndContext()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            CRBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            CRPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(context.Pages, Has.Count.EqualTo(1), "Context should have 1 page");

            // Close the page.
            await page.ClosePageAsync(runBeforeUnload: false).ConfigureAwait(false);

            // Wait briefly for the detach event to propagate.
            await Task.Delay(500).ConfigureAwait(false);

            Assert.That(context.Pages, Has.Count.EqualTo(0), "Context should have 0 pages after close");

            // Close the context.
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should evaluate java script")]
        [Test, Timeout(30_000)]
        public async Task ShouldEvaluateJavaScript()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            CRBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            CRPage page = await context.NewPageAsync().ConfigureAwait(false);

            // Wait for the page to be fully initialized.
            await page.InitializedTask.ConfigureAwait(false);

            // Evaluate simple expressions (EvaluateAsync waits for execution context internally).
            int result = await page.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(2), "1+1 should equal 2");

            string hello = await page.EvaluateAsync<string>("'hello ' + 'world'").ConfigureAwait(false);
            Assert.That(hello, Is.EqualTo("hello world"), "String concatenation should work");

            bool boolean = await page.EvaluateAsync<bool>("true").ConfigureAwait(false);
            Assert.That(boolean, Is.True, "Boolean evaluation should work");

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should navigate to url")]
        [Test, Timeout(30_000)]
        public async Task ShouldNavigateToUrl()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            CRBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            CRPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.InitializedTask.ConfigureAwait(false);

            // GoToAsync navigates first to get the document ID, then waits for the
            // matching lifecycle event — filtering out stale events from prior documents.
            await page.GoToAsync("data:text/html,<h1>Hello</h1>").ConfigureAwait(false);

            // EvaluateAsync internally waits for a valid execution context.
            string title = await page.EvaluateAsync<string>("document.querySelector('h1').textContent").ConfigureAwait(false);
            Assert.That(title, Is.EqualTo("Hello"), "Page should contain the navigated content");

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("connect-over-cdp.spec.ts", "should create multiple pages")]
        [Test, Timeout(30_000)]
        public async Task ShouldCreateMultiplePages()
        {
            string executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            if (executablePath == null)
            {
                Assert.Ignore("Chromium executable not found. Skipping.");
            }

            await using CRBrowser browser = await ChromiumBrowserType.LaunchAsync(
                executablePath,
                headless: true).ConfigureAwait(false);

            CRBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            CRPage page1 = await context.NewPageAsync().ConfigureAwait(false);
            CRPage page2 = await context.NewPageAsync().ConfigureAwait(false);
            CRPage page3 = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(context.Pages, Has.Count.EqualTo(3), "Context should have 3 pages");
            Assert.That(page1.TargetId, Is.Not.EqualTo(page2.TargetId), "Pages should have different target IDs");

            await context.CloseAsync().ConfigureAwait(false);
        }
    }
}
