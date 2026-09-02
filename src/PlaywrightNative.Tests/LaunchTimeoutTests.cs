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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
