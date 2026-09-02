/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// NewContext geolocation and permissions applied to pages.
    /// </summary>
    [TestFixture]
    public class ContextGeolocationTests : PageTestEx
    {
        [PlaywrightTest("geolocation.spec.ts", "geolocation is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextGeolocationShouldApplyToPage()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { Geolocation = new Geolocation { Latitude = 10, Longitude = 10 }, Permissions = new[] { ContextPermissions.Geolocation } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await AssertGeolocationAsync(page, 10, 10).ConfigureAwait(false);
        }

        [PlaywrightTest("geolocation.spec.ts", "options bag geolocation")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyGeolocation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                Geolocation = new Geolocation { Latitude = 41.89f, Longitude = 12.49f },
                Permissions = new[] { ContextPermissions.Geolocation },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await AssertGeolocationAsync(page, 41.89f, 12.49f).ConfigureAwait(false);
        }

        [PlaywrightTest("geolocation.spec.ts", "NewPage applies geolocation")]
        [Test]
        [Timeout(30_000)]
        public async Task NewPageShouldApplyGeolocation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync(new() { Geolocation = new Geolocation { Latitude = 20, Longitude = 30 }, Permissions = new[] { ContextPermissions.Geolocation } }).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await AssertGeolocationAsync(page, 20, 30).ConfigureAwait(false);
        }

        private static async Task AssertGeolocationAsync(IPage page, float latitude, float longitude)
        {
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
            Assert.That(coords[0], Is.EqualTo(latitude).Within(0.01));
            Assert.That(coords[1], Is.EqualTo(longitude).Within(0.01));
        }
    }
}
