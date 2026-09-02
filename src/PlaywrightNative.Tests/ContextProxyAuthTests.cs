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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for context proxy username/password.
    /// </summary>
    [TestFixture]
    public class ContextProxyAuthTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-proxy.spec.ts", "context proxy sends Basic credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAuthenticateToHttpProxy()
        {
            await using LoopbackHttpProxy proxy = new LoopbackHttpProxy("user", "secret");
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(
                proxy: new Proxy { Server = "http://per-context" }).ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                Proxy = new Proxy
                {
                    Server = "http://" + proxy.Server,
                    Username = "user",
                    Password = "secret",
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync("http://non-existent.invalid/authed.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(await page.TitleAsync().ConfigureAwait(false), Is.EqualTo("proxied"));
            Assert.That(proxy.Targets, Has.Some.Contain("authed.html"));
        }
    }
}
