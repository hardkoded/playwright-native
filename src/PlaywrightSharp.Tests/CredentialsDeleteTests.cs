/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
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
