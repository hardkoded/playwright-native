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
    /// Official <c>browserContext.credentials.install</c>.
    /// </summary>
    [TestFixture]
    public class CredentialsInstallTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-storage-state.spec.ts", "install completes")]
        [Test]
        [Timeout(30_000)]
        public async Task InstallShouldComplete()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.InstallAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page, Is.Not.Null);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "install enables a platform authenticator")]
        [Test]
        [Timeout(30_000)]
        public async Task InstallShouldEnablePlatformAuthenticator()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebAuthn virtual authenticator is Chromium CDP.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.InstallAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            bool available = await page.EvaluateAsync<bool>(
                "PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()").ConfigureAwait(false);
            Assert.That(available, Is.True);
        }
    }
}
