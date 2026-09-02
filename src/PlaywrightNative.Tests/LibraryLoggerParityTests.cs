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
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/logger.spec.ts</c> parity. Both titles.
    /// Official skip <c>should log @smoke</c> when <c>mode !== 'default'</c>
    /// (remote connection); this stack is always default.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryLoggerParityTests : PageTestEx
    {
        [PlaywrightTest("logger.spec.ts", "should log @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLog()
        {
            List<(string Name, PlaywrightLogSeverity Severity, string Message)> log = new();
            IBrowser browser = await LaunchAsync(new BrowserTypeLaunchOptions
            {
                Logger = new RecordingLogger(log),
            }).ConfigureAwait(false);
            await browser.NewContextAsync().ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            Assert.That(log.Exists(item => item.Severity == PlaywrightLogSeverity.Info), Is.True);
            Assert.That(log.Exists(item => item.Message.Contains("browser.newContext started")), Is.True);
            Assert.That(log.Exists(item => item.Message.Contains("browser.newContext succeeded")), Is.True);
        }

        [PlaywrightTest("logger.spec.ts", "should log context-level")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLogContextLevel()
        {
            List<(string Name, PlaywrightLogSeverity Severity, string Message)> log = new();
            IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                Logger = new RecordingLogger(log),
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Button</button>").ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);

            Assert.That(log.Count > 0, Is.True);
            Assert.That(log.Count(item => item.Message.Contains("page.setContent")) > 0, Is.True);
            Assert.That(log.Count(item => item.Message.Contains("page.click")) > 0, Is.True);
        }

        private static Task<IBrowser> LaunchAsync(BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            if (string.IsNullOrEmpty(options.ExecutablePath))
            {
                options.ExecutablePath = CurrentExecutablePath();
            }

            options.Headless = true;
            return CurrentBrowserType().LaunchAsync(options);
        }

        private static IBrowserType CurrentBrowserType()
        {
            if (TestConstants.IsWebKit)
            {
                return BrowserTypeInfo.Webkit;
            }

            if (TestConstants.IsFirefox)
            {
                return BrowserTypeInfo.Firefox;
            }

            return BrowserTypeInfo.Chromium;
        }

        private static string CurrentExecutablePath()
        {
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                return BrowserExecutableFixture.WebkitExecutablePath;
            }

            if (TestConstants.IsFirefox)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
                {
                    Assert.Ignore("Firefox executable not available (download skipped or failed).");
                }

                return BrowserExecutableFixture.FirefoxExecutablePath;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            return BrowserExecutableFixture.ChromiumExecutablePath;
        }

        private sealed class RecordingLogger : IPlaywrightLogger
        {
            private readonly List<(string Name, PlaywrightLogSeverity Severity, string Message)> _log;

            internal RecordingLogger(List<(string Name, PlaywrightLogSeverity Severity, string Message)> log)
            {
                _log = log;
            }

            public bool IsEnabled(string name, PlaywrightLogSeverity severity)
                => severity != PlaywrightLogSeverity.Verbose;

            public void Log(string name, PlaywrightLogSeverity severity, string message)
                => _log.Add((name, severity, message));
        }
    }
}
