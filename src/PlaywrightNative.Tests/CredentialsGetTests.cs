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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserContext.credentials.get</c>.
    /// </summary>
    [TestFixture]
    public class CredentialsGetTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-storage-state.spec.ts", "get returns an empty list")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldReturnEmptyWhenNothingSeeded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IReadOnlyList<VirtualCredential> list = await context.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(list, Is.Empty);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "get returns seeded credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldReturnSeededCredentials()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential created = await context.Credentials.CreateAsync("example.com").ConfigureAwait(false);

            IReadOnlyList<VirtualCredential> list = await context.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(list, Has.Count.EqualTo(1));
            Assert.That(list[0].Id, Is.EqualTo(created.Id));
            Assert.That(list[0].RpId, Is.EqualTo("example.com"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "get filters by rpId and id")]
        [Test]
        [Timeout(30_000)]
        public async Task GetShouldFilterByRpIdAndId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.CreateAsync("a.example", id: "ida").ConfigureAwait(false);
            VirtualCredential b = await context.Credentials.CreateAsync("b.example", id: "idb").ConfigureAwait(false);

            IReadOnlyList<VirtualCredential> byRp = await context.Credentials.GetAsync("b.example").ConfigureAwait(false);
            Assert.That(byRp, Has.Count.EqualTo(1));
            Assert.That(byRp[0].Id, Is.EqualTo("idb"));

            IReadOnlyList<VirtualCredential> byId = await context.Credentials.GetAsync(new() { Id = "ida" }).ConfigureAwait(false);
            Assert.That(byId, Has.Count.EqualTo(1));
            Assert.That(byId[0].RpId, Is.EqualTo("a.example"));

            IReadOnlyList<VirtualCredential> both = await context.Credentials.GetAsync("b.example", b.Id).ConfigureAwait(false);
            Assert.That(both, Has.Count.EqualTo(1));
        }
    }
}
