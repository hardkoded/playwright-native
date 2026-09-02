/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>browserContext.storageState</c> <c>credentials</c> option.
    /// </summary>
    [TestFixture]
    public class StorageStateCredentialsTests : PageTestEx
    {
        [PlaywrightTest("browsercontext-storage-state.spec.ts", "default export omits credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task DefaultExportShouldOmitCredentials()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Credentials.CreateAsync("example.com", id: "Y3JlZDE").ConfigureAwait(false);

            string json = await context.StorageStateAsync().ConfigureAwait(false);
            Assert.That(json, Does.Not.Contain("\"credentials\""));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "credentials true includes an empty array")]
        [Test]
        [Timeout(30_000)]
        public async Task CredentialsTrueShouldIncludeEmptyArray()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            string json = await context.StorageStateAsync(true).ConfigureAwait(false);
            Assert.That(json, Does.Contain("\"credentials\":[]"));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "credentials true includes a seeded passkey")]
        [Test]
        [Timeout(30_000)]
        public async Task CredentialsTrueShouldIncludeSeededPasskey()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential created = await context.Credentials
                .CreateAsync("example.com", id: "Y3JlZDE")
                .ConfigureAwait(false);

            string json = await context.StorageStateAsync(true).ConfigureAwait(false);
            Assert.That(json, Does.Contain("\"credentials\""));
            Assert.That(json, Does.Contain("\"id\":\"Y3JlZDE\""));
            Assert.That(json, Does.Contain("\"rpId\":\"example.com\""));
            Assert.That(json, Does.Contain(created.PrivateKey));
            Assert.That(json, Does.Contain(created.PublicKey));
            Assert.That(json, Does.Contain(created.UserHandle));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "SetStorageStateAsync restores credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task SetStorageStateAsyncShouldRestoreCredentials()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            VirtualCredential created = await source.Credentials
                .CreateAsync("example.com", id: "Y3JlZDE")
                .ConfigureAwait(false);
            string state = await source.StorageStateAsync(true).ConfigureAwait(false);

            await using IBrowserContext dest = await browser.NewContextAsync().ConfigureAwait(false);
            await dest.Credentials.CreateAsync("other.example", id: "b3RoZXI").ConfigureAwait(false);
            await dest.SetStorageStateAsync(state).ConfigureAwait(false);

            IReadOnlyList<VirtualCredential> list = await dest.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(list, Has.Exactly(1).Items);
            Assert.That(list[0].Id, Is.EqualTo("Y3JlZDE"));
            Assert.That(list[0].RpId, Is.EqualTo("example.com"));
            Assert.That(list[0].PrivateKey, Is.EqualTo(created.PrivateKey));
            Assert.That(list[0].PublicKey, Is.EqualTo(created.PublicKey));
            Assert.That(list[0].UserHandle, Is.EqualTo(created.UserHandle));
        }

        [PlaywrightTest("browsercontext-storage-state.spec.ts", "NewContext storageState restores credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextShouldRestoreCredentialsAndInstall()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext source = await browser.NewContextAsync().ConfigureAwait(false);
            await source.Credentials.CreateAsync("example.com", id: "Y3JlZDE").ConfigureAwait(false);
            string state = await source.StorageStateAsync(true).ConfigureAwait(false);

            await using IBrowserContext restored = await browser.NewContextAsync(new() { StorageState = state }).ConfigureAwait(false);
            IReadOnlyList<VirtualCredential> list = await restored.Credentials.GetAsync().ConfigureAwait(false);
            Assert.That(list.Any(item => item.Id == "Y3JlZDE"), Is.True);

            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebAuthn virtual authenticator is Chromium CDP.");
                return;
            }

            IPage page = await restored.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            bool available = await page.EvaluateAsync<bool>(
                "PublicKeyCredential.isUserVerifyingPlatformAuthenticatorAvailable()").ConfigureAwait(false);
            Assert.That(available, Is.True);
        }
    }
}
