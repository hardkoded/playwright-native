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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/chromium/disable-web-security.spec.ts</c> parity.
    /// Chromium-only launch arg <c>--disable-web-security</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryDisableWebSecurityParityTests : PageTestEx
    {
        private static readonly string[] DisableWebSecurityArgs = { "--disable-web-security" };

        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only disable-web-security.spec.ts.");
            }
        }

        [PlaywrightTest("disable-web-security.spec.ts", "test utility world in popup w/ --disable-web-security")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TestUtilityWorldInPopupWDisableWebSecurity()
        {
            InstallRoutes();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = DisableWebSecurityArgs,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/main.html").ConfigureAwait(false);
            Task<IPage> page1Promise = page.Context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.GetByRole("link", name: "Click me").ClickAsync().ConfigureAwait(false);
            IPage page1 = await page1Promise.ConfigureAwait(false);
            await Assertions.Expect(page1).ToHaveURLAsync(new Regex("target")).ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("disable-web-security.spec.ts", "test init script w/ --disable-web-security")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TestInitScriptWDisableWebSecurity()
        {
            InstallRoutes();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = DisableWebSecurityArgs,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Context.AddInitScriptAsync("window.injected = 123").ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/main.html").ConfigureAwait(false);
            Task<IPage> page1Promise = page.Context.WaitForEventAsync(BrowserContextEvent.Page);
            await page.GetByRole("link", name: "Click me").ClickAsync().ConfigureAwait(false);
            IPage page1 = await page1Promise.ConfigureAwait(false);
            int value = await page1.EvaluateAsync<int>("window.injected").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(123));
            await page.CloseAsync().ConfigureAwait(false);
        }

        private static void InstallRoutes()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.Reset();
            Server.SetRoute("/main.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync(
                    "<a href=\"" + TestConstants.ServerUrl + "/target.html\" target=\"_blank\">Click me</a>");
            });
            Server.SetRoute("/target.html", http =>
            {
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<html></html>");
            });
        }
    }
}
