/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
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
    /// IBrowserContext.StorageStateAsync and NewContext storageState on Chromium and WebKit.
    /// </summary>
    [TestFixture]
    public class ContextStorageStateTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "storageState restores cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task StorageStateShouldRestoreCookiesOnNewContext()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await source.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await source.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave66",
                    Value = "cookie",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            string state = await source.StorageStateAsync().ConfigureAwait(false);
            Assert.That(state, Does.Contain("wave66"));

            await using IBrowserContext restored = await browser.NewContextAsync(new() { StorageState = state }).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await restored.GetCookiesAsync().ConfigureAwait(false);
            BrowserContextCookiesResult found = cookies.FirstOrDefault(c => c.Name == "wave66");
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Value, Is.EqualTo("cookie"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "SetStorageStateAsync restores cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task SetStorageStateAsyncShouldRestoreCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await source.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await source.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave324",
                    Value = "cookie",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            string state = await source.StorageStateAsync().ConfigureAwait(false);

            await using IBrowserContext dest = await browser.NewContextAsync().ConfigureAwait(false);
            await dest.SetStorageStateAsync(state).ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await dest.GetCookiesAsync().ConfigureAwait(false);
            BrowserContextCookiesResult found = cookies.FirstOrDefault(c => c.Name == "wave324");
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Value, Is.EqualTo("cookie"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "storageStatePath restores cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task StorageStatePathShouldRestoreCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = Path.Combine(Path.GetTempPath(), "pw-wave66-storage-state.json");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await source.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await source.AddCookiesAsync(new[]
                {
                    new Cookie
                    {
                        Name = "fromfile",
                        Value = "yes",
                        Url = TestConstants.EmptyPage,
                        SameSite = SameSiteAttribute.Lax,
                    },
                }).ConfigureAwait(false);

                await source.StorageStateAsync(path).ConfigureAwait(false);
                Assert.That(File.Exists(path), Is.True);

                await using IBrowserContext restored = await browser.NewContextAsync(new BrowserContextOptions
                {
                    StorageStatePath = path,
                }).ConfigureAwait(false);
                IReadOnlyList<BrowserContextCookiesResult> cookies = await restored.GetCookiesAsync().ConfigureAwait(false);
                Assert.That(cookies.Any(c => c.Name == "fromfile" && c.Value == "yes"), Is.True);
            }
            finally
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "storageState restores localStorage")]
        [Test]
        [Timeout(30_000)]
        public async Task StorageStateShouldRestoreLocalStorage()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await source.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await page.EvaluateAsync("localStorage.setItem('wave66', 'ls')").ConfigureAwait(false);

            string state = await source.StorageStateAsync().ConfigureAwait(false);
            Assert.That(state, Does.Contain("wave66"));

            await using IBrowserContext restored = await browser.NewContextAsync(new() { StorageState = state }).ConfigureAwait(false);
            IPage restoredPage = await restored.NewPageAsync().ConfigureAwait(false);
            await restoredPage.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string value = await restoredPage.EvaluateAsync<string>("localStorage.getItem('wave66')").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("ls"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "storageState indexedDB is collected when requested")]
        [Test]
        [Timeout(30_000)]
        public async Task StorageStateShouldCollectIndexedDBWhenRequested()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await source.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            bool seeded = await page.EvaluateAsync<bool>(@"(async () => {
                await new Promise((resolve, reject) => {
                    const req = indexedDB.open('pw-wave390', 1);
                    req.onerror = () => reject(req.error);
                    req.onupgradeneeded = () => {
                        req.result.createObjectStore('items', { keyPath: 'id' });
                    };
                    req.onsuccess = () => {
                        const db = req.result;
                        const tx = db.transaction('items', 'readwrite');
                        tx.oncomplete = () => { db.close(); resolve(true); };
                        tx.onerror = () => reject(tx.error);
                        tx.objectStore('items').put({ id: 'hello', n: 1 });
                    };
                });
                const infos = await indexedDB.databases();
                return infos.some(info => info && info.name === 'pw-wave390');
            })()").ConfigureAwait(false);
            Assert.That(seeded, Is.True);

            string without = await source.StorageStateAsync().ConfigureAwait(false);
            Assert.That(without, Does.Not.Contain("pw-wave390"));

            string with = await source.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false);
            Assert.That(with, Does.Contain("pw-wave390"));
            Assert.That(with, Does.Contain("hello"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "SetStorageStateAsync restores indexedDB")]
        [Test]
        [Timeout(30_000)]
        public async Task SetStorageStateAsyncShouldRestoreIndexedDB()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await source.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            bool seeded = await page.EvaluateAsync<bool>(@"(async () => {
                await new Promise((resolve, reject) => {
                    const req = indexedDB.open('pw-wave391', 1);
                    req.onerror = () => reject(req.error);
                    req.onupgradeneeded = () => {
                        req.result.createObjectStore('items', { keyPath: 'id' });
                    };
                    req.onsuccess = () => {
                        const db = req.result;
                        const tx = db.transaction('items', 'readwrite');
                        tx.oncomplete = () => { db.close(); resolve(true); };
                        tx.onerror = () => reject(tx.error);
                        tx.objectStore('items').put({ id: 'restored', n: 2 });
                    };
                });
                return true;
            })()").ConfigureAwait(false);
            Assert.That(seeded, Is.True);

            string state = await source.StorageStateAsync(new() { IndexedDB = true }).ConfigureAwait(false);
            Assert.That(state, Does.Contain("pw-wave391"));

            await using IBrowserContext dest = await browser.NewContextAsync().ConfigureAwait(false);
            await dest.SetStorageStateAsync(state).ConfigureAwait(false);
            IPage restored = await dest.NewPageAsync().ConfigureAwait(false);
            await restored.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            string id = await restored.EvaluateAsync<string>(@"(async () => {
                const db = await new Promise((resolve, reject) => {
                    const req = indexedDB.open('pw-wave391', 1);
                    req.onerror = () => reject(req.error);
                    req.onsuccess = () => resolve(req.result);
                });
                try {
                    const rec = await new Promise((resolve, reject) => {
                        const tx = db.transaction('items', 'readonly');
                        const req = tx.objectStore('items').get('restored');
                        req.onerror = () => reject(req.error);
                        req.onsuccess = () => resolve(req.result);
                    });
                    return rec && rec.id;
                } finally {
                    db.close();
                }
            })()").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("restored"));
        }
    }
}
