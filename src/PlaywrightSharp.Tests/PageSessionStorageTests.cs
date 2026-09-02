/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page.sessionStorage</c>.
    /// </summary>
    [TestFixture]
    public class PageSessionStorageTests : PageTestEx
    {
        [PlaywrightTest("page-localstorage.spec.ts", "sessionStorage round-trip")]
        [Test]
        [Timeout(30_000)]
        public async Task SessionStorageRoundTripShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Assert.That(await page.SessionStorage.ItemsAsync().ConfigureAwait(false), Is.Empty);

            await page.SessionStorage.SetItemAsync("s1", "v1").ConfigureAwait(false);
            await page.SessionStorage.SetItemAsync("s2", "v2").ConfigureAwait(false);
            List<WebStorageItem> items = (await page.SessionStorage.ItemsAsync().ConfigureAwait(false))
                .OrderBy(item => item.Name)
                .ToList();
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(items[0].Name, Is.EqualTo("s1"));
            Assert.That(items[0].Value, Is.EqualTo("v1"));
            Assert.That(items[1].Name, Is.EqualTo("s2"));
            Assert.That(items[1].Value, Is.EqualTo("v2"));
            Assert.That(await page.SessionStorage.GetItemAsync("s1").ConfigureAwait(false), Is.EqualTo("v1"));

            await page.SessionStorage.RemoveItemAsync("s1").ConfigureAwait(false);
            items = (await page.SessionStorage.ItemsAsync().ConfigureAwait(false)).ToList();
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Name, Is.EqualTo("s2"));

            await page.SessionStorage.ClearAsync().ConfigureAwait(false);
            Assert.That(await page.SessionStorage.ItemsAsync().ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("page-localstorage.spec.ts", "localStorage and sessionStorage are independent")]
        [Test]
        [Timeout(30_000)]
        public async Task LocalStorageAndSessionStorageShouldBeIndependent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.LocalStorage.SetItemAsync("shared", "local").ConfigureAwait(false);
            await page.SessionStorage.SetItemAsync("shared", "session").ConfigureAwait(false);

            Assert.That(await page.LocalStorage.GetItemAsync("shared").ConfigureAwait(false), Is.EqualTo("local"));
            Assert.That(await page.SessionStorage.GetItemAsync("shared").ConfigureAwait(false), Is.EqualTo("session"));

            await page.LocalStorage.ClearAsync().ConfigureAwait(false);
            Assert.That(await page.LocalStorage.ItemsAsync().ConfigureAwait(false), Is.Empty);
            Assert.That(await page.SessionStorage.GetItemAsync("shared").ConfigureAwait(false), Is.EqualTo("session"));
        }
    }
}
