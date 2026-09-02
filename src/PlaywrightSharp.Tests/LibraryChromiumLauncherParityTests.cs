/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/chromium/launcher.spec.ts</c> parity. Chromium-only
    /// launchServer remote-debugging args and <c>newBrowserCDPSession</c>
    /// target discovery. Ported via <c>LaunchAsync</c> (C# has no
    /// <c>launchServer</c>). Official <c>it.skip(mode !== 'default')</c> is
    /// default-mode only; this process is the default transport.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryChromiumLauncherParityTests : PageTestEx
    {
        [SetUp]
        public void SkipNonChromium()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only launcher.spec.ts.");
            }
        }

        [PlaywrightTest("launcher.spec.ts", "should throw with remote-debugging-pipe argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWithRemoteDebuggingPipeArgument()
        {
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
                {
                    Args = new[] { "--remote-debugging-pipe" },
                }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Playwright manages remote debugging connection itself"));
        }

        [PlaywrightTest("launcher.spec.ts", "should not throw with remote-debugging-port argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWithRemoteDebuggingPortArgument()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions
            {
                Args = new[] { "--remote-debugging-port=0" },
            }).ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("launcher.spec.ts", "should not create pages automatically")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCreatePagesAutomatically()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            ICDPSession browserSession = await browser.NewBrowserCDPSessionAsync().ConfigureAwait(false);
            List<JsonElement> targets = new List<JsonElement>();
            browserSession.Event("Target.targetCreated").OnEvent += (_, parameters) =>
            {
                if (!parameters.HasValue)
                {
                    return;
                }

                if (parameters.Value.TryGetProperty("targetInfo", out JsonElement targetInfo)
                    && targetInfo.TryGetProperty("type", out JsonElement type)
                    && type.GetString() != "browser")
                {
                    targets.Add(targetInfo);
                }
            };
            await browserSession.SendAsync("Target.setDiscoverTargets", new { discover = true }).ConfigureAwait(false);
            await browser.NewContextAsync().ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            Assert.That(targets.Count, Is.EqualTo(0));
        }
    }
}
