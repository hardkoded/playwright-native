/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.Channel"/>.
    /// </summary>
    [TestFixture]
    public class LaunchChannelTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "Channel resolver finds Chrome")]
        [Test]
        public void ResolverShouldFindChrome()
        {
            string path = BrowserChannelResolver.Resolve(BrowserChannel.Chrome);
            Assert.That(path, Does.Contain("chrome").IgnoreCase);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "Channel resolver handles Edge")]
        [Test]
        public void ResolverShouldHandleEdge()
        {
            bool installed = false;
            foreach (string candidate in BrowserChannelResolver.CandidatePaths(BrowserChannel.Msedge))
            {
                if (File.Exists(candidate))
                {
                    installed = true;
                    break;
                }
            }

            if (installed)
            {
                string path = BrowserChannelResolver.Resolve(BrowserChannel.Msedge);
                Assert.That(path, Does.Contain("msedge").IgnoreCase);
                return;
            }

            PlaywrightNativeException exception = Assert.Catch(() => BrowserChannelResolver.Resolve(BrowserChannel.Msedge)) as PlaywrightNativeException;
            Assert.That(exception, Is.Not.Null);
            Assert.That(exception.Message, Does.Contain("msedge"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch Channel Chrome starts Chromium")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchShouldUseChromeChannel()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Channel is a Chromium launch option.");
            }

            await using IBrowser browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                Channel = BrowserChannel.Chrome,
            }).ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("chrome://version").ConfigureAwait(false);
            string text = await page.EvaluateAsync<string>("document.body.innerText").ConfigureAwait(false);
            Assert.That(text, Does.Contain("Chrome").IgnoreCase);
        }
    }
}
