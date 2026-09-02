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
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official persistent-context <c>storageState</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentStorageStateTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync StorageState")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorStorageState()
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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-storage-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            try
            {
                string state = "{\"cookies\":[{\"name\":\"wave493\",\"value\":\"cookie\",\"url\":\"" +
                    TestConstants.EmptyPage +
                    "\",\"sameSite\":\"Lax\"}],\"origins\":[]}";

                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    StorageState = state,
                }).ConfigureAwait(false);

                IReadOnlyList<BrowserContextCookiesResult> cookies = await context.GetCookiesAsync().ConfigureAwait(false);
                BrowserContextCookiesResult found = cookies.FirstOrDefault(c => c.Name == "wave493");
                Assert.That(found, Is.Not.Null);
                Assert.That(found.Value, Is.EqualTo("cookie"));

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
