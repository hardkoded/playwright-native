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
    /// <see cref="BrowserLauncher.LaunchAsync"/> launches Firefox when
    /// <c>PRODUCT=FIREFOX</c>.
    /// </summary>
    [TestFixture]
    public class FirefoxLauncherTests : PlaywrightTestEx
    {
        [PlaywrightTest("launcher.spec.ts", "BrowserLauncher launches Firefox when PRODUCT=FIREFOX")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchAsyncShouldOpenAPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            int sum = await page.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(sum, Is.EqualTo(2));
        }
    }
}
