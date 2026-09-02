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
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.Args"/>.
    /// </summary>
    [TestFixture]
    public class LaunchArgsTests : PageTestEx
    {
        private static Task<IBrowser> LaunchWithArgsAsync(params string[] args)
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
                    Args = args,
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
                    Args = args,
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
                Args = args,
            });
        }

        private static bool MozLogExists(string logPath)
        {
            if (File.Exists(logPath) || File.Exists(logPath + ".moz_log"))
            {
                return true;
            }

            string directory = Path.GetDirectoryName(logPath);
            string prefix = Path.GetFileName(logPath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(prefix) || !Directory.Exists(directory))
            {
                return false;
            }

            string[] matches = Directory.GetFiles(directory, prefix + "*");
            return matches.Length > 0;
        }

        private static void TryDeleteMozLog(string logPath)
        {
            TryDeleteFile(logPath);
            TryDeleteFile(logPath + ".moz_log");
            string directory = Path.GetDirectoryName(logPath);
            string prefix = Path.GetFileName(logPath);
            if (string.IsNullOrEmpty(directory) || string.IsNullOrEmpty(prefix) || !Directory.Exists(directory))
            {
                return;
            }

            foreach (string path in Directory.GetFiles(directory, prefix + "*"))
            {
                TryDeleteFile(path);
            }
        }

        private static void TryDeleteFile(string path)
        {
            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch args are forwarded to the browser process")]
        [Test]
        [Timeout(30_000)]
        public async Task LaunchArgsShouldBeForwardedToTheBrowser()
        {
            if (TestConstants.IsWebKit)
            {
                Exception ex = Assert.CatchAsync<Exception>(async () =>
                {
                    await using IBrowser browser = await LaunchWithArgsAsync("--not-a-webkit-flag").ConfigureAwait(false);
                });
                Assert.That(ex, Is.Not.Null);
                Assert.That(ex.Message, Does.Contain("Unknown option"));
                return;
            }

            if (TestConstants.IsFirefox)
            {
                string logPath = Path.Combine(Path.GetTempPath(), "pw-wave576-" + Guid.NewGuid().ToString("N"));
                try
                {
                    await using IBrowser firefox = await LaunchWithArgsAsync(
                        "--MOZ_LOG=timestamp",
                        "--MOZ_LOG_FILE=" + logPath).ConfigureAwait(false);
                    IPage firefoxPage = await firefox.NewPageAsync().ConfigureAwait(false);
                    await firefoxPage.GoToAsync("data:text/html,<html><body>wave419</body></html>").ConfigureAwait(false);
                    string body = await firefoxPage.EvaluateAsync<string>("document.body.textContent").ConfigureAwait(false);
                    Assert.That(body, Does.Contain("wave419"));
                    Assert.That(MozLogExists(logPath), Is.True, "Firefox should honor --MOZ_LOG_FILE from launch Args");
                }
                finally
                {
                    TryDeleteMozLog(logPath);
                }

                return;
            }

            const string marker = "PlaywrightNative-Wave419";
            await using IBrowser chromium = await LaunchWithArgsAsync("--user-agent=" + marker).ConfigureAwait(false);
            IPage chromiumPage = await chromium.NewPageAsync().ConfigureAwait(false);
            string userAgent = await chromiumPage.EvaluateAsync<string>("navigator.userAgent").ConfigureAwait(false);
            Assert.That(userAgent, Does.Contain(marker));
        }
    }
}
