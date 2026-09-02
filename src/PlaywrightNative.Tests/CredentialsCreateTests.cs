/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserContext.credentials.create</c>.
    /// </summary>
    [TestFixture]
    public class CredentialsCreateTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-storage-state.spec.ts", "create returns a generated passkey")]
        [Test]
        [Timeout(30_000)]
        public async Task CreateShouldReturnGeneratedPasskey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.InstallAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Assert.That(page, Is.Not.Null);

            VirtualCredential credential = await context.Credentials.CreateAsync("example.com").ConfigureAwait(false);
            Assert.That(credential.RpId, Is.EqualTo("example.com"));
            Assert.That(credential.Id, Is.Not.Empty);
            Assert.That(credential.UserHandle, Is.Not.Empty);
            Assert.That(credential.PrivateKey, Is.Not.Empty);
            Assert.That(credential.PublicKey, Is.Not.Empty);
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "create honors an explicit id")]
        [Test]
        [Timeout(30_000)]
        public async Task CreateShouldHonorExplicitId()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            VirtualCredential credential = await context.Credentials
                .CreateAsync("example.com", id: "Y3JlZDE")
                .ConfigureAwait(false);
            Assert.That(credential.Id, Is.EqualTo("Y3JlZDE"));
            Assert.That(credential.RpId, Is.EqualTo("example.com"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "create throws on empty rpId")]
        [Test]
        [Timeout(30_000)]
        public void CreateShouldThrowOnEmptyRpId()
        {
            Assert.ThrowsAsync<ArgumentException>(async () =>
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                await context.Credentials.CreateAsync(string.Empty).ConfigureAwait(false);
            });
        }
    }
}
