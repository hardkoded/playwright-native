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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserContext.credentials.delete</c>.
    /// </summary>
    [TestFixture]
    public class CredentialsDeleteTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-storage-state.spec.ts", "delete removes a seeded credential")]
        [Test]
        [Timeout(30_000)]
        public async Task DeleteShouldRemoveASeededCredential()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.InstallAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page, Is.Not.Null);

            VirtualCredential created = await context.Credentials.CreateAsync("example.com").ConfigureAwait(false);
            await context.Credentials.DeleteAsync(created.Id).ConfigureAwait(false);

            IReadOnlyList<VirtualCredential> list = await context.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(list, Is.Empty);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "delete throws on empty id")]
        [Test]
        [Timeout(30_000)]
        public void DeleteShouldThrowOnEmptyId()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.Credentials.DeleteAsync(string.Empty).ConfigureAwait(false);
            });
        }
    }
}
