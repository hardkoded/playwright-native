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
    /// Official persistent-context <c>ClientCertificates</c> launch option.
    /// </summary>
    [TestFixture]
    public class LaunchPersistentClientCertificatesTests : PageTestEx
    {
        [PlaywrightTest("defaultbrowsercontext-1.spec.ts", "LaunchPersistentContextAsync ClientCertificates")]
        [Test]
        [Timeout(60_000)]
        public async Task LaunchPersistentContextAsyncShouldHonorClientCertificates()
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

            string userDataDir = Path.Combine(Path.GetTempPath(), "pwsharp-persist-certs-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(userDataDir);
            await using MutualTlsServer server = await MutualTlsServer.StartAsync().ConfigureAwait(false);
            try
            {
                IBrowserContext context = await browserType.LaunchPersistentContextAsync(userDataDir, new BrowserTypeLaunchPersistentContextOptions
                {
                    ExecutablePath = executablePath,
                    Headless = true,
                    IgnoreHTTPSErrors = true,
                    ClientCertificates = new[]
                    {
                        new ClientCertificate
                        {
                            Origin = server.Origin,
                            Cert = server.ClientCertPem,
                            Key = server.ClientKeyPem,
                        },
                    },
                }).ConfigureAwait(false);

                IAPIResponse response = await context.APIRequest.GetAsync(server.Origin + "/").ConfigureAwait(false);
                string body = await response.TextAsync().ConfigureAwait(false);
                Assert.That(body, Does.Contain("Hello CN=Alice"));

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
