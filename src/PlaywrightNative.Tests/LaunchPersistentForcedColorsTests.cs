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
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official persistent-context <c>forcedColors</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentForcedColorsTests : PageTestEx
    {
        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync ForcedColors")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorForcedColors()
        {
            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit build does not expose ForcedColors via overrideUserPreference.");
                return;
            }

            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return;
            }

            if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            browserType = Playwright.Chromium;
            executablePath = BrowserExecutableFixture.ChromiumExecutablePath;

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-fc-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    ForcedColors = ForcedColors.Active,
                }).ConfigureAwait(false);

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(
                    await page.EvaluateAsync<bool>("matchMedia('(forced-colors: active)').matches").ConfigureAwait(false),
                    Is.True);
                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (Directory.Exists(userDataDir))
                    {
                        Directory.Delete(userDataDir, recursive: true);
                    }
                }
                catch (IOException)
                {
                }
            }
        }
    }
}
