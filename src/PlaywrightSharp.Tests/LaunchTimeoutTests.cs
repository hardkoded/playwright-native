/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.Timeout"/>.
    /// </summary>
    [TestFixture]
    public class LaunchTimeoutTests : PageTestEx
    {
        private static string FullMessage(Exception exception)
        {
            if (exception == null)
            {
                return string.Empty;
            }

            return exception.Message + " " + FullMessage(exception.InnerException);
        }

        private static Task<IBrowser> LaunchWithTimeoutAsync(int timeout)
        {
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                return Playwright.LaunchWebkitAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = BrowserExecutableFixture.WebkitExecutablePath,
                    Timeout = timeout,
                });
            }

            if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                return Playwright.LaunchFirefoxAsync(new BrowserTypeLaunchOptions
                {
                    ExecutablePath = BrowserExecutableFixture.FirefoxExecutablePath,
                    Timeout = timeout,
                    Headless = true,
                });
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            return Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = BrowserExecutableFixture.ChromiumExecutablePath,
                Timeout = timeout,
            });
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch Timeout 1 throws")]
        [Test]
        [Timeout(30_000)]
        public void LaunchShouldThrowWhenTimeoutIsOneMillisecond()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit pipe transport completes start as soon as the process is alive, so a 1ms Timeout cannot be observed.");
            }

            Exception ex = Assert.CatchAsync<Exception>(async () =>
            {
                await using IBrowser browser = await LaunchWithTimeoutAsync(1).ConfigureAwait(false);
            });
            Assert.That(ex, Is.Not.Null);
            string combined = FullMessage(ex);
            Assert.That(combined, Does.Contain("Timed out after 1 ms"), combined);
        }
    }
}
