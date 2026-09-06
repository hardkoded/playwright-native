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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// IBrowserContext cookie APIs on Chromium and WebKit.
    /// </summary>
    [TestFixture]
    public class ContextCookieTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-cookies.spec.ts", "addCookies and getCookies roundtrip")]
        [Test]
        [Timeout(30_000)]
        public async Task AddCookiesShouldRoundTripThroughGetCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave65",
                    Value = "cookie",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            BrowserContextCookiesResult found = cookies.FirstOrDefault(c => c.Name == "wave65");
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Value, Is.EqualTo("cookie"));
            Assert.That(found.Domain, Does.Contain("localhost"));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "CookiesAsync aliases GetCookiesAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task CookiesAsyncShouldAliasGetCookiesAsync()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave228",
                    Value = "alias",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> viaGet = await context.GetCookiesAsync().ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> viaAlias = await context.CookiesAsync().ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> viaUrl = await context.CookiesAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(viaAlias.Any(c => c.Name == "wave228" && c.Value == "alias"), Is.True);
            Assert.That(viaGet.Count, Is.EqualTo(viaAlias.Count));
            Assert.That(viaUrl.Any(c => c.Name == "wave228"), Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "addCookies are visible to the page")]
        [Test]
        [Timeout(30_000)]
        public async Task AddCookiesShouldBeVisibleToDocumentCookie()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "fromctx",
                    Value = "yes",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            string documentCookie = await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false);
            Assert.That(documentCookie, Does.Contain("fromctx=yes"));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "clearCookies removes cookies")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearCookiesShouldRemoveAddedCookies()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "gone",
                    Value = "soon",
                    Url = TestConstants.EmptyPage,
                    SameSite = SameSiteAttribute.Lax,
                },
            }).ConfigureAwait(false);

            await context.ClearCookiesAsync().ConfigureAwait(false);
            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Any(c => c.Name == "gone"), Is.False);

            await page.ReloadAsync().ConfigureAwait(false);
            string documentCookie = await page.EvaluateAsync<string>("document.cookie").ConfigureAwait(false);
            Assert.That(documentCookie, Does.Not.Contain("gone=soon"));
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "clearCookies can filter by name")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearCookiesShouldRemoveMatchingNameOnly()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "wave343-keep", Value = "yes", Url = TestConstants.EmptyPage, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "wave343-drop", Value = "no", Url = TestConstants.EmptyPage, SameSite = SameSiteAttribute.Lax },
            }).ConfigureAwait(false);

            await context.ClearCookiesAsync("wave343-drop").ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Any(c => c.Name == "wave343-drop"), Is.False);
            Assert.That(cookies.Any(c => c.Name == "wave343-keep" && c.Value == "yes"), Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "clearCookies can filter by name regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearCookiesShouldRemoveMatchingNameRegex()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "wave407-keep", Value = "yes", Url = TestConstants.EmptyPage, SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "wave407-drop", Value = "no", Url = TestConstants.EmptyPage, SameSite = SameSiteAttribute.Lax },
            }).ConfigureAwait(false);

            await context.ClearCookiesAsync(new Regex("^wave407-drop$")).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Any(c => c.Name == "wave407-drop"), Is.False);
            Assert.That(cookies.Any(c => c.Name == "wave407-keep" && c.Value == "yes"), Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "clearCookies can filter by domain regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearCookiesShouldRemoveMatchingDomainRegex()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "wave426-keep", Value = "yes", Url = TestConstants.CrossProcessHttpPrefix + "/empty.html", SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "wave426-drop", Value = "no", Url = TestConstants.EmptyPage, SameSite = SameSiteAttribute.Lax },
            }).ConfigureAwait(false);

            await context.ClearCookiesAsync(null, new Regex("localhost")).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Any(c => c.Name == "wave426-drop"), Is.False);
            Assert.That(cookies.Any(c => c.Name == "wave426-keep" && c.Value == "yes"), Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "clearCookies can filter by path regex")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearCookiesShouldRemoveMatchingPathRegex()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            // Cookie API is url XOR domain/path (upstream network.py). Path-only
            // cookies must use an explicit domain/path pair.
            Uri empty = new Uri(TestConstants.EmptyPage);
            await context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "wave427-keep", Value = "yes", Domain = empty.Host, Path = "/", SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "wave427-drop", Value = "no", Domain = empty.Host, Path = "/grid.html", SameSite = SameSiteAttribute.Lax },
            }).ConfigureAwait(false);

            await context.ClearCookiesAsync(null, null, new Regex("grid\\.html$")).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Any(c => c.Name == "wave427-drop"), Is.False);
            Assert.That(cookies.Any(c => c.Name == "wave427-keep" && c.Value == "yes"), Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "clearCookies can filter by url")]
        [Test]
        [Timeout(30_000)]
        public async Task ClearCookiesShouldRemoveMatchingUrl()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            await context.AddCookiesAsync(new[]
            {
                new Cookie { Name = "wave433-keep", Value = "yes", Url = TestConstants.CrossProcessHttpPrefix + "/empty.html", SameSite = SameSiteAttribute.Lax },
                new Cookie { Name = "wave433-drop", Value = "no", Url = TestConstants.EmptyPage, SameSite = SameSiteAttribute.Lax },
            }).ConfigureAwait(false);

            await context.ClearCookiesAsync(new Uri(TestConstants.EmptyPage)).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            Assert.That(cookies.Any(c => c.Name == "wave433-drop"), Is.False);
            Assert.That(cookies.Any(c => c.Name == "wave433-keep" && c.Value == "yes"), Is.True);
        }

        [PlaywrightTest("browsercontext-cookies.spec.ts", "addCookies honors partitionKey")]
        [Test]
        [Timeout(30_000)]
        public async Task AddCookiesShouldHonorPartitionKey()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Cookie partitionKey is Chromium Storage.setCookies.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.HttpsPrefix + "/empty.html").ConfigureAwait(false);

            string origin = TestConstants.HttpsPrefix;
            await context.AddCookiesAsync(new[]
            {
                new Cookie
                {
                    Name = "wave384",
                    Value = "chips",
                    Url = origin + "/empty.html",
                    Secure = true,
                    SameSite = SameSiteAttribute.None,
                    PartitionKey = origin,
                },
            }).ConfigureAwait(false);

            IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
            BrowserContextCookiesResult found = cookies.FirstOrDefault(c => c.Name == "wave384");
            Assert.That(found, Is.Not.Null);
            Assert.That(found.Value, Is.EqualTo("chips"));
            Assert.That(found.PartitionKey, Does.Contain("localhost"));
        }
    }
}
