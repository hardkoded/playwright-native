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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsertype-launch.spec.ts</c> parity. Twelve
    /// portable titles. Skip Node-only <c>should handle timeout</c> and
    /// <c>should handle exception and report launch log</c>
    /// (<c>__testHookBeforeCreateBrowser</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserTypeLaunchParityTests : PlaywrightTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "should reject all promises when browser is closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRejectAllPromisesWhenBrowserIsClosed()
        {
            IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IPage page = await (await browser.NewContextAsync().ConfigureAwait(false)).NewPageAsync().ConfigureAwait(false);
            Task evaluateTask = page.EvaluateAsync("() => new Promise(r => {})");
            await page.EvaluateAsync("() => new Promise(f => setTimeout(f, 0))").ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => evaluateTask);
            Assert.That(error.Message, Does.Contain(" closed"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should throw if userDataDir option is passed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowIfUserDataDirOptionIsPassed()
        {
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => LaunchAsync(new BrowserTypeLaunchOptions { UserDataDir = "random-path" }));
            Assert.That(error.Message, Does.Contain("userDataDir option is not supported in `browserType.launch`. Use `browserType.launchPersistentContext` instead"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should throw if userDataDir is passed as an argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowIfUserDataDirIsPassedAsAnArgument()
        {
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Args = new[] { "--user-data-dir=random-path", "--profile=random-path" },
                }));
            Assert.That(error.Message, Does.Contain("Pass userDataDir parameter to 'browserType.launchPersistentContext"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should throw if port option is passed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowIfPortOptionIsPassed()
        {
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => LaunchAsync(new BrowserTypeLaunchOptions { Port = 1234 }));
            Assert.That(error.Message, Does.Contain("Cannot specify a port without launching as a server."));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should throw if port option is passed for persistent context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowIfPortOptionIsPassedForPersistentContext()
        {
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => CurrentBrowserType().LaunchPersistentContextAsync(
                    "foo",
                    new BrowserTypeLaunchOptions { Port = 1234 }));
            Assert.That(error.Message, Does.Contain("Cannot specify a port without launching as a server."));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should throw if page argument is passed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowIfPageArgumentIsPassed()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("official skip: browserName === 'firefox' && !isBidi");
            }

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => LaunchAsync(new BrowserTypeLaunchOptions { Args = new[] { "http://example.com" } }));
            Assert.That(error.Message, Does.Contain("can not specify page"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should reject if launched browser fails immediately")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRejectIfLaunchedBrowserFailsImmediately()
        {
            string dummy = TestUtils.GetWebServerFile("dummy_bad_browser_executable.js");
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => CurrentBrowserType().LaunchAsync(new BrowserTypeLaunchOptions { ExecutablePath = dummy }));
            Assert.That(
                Regex.IsMatch(error.Message, @"browserType\.launch(.|\n)*(spawn UNKNOWN|spawn EFTYPE|Browser logs:)", RegexOptions.IgnoreCase | RegexOptions.Multiline),
                Is.True,
                error.Message);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should reject if executable path is invalid")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldRejectIfExecutablePathIsInvalid()
        {
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => CurrentBrowserType().LaunchAsync(new BrowserTypeLaunchOptions { ExecutablePath = "random-invalid-path" }));
            Assert.That(error.Message, Does.Contain("Failed to launch"));
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should accept objects as options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAcceptObjectsAsOptions()
        {
            IBrowser browser = await LaunchAsync(new BrowserTypeLaunchOptions()).ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should fire close event for all contexts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireCloseEventForAllContexts()
        {
            IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            bool closed = false;
            context.Close += (_, _) => closed = true;
            await browser.CloseAsync().ConfigureAwait(false);
            Assert.That(closed, Is.True);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should be callable twice")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldBeCallableTwice()
        {
            IBrowser browser = await LaunchAsync().ConfigureAwait(false);
            await Task.WhenAll(browser.CloseAsync(), browser.CloseAsync()).ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "should allow await using")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAllowAwaitUsing()
        {
            IBrowser b;
            IBrowserContext c;
            IPage p;
            {
                await using IBrowser browser = await LaunchAsync().ConfigureAwait(false);
                b = browser;
                {
                    await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                    c = context;
                    {
                        await using IPage page = await context.NewPageAsync().ConfigureAwait(false);
                        p = page;
                    }

                    Assert.That(p.IsClosed, Is.True);
                }

                string clearError;
                try
                {
                    await c.ClearCookiesAsync().ConfigureAwait(false);
                    clearError = null;
                }
                catch (Exception ex)
                {
                    clearError = ex.Message;
                }

                Assert.That(clearError, Does.Contain("Target page, context or browser has been closed"));
            }

            Assert.That(b.IsConnected, Is.False);
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
    }
}
