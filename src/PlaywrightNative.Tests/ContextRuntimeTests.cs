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
    /// Runtime IBrowserContext geolocation, offline, and permission overrides.
    /// </summary>
    [TestFixture]
    public class ContextRuntimeTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-basic.spec.ts", "SetOfflineAsync toggles navigator.onLine")]
        [Test]
        [Timeout(30_000)]
        public async Task SetOfflineShouldToggleNavigatorOnLine()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Assert.That(await page.EvaluateAsync<bool>("navigator.onLine").ConfigureAwait(false), Is.True);

            await context.SetOfflineAsync(true).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("navigator.onLine").ConfigureAwait(false), Is.False);

            await context.SetOfflineAsync(false).ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<bool>("navigator.onLine").ConfigureAwait(false), Is.True);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "SetGeolocationAsync applies to an existing page")]
        [Test]
        [Timeout(30_000)]
        public async Task SetGeolocationShouldApplyToExistingPage()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await context.SetGeolocationAsync(new Geolocation { Latitude = 33, Longitude = 44 }).ConfigureAwait(false);
            await AssertGeolocationAsync(page, 33, 44).ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-basic.spec.ts", "ClearPermissionsAsync revokes geolocation")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearPermissionsShouldRevokeGeolocation()
        {
            if (TestServerSetup.Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.GrantPermissionsAsync(new[] { ContextPermissions.Geolocation }).ConfigureAwait(false);
            await context.SetGeolocationAsync(new Geolocation { Latitude = 1, Longitude = 2 }).ConfigureAwait(false);
            await AssertGeolocationAsync(page, 1, 2).ConfigureAwait(false);

            await context.ClearPermissionsAsync().ConfigureAwait(false);
            await page.ReloadAsync().ConfigureAwait(false);
            string state = await page.EvaluateAsync<string>(
                "(async () => (await navigator.permissions.query({ name: 'geolocation' })).state)()").ConfigureAwait(false);
            Assert.That(state, Is.Not.EqualTo("granted"));
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
