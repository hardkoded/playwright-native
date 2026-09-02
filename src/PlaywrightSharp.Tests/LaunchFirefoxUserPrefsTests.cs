/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.Firefox;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.FirefoxUserPrefs"/>.
    /// </summary>
    [TestFixture]
    public class LaunchFirefoxUserPrefsTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "WriteUserPrefs writes user.js")]
        [Test]
        public void WriteUserPrefsShouldWriteUserJs()
        {
            string profileDir = Path.Combine(Path.GetTempPath(), "pwsharp-ff-prefs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(profileDir);
            try
            {
                FirefoxBrowserType.WriteUserPrefs(profileDir, new Dictionary<string, object>
                {
                    ["general.oscpu.override"] = "Win32",
                    ["network.cookie.cookieBehavior"] = 1,
                    ["javascript.enabled"] = true,
                });

                string userJs = File.ReadAllText(Path.Combine(profileDir, "user.js"));
                Assert.That(userJs, Does.Contain("user_pref(\"general.oscpu.override\", \"Win32\");"));
                Assert.That(userJs, Does.Contain("user_pref(\"network.cookie.cookieBehavior\", 1);"));
                Assert.That(userJs, Does.Contain("user_pref(\"javascript.enabled\", true);"));
            }
            finally
            {
                try
                {
                    Directory.Delete(profileDir, true);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "launch FirefoxUserPrefs writes user.js into the profile")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchShouldWriteFirefoxUserPrefsIntoTheProfile()
        {
            if (!TestConstants.IsFirefox)
            {
                Assert.Ignore("FirefoxUserPrefs is a Firefox launch option.");
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.FirefoxExecutablePath))
            {
                Assert.Ignore("Firefox executable not available (download skipped or failed).");
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-ff-prefs-launch-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await Playwright.Firefox.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchOptions
                {
                    ExecutablePath = BrowserExecutableFixture.FirefoxExecutablePath,
                    Headless = true,
                    FirefoxUserPrefs = new Dictionary<string, object>
                    {
                        ["general.oscpu.override"] = "Win32",
                    },
                }).ConfigureAwait(false);

                string userJs = File.ReadAllText(Path.Combine(userDataDir, "user.js"));
                Assert.That(userJs, Does.Contain("user_pref(\"general.oscpu.override\", \"Win32\");"));
                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    Directory.Delete(userDataDir, true);
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
