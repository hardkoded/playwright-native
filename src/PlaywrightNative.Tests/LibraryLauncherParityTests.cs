/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/launcher.spec.ts</c> parity. Three portable
    /// titles. Skip Node-only <c>should kill browser process on timeout
    /// after close</c> (<c>__testHookGracefullyClose</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryLauncherParityTests : PageTestEx
    {
        [PlaywrightTest("launcher.spec.ts", "should have an errors object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldHaveAnErrorsObject()
        {
            Assert.That(Playwright.Errors.TimeoutError.ToString(), Does.Contain("TimeoutError"));
        }

        [PlaywrightTest("launcher.spec.ts", "should have a devices object")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldHaveADevicesObject()
        {
            Assert.That(Playwright.Devices["iPhone 6"], Is.Not.Null);
            Assert.That(Playwright.Devices["iPhone 6"].DefaultBrowserType, Is.EqualTo("webkit"));
        }

        [PlaywrightTest("launcher.spec.ts", "should throw a friendly error if its headed and there is no xserver on linux running")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowAFriendlyErrorIfItsHeadedAndThereIsNoXserverOnLinuxRunning()
        {
            if (!OperatingSystem.IsLinux())
            {
                Assert.Ignore("official skip: platform !== 'linux'");
            }

            Dictionary<string, string> env = new()
            {
                ["DISPLAY"] = null,
            };

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Headless = false,
                    Env = env,
                }));
            Assert.That(error, Is.Not.Null);
            Assert.That(
                error.Message,
                Does.Match(new Regex("Looks like you launched a headed browser without having a XServer running.")));
            Assert.That(error.Message, Does.Match(new Regex("xvfb-run")));
        }

        private static Task<IBrowser> LaunchAsync(BrowserTypeLaunchOptions options = null)
        {
            options ??= new BrowserTypeLaunchOptions();
            if (string.IsNullOrEmpty(options.ExecutablePath))
            {
                options.ExecutablePath = CurrentExecutablePath();
            }

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
