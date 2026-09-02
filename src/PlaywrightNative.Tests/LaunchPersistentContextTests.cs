/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserType.launchPersistentContext()</c>.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentContextTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync persists cookies")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldPersistCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext first = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                }).ConfigureAwait(false);

                Assert.That(first.Browser, Is.Not.Null);
                IPage page = await first.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await first.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Name = "wave462",
                        Value = "persist",
                        Url = TestConstants.EmptyPage,
                        SameSite = SameSiteAttribute.Lax,
                        Expires = DateTimeOffset.UtcNow.AddDays(1).ToUnixTimeSeconds(),
                    },
                }).ConfigureAwait(false);
                await page.EvaluateAsync("localStorage.setItem('wave462', 'persist')").ConfigureAwait(false);

                IReadOnlyList<BrowserContextCookiesResult> written = await first.GetCookiesAsync().ConfigureAwait(false);
                Assert.That(written.Any(c => c.Name == "wave462" && c.Value == "persist"), Is.True);
                await first.CloseAsync().ConfigureAwait(false);

                IBrowserContext second = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                }).ConfigureAwait(false);

                IPage restoredPage = await second.NewPageAsync().ConfigureAwait(false);
                await restoredPage.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                string stored = await restoredPage.EvaluateAsync<string>("String(localStorage.getItem('wave462'))").ConfigureAwait(false);
                Assert.That(stored, Is.EqualTo("persist"));
                await second.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Directory.Delete(userDataDir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync persists localStorage")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldPersistLocalStorage()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

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
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                browserType = Playwright.Firefox;
                executablePath = BrowserExecutableFixture.FirefoxExecutablePath;
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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-ls-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext first = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                }).ConfigureAwait(false);

                Assert.That(first.Browser, Is.Not.Null);
                IPage page = await first.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.EvaluateAsync("localStorage.setItem('wave594', 'persist')").ConfigureAwait(false);
                await first.CloseAsync().ConfigureAwait(false);

                IBrowserContext second = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                }).ConfigureAwait(false);

                Assert.That(second.Browser, Is.Not.Null);
                IPage restoredPage = await second.NewPageAsync().ConfigureAwait(false);
                await restoredPage.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                string stored = await restoredPage.EvaluateAsync<string>("String(localStorage.getItem('wave594'))").ConfigureAwait(false);
                Assert.That(stored, Is.EqualTo("persist"));
                await second.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Directory.Delete(userDataDir, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
