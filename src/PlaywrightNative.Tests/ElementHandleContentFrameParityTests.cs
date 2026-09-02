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
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>elementhandle-content-frame.spec.ts</c> parity for
    /// <see cref="IElementHandle.ContentFrameAsync"/>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ElementHandleContentFrameParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                CrossProcessPrefix = TestConstants.CrossProcessHttpPrefix;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19762;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    CrossProcessPrefix = "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture);
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        private static async Task<bool> FixtureReachableAsync(string prefix)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2),
                };
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(prefix + "/empty.html").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string frameId, string url)
        {
            string frameIdJson = JsonSerializer.Serialize(frameId);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(() => new Promise(resolve => {" +
                "  const frame = document.createElement('iframe');" +
                "  frame.src = " + urlJson + ";" +
                "  frame.id = " + frameIdJson + ";" +
                "  frame.onload = () => resolve(true);" +
                "  document.body.appendChild(frame);" +
                "}))()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#" + frameId).ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            IFrame content = await handle.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(content, Is.Not.Null);
            return content;
        }

        private static IFrame ChildFrame(IPage page)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThanOrEqualTo(2));
            return frames[1];
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#frame1").ConfigureAwait(false);
            IFrame frame = await elementHandle.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(frame, Is.SameAs(ChildFrame(page)));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "should work for cross-process iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForCrossProcessIframes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", CrossProcessPrefix + "/empty.html").ConfigureAwait(false);
            IElementHandle elementHandle = await page.QuerySelectorAsync("#frame1").ConfigureAwait(false);
            IFrame frame = await elementHandle.ContentFrameAsync().ConfigureAwait(false);
            Assert.That(frame, Is.SameAs(ChildFrame(page)));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "should work for cross-frame evaluations")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkForCrossFrameEvaluations()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = ChildFrame(page);
            IJSHandle handle = await frame.EvaluateHandleAsync("() => window.top.document.querySelector('#frame1')").ConfigureAwait(false);
            IElementHandle elementHandle = handle.AsElement();
            Assert.That(elementHandle, Is.Not.Null);
            Assert.That(await elementHandle.ContentFrameAsync().ConfigureAwait(false), Is.SameAs(frame));
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "should return null for non-iframes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullForNonIframes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = ChildFrame(page);
            IJSHandle handle = await frame.EvaluateHandleAsync("() => document.body").ConfigureAwait(false);
            IElementHandle elementHandle = handle.AsElement();
            Assert.That(elementHandle, Is.Not.Null);
            Assert.That(await elementHandle.ContentFrameAsync().ConfigureAwait(false), Is.Null);
        }

        [PlaywrightTest("elementhandle-content-frame.spec.ts", "should return null for document.documentElement")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnNullForDocumentDocumentElement()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false);
            IFrame frame = ChildFrame(page);
            IJSHandle handle = await frame.EvaluateHandleAsync("() => document.documentElement").ConfigureAwait(false);
            IElementHandle elementHandle = handle.AsElement();
            Assert.That(elementHandle, Is.Not.Null);
            Assert.That(await elementHandle.ContentFrameAsync().ConfigureAwait(false), Is.Null);
        }
    }
}
