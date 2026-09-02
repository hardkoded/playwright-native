/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official persistent-context <c>locale</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentLocaleTests : PageTestEx
    {
        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync Locale")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorLocale()
        {
            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-locale-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    Locale = "de-DE",
                }).ConfigureAwait(false);

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                if (TestConstants.IsChromium)
                {
                    string language = await page.EvaluateAsync<string>("navigator.language").ConfigureAwait(false);
                    Assert.That(language, Does.StartWith("de"));
                }
                else
                {
                    SimpleServer server = TestServerSetup.Server;
                    if (server == null)
                    {
                        Assert.Ignore("Test server is unavailable.");
                        return;
                    }

                    Task<IRequest> waitTask = page.WaitForRequestAsync(r => r.Url.Contains("/empty.html", StringComparison.Ordinal));
                    await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                    IRequest request = await waitTask.ConfigureAwait(false);
                    Assert.That(
                        request.Headers.Any(h =>
                            string.Equals(h.Key, "Accept-Language", StringComparison.OrdinalIgnoreCase) &&
                            h.Value.Contains("de-DE", StringComparison.OrdinalIgnoreCase)),
                        Is.True);
                }

                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
