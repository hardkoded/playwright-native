/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/chromium/bfcache.spec.ts</c> parity. Do not edit
    /// leftover page-history bfcache titles.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBfcacheParityTests : PageTestEx
    {
        private static readonly string[] EnableBfcacheArgs = { "--disable-back-forward-cache" };

        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only bfcache.spec.ts.");
            }
        }

        [PlaywrightTest("bfcache.spec.ts", "bindings should work after restoring from bfcache")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task BindingsShouldWorkAfterRestoringFromBfcache()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                IgnoreDefaultArgsList = EnableBfcacheArgs,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.ExposeFunctionAsync("add", (int a, int b) => a + b).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.ServerUrl + "/cached/bfcached.html").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.add(1, 2)").ConfigureAwait(false), Is.EqualTo(3));
            await page.SetContentAsync("<a href='about:blank'}>click me</a>").ConfigureAwait(false);
            await page.ClickAsync("a").ConfigureAwait(false);
            await page.GoBackAsync(WaitUntilState.Commit).ConfigureAwait(false);
            await page.EvaluateAsync("window.didShow").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<int>("window.add(2, 3)").ConfigureAwait(false), Is.EqualTo(5));
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
