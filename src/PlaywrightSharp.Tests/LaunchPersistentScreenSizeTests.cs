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
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official persistent-context <c>screen</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentScreenSizeTests : PageTestEx
    {
        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync ScreenSize")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorScreenSize()
        {
            IBrowserType browserType;
            string executablePath;
            if (TestConstants.IsWebKit)
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.WebkitExecutablePath))
                {
                    Assert.Ignore("WebKit executable not available (download skipped or failed).");
                }

                browserType = Playwright.Webkit;
                executablePath = BrowserExecutableFixture.WebkitExecutablePath;
            }
            else if (TestConstants.IsFirefox)
            {
                Assert.Ignore("LaunchPersistentContext is not wired for Firefox yet.");
                return;
            }
            else
            {
                if (string.IsNullOrEmpty(BrowserExecutableFixture.ChromiumExecutablePath))
                {
                    Assert.Ignore("Chromium executable not available (download skipped or failed).");
                }

                browserType = Playwright.Chromium;
                executablePath = BrowserExecutableFixture.ChromiumExecutablePath;
            }

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-screen-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    ViewportSize = new ViewportSize { Width = 400, Height = 300 },
                    ScreenSize = new ScreenSize { Width = 800, Height = 600 },
                }).ConfigureAwait(false);

                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("window.screen.width").ConfigureAwait(false), Is.EqualTo(800));
                Assert.That(await page.EvaluateAsync<int>("window.screen.height").ConfigureAwait(false), Is.EqualTo(600));
                Assert.That(await page.EvaluateAsync<int>("window.innerWidth").ConfigureAwait(false), Is.EqualTo(400));
                Assert.That(await page.EvaluateAsync<int>("window.innerHeight").ConfigureAwait(false), Is.EqualTo(300));

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
