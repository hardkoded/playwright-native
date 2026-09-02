/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official persistent-context <c>storageState</c> path launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentStorageStatePathTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync StorageStatePath")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorStorageStatePath()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-ssp-" + Guid.NewGuid().ToString("N"));
            string statePath = Path.Combine(Path.GetTempPath(), "pwsharp-persist-ssp-" + Guid.NewGuid().ToString("N") + ".json");
            Directory.CreateDirectory(userDataDir);
            try
            {
                string state = "{\"cookies\":[{\"name\":\"wave496\",\"value\":\"fromfile\",\"url\":\"" +
                    TestConstants.EmptyPage +
                    "\",\"sameSite\":\"Lax\"}],\"origins\":[]}";
                File.WriteAllText(statePath, state);

                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    StorageStatePath = statePath,
                }).ConfigureAwait(false);

                IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
                Assert.That(cookies.Any(c => c.Name == "wave496" && c.Value == "fromfile"), Is.True);

                await context.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    if (File.Exists(statePath))
                    {
                        File.Delete(statePath);
                    }
                }
                catch (IOException)
                {
                }

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
