/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.Coverage"/>.
    /// </summary>
    [TestFixture]
    public class PageCoverageTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("css-coverage.spec.ts", "JS coverage reports executed scripts")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportJavaScriptCoverage()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Profiler precise coverage is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/cov.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("window.__covered = 1;");
            });
            Server.SetRoute("/cov.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><script src=\"/cov.js\"></script><body>ok</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/cov.html").ConfigureAwait(false);
            var entries = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);

            Assert.That(entries, Has.Some.Matches<JSCoverageEntry>(entry =>
                entry.Url != null && entry.Url.Contains("/cov.js") && !string.IsNullOrEmpty(entry.ScriptId)));
        }

        [PlaywrightTest("css-coverage.spec.ts", "JS coverage resetOnNavigation false keeps earlier scripts")]
        [Test]
        [Timeout(30_000)]
        public async Task StartJSCoverageAsyncShouldHonorResetOnNavigationFalse()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Profiler precise coverage is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/one.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("window.__one = 1;");
            });
            Server.SetRoute("/two.js", http =>
            {
                http.Response.ContentType = "application/javascript";
                return http.Response.WriteAsync("window.__two = 2;");
            });
            Server.SetRoute("/one.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><script src=\"/one.js\"></script><body>one</body></html>");
            });
            Server.SetRoute("/two.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><script src=\"/two.js\"></script><body>two</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync(resetOnNavigation: false).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/one.html").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/two.html").ConfigureAwait(false);
            IReadOnlyList<JSCoverageEntry> entries = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);

            Assert.That(entries, Has.Some.Matches<JSCoverageEntry>(entry =>
                entry.Url != null && entry.Url.Contains("/one.js")));
            Assert.That(entries, Has.Some.Matches<JSCoverageEntry>(entry =>
                entry.Url != null && entry.Url.Contains("/two.js")));
        }

        [PlaywrightTest("css-coverage.spec.ts", "JS coverage reportAnonymousScripts includes eval")]
        [Test]
        [Timeout(30_000)]
        public async Task StartJSCoverageAsyncShouldHonorReportAnonymousScripts()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Profiler precise coverage is Chromium-only.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.Coverage().StartJSCoverageAsync(reportAnonymousScripts: true).ConfigureAwait(false);
            int result = await page.EvaluateAsync<int>("1 + 2").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(3));
            IReadOnlyList<JSCoverageEntry> entries = await page.Coverage().StopJSCoverageAsync().ConfigureAwait(false);

            Assert.That(entries, Has.Some.Matches<JSCoverageEntry>(entry =>
                string.IsNullOrEmpty(entry.Url) || entry.Url.Contains("playwright", System.StringComparison.OrdinalIgnoreCase)));
        }

        [PlaywrightTest("css-coverage.spec.ts", "CSS coverage reports used rules")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportCssCoverage()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CSS rule usage tracking is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/cov.css", http =>
            {
                http.Response.ContentType = "text/css";
                return http.Response.WriteAsync("body { color: red } .unused { color: blue }");
            });
            Server.SetRoute("/cov.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><link rel=\"stylesheet\" href=\"/cov.css\"><body>ok</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/cov.html").ConfigureAwait(false);
            var entries = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);

            Assert.That(entries, Has.Some.Matches<CSSCoverageEntry>(entry =>
                entry.Url != null && entry.Url.Contains("/cov.css") && entry.Ranges.Count > 0));
        }

        [PlaywrightTest("css-coverage.spec.ts", "CSS coverage resetOnNavigation false keeps earlier styles")]
        [Test]
        [Timeout(30_000)]
        public async Task StartCSSCoverageAsyncShouldHonorResetOnNavigationFalse()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("CSS rule usage tracking is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetRoute("/one.css", http =>
            {
                http.Response.ContentType = "text/css";
                return http.Response.WriteAsync("body { color: red }");
            });
            Server.SetRoute("/two.css", http =>
            {
                http.Response.ContentType = "text/css";
                return http.Response.WriteAsync("body { color: blue }");
            });
            Server.SetRoute("/one.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><link rel=\"stylesheet\" href=\"/one.css\"><body>one</body></html>");
            });
            Server.SetRoute("/two.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><link rel=\"stylesheet\" href=\"/two.css\"><body>two</body></html>");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.Coverage().StartCSSCoverageAsync(resetOnNavigation: false).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/one.html").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/two.html").ConfigureAwait(false);
            IReadOnlyList<CSSCoverageEntry> entries = await page.Coverage().StopCSSCoverageAsync().ConfigureAwait(false);

            Assert.That(entries, Has.Some.Matches<CSSCoverageEntry>(entry =>
                entry.Url != null && entry.Url.Contains("/one.css")));
            Assert.That(entries, Has.Some.Matches<CSSCoverageEntry>(entry =>
                entry.Url != null && entry.Url.Contains("/two.css")));
        }
    }
}
