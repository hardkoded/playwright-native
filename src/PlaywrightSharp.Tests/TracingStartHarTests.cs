/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>tracing.startHar</c> / <c>tracing.stopHar</c>.
    /// </summary>
    [TestFixture]
    public class TracingStartHarTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("har.spec.ts", "should record HAR via startHar/stopHar")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRecordHarViaStartHarStopHar()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareHarRoute();
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.Tracing.StartHarAsync(path).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.Tracing.StopHarAsync().ConfigureAwait(false);

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                JsonElement log = document.RootElement.GetProperty("log");
                Assert.That(log.GetProperty("version").GetString(), Is.EqualTo("1.2"));
                Assert.That(ContainsUrl(log.GetProperty("entries"), "/har.html"), Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("har.spec.ts", "should throw on duplicate startHar")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowOnDuplicateStartHar()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.Tracing.StartHarAsync(path).ConfigureAwait(false);
                PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                    () => context.Tracing.StartHarAsync(path));
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex.Message, Does.Contain("already been started"));
                await context.Tracing.StopHarAsync().ConfigureAwait(false);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("har.spec.ts", "should throw when stopHar was not started")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenStopHarWasNotStarted()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            PlaywrightSharpException ex = Assert.CatchAsync<PlaywrightSharpException>(
                () => context.Tracing.StopHarAsync());
            Assert.That(ex, Is.Not.Null);
            Assert.That(ex.Message, Does.Contain("has not been started"));
        }

        [PlaywrightTest("har.spec.ts", "should write HAR when startHar disposable is disposed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWriteHarWhenStartHarDisposableIsDisposed()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            PrepareHarRoute();
            string path = TempHarPath();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await using (IAsyncDisposable session = await context.Tracing.StartHarAsync(path).ConfigureAwait(false))
                {
                    await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                }

                Assert.That(File.Exists(path), Is.True);
                using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
                Assert.That(
                    ContainsUrl(document.RootElement.GetProperty("log").GetProperty("entries"), "/har.html"),
                    Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        private static void PrepareHarRoute()
        {
            Server.Reset();
            Server.SetRoute("/har.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html><body>wave-629-har-marker</body></html>");
            });
        }

        private static string TempHarPath()
            => Path.Combine(Path.GetTempPath(), "pw-wave629-" + Guid.NewGuid().ToString("N") + ".har");

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }

        private static bool ContainsUrl(JsonElement entries, string fragment)
        {
            foreach (JsonElement entry in entries.EnumerateArray())
            {
                string url = entry.GetProperty("request").GetProperty("url").GetString();
                if (url != null && url.Contains(fragment, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
