/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official persistent-context <c>geolocation</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentGeolocationTests : PageTestEx
    {
        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync Geolocation")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorGeolocation()
        {
            if (TestServerSetup.Server == null)
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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-geo-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    Geolocation = new Geolocation { Latitude = 10, Longitude = 10 },
                }).ConfigureAwait(false);

                await context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

                double[] coords = await page.EvaluateAsync<double[]>(
                    @"new Promise((resolve, reject) => {
                        const timer = setTimeout(() => reject(new Error('geolocation timeout')), 8000);
                        navigator.geolocation.getCurrentPosition(
                            pos => {
                                clearTimeout(timer);
                                resolve([pos.coords.latitude, pos.coords.longitude]);
                            },
                            err => {
                                clearTimeout(timer);
                                reject(new Error(err.message));
                            });
                    })").ConfigureAwait(false);

                Assert.That(coords, Is.Not.Null);
                Assert.That(coords, Has.Length.EqualTo(2));
                Assert.That(coords[0], Is.EqualTo(10).Within(0.01));
                Assert.That(coords[1], Is.EqualTo(10).Within(0.01));

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
