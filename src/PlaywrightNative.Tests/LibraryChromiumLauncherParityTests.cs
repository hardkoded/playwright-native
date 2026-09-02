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
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
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
