/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page.localStorage</c>.
    /// </summary>
    [TestFixture]
    public class PageLocalStorageTests : PageTestEx
    {
        [PlaywrightTest("page-localstorage.spec.ts", "localStorage.items returns empty array on fresh origin")]
        [Test]
        [Timeout(30_000)]
        public async Task ItemsShouldBeEmptyOnFreshOrigin()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            IReadOnlyList<WebStorageItem> items = await page.LocalStorage.ItemsAsync().ConfigureAwait(false);
            Assert.That(items, Is.Empty);
        }

        [PlaywrightTest("page-localstorage.spec.ts", "localStorage.getItem returns null for missing key")]
        [Test]
        [Timeout(30_000)]
        public async Task GetItemShouldReturnNullForMissingKey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            Assert.That(await page.LocalStorage.GetItemAsync("absent").ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("page-localstorage.spec.ts", "localStorage.setItem persists and surfaces in items()/getItem()")]
        [Test]
        [Timeout(30_000)]
        public async Task SetItemShouldPersistAndSurfaceInItems()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.LocalStorage.SetItemAsync("alpha", "1").ConfigureAwait(false);
            await page.LocalStorage.SetItemAsync("beta", "2").ConfigureAwait(false);

            List<WebStorageItem> items = (await page.LocalStorage.ItemsAsync().ConfigureAwait(false))
                .OrderBy(item => item.Name)
                .ToList();
            Assert.That(items, Has.Count.EqualTo(2));
            Assert.That(items[0].Name, Is.EqualTo("alpha"));
            Assert.That(items[0].Value, Is.EqualTo("1"));
            Assert.That(items[1].Name, Is.EqualTo("beta"));
            Assert.That(items[1].Value, Is.EqualTo("2"));
            Assert.That(await page.LocalStorage.GetItemAsync("alpha").ConfigureAwait(false), Is.EqualTo("1"));
            Assert.That(
                await page.EvaluateAsync<string>("localStorage.getItem('alpha')").ConfigureAwait(false),
                Is.EqualTo("1"));
        }

        [PlaywrightTest("page-localstorage.spec.ts", "localStorage.setItem overwrites existing value")]
        [Test]
        [Timeout(30_000)]
        public async Task SetItemShouldOverwriteExistingValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.LocalStorage.SetItemAsync("k", "first").ConfigureAwait(false);
            await page.LocalStorage.SetItemAsync("k", "second").ConfigureAwait(false);
            Assert.That(await page.LocalStorage.GetItemAsync("k").ConfigureAwait(false), Is.EqualTo("second"));
        }

        [PlaywrightTest("page-localstorage.spec.ts", "localStorage.removeItem removes a single item")]
        [Test]
        [Timeout(30_000)]
        public async Task RemoveItemShouldRemoveASingleItem()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.LocalStorage.SetItemAsync("a", "1").ConfigureAwait(false);
            await page.LocalStorage.SetItemAsync("b", "2").ConfigureAwait(false);
            await page.LocalStorage.RemoveItemAsync("a").ConfigureAwait(false);

            IReadOnlyList<WebStorageItem> items = await page.LocalStorage.ItemsAsync().ConfigureAwait(false);
            Assert.That(items, Has.Count.EqualTo(1));
            Assert.That(items[0].Name, Is.EqualTo("b"));
            Assert.That(items[0].Value, Is.EqualTo("2"));
        }

        [PlaywrightTest("page-localstorage.spec.ts", "localStorage.clear empties storage")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearShouldEmptyStorage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await page.LocalStorage.SetItemAsync("a", "1").ConfigureAwait(false);
            await page.LocalStorage.SetItemAsync("b", "2").ConfigureAwait(false);
            await page.LocalStorage.ClearAsync().ConfigureAwait(false);

            Assert.That(await page.LocalStorage.ItemsAsync().ConfigureAwait(false), Is.Empty);
        }
    }
}
