/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>frame-goto.spec.ts</c>.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class FrameGotoParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19551;
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

        [TearDown]
        public void ResetServerRoutes()
        {
            Server?.Reset();
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

            Server.Reset();
        }

        private static IFrame FrameAt(IPage page, int index)
        {
            List<IFrame> frames = new List<IFrame>(page.Frames);
            Assert.That(frames.Count, Is.GreaterThan(index));
            return frames[index];
        }

        private static async Task<IFrame> AttachFrameAsync(IPage page, string name, string url)
        {
            string nameJson = JsonSerializer.Serialize(name);
            string urlJson = JsonSerializer.Serialize(url);
            string script =
                "(async () => { const f = document.createElement('iframe'); f.name = " +
                nameJson +
                "; f.id = " +
                nameJson +
                "; f.src = " +
                urlJson +
                "; const done = new Promise(r => f.onload = r); document.body.appendChild(f); await done; })()";
            await page.EvaluateAsync<object>(script).ConfigureAwait(false);

            DateTime deadline = DateTime.UtcNow.AddSeconds(15);
            while (DateTime.UtcNow < deadline)
            {
                IFrame named = page.Frame(name);
                if (named != null)
                {
                    return named;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Frame " + name + " did not attach.");
            return null;
        }

        [PlaywrightTest("frame-goto.spec.ts", "should navigate subframes")]
        [PlaywrightTest("frame-goto.spec.ts", "should navigate subframes @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNavigateSubframes()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);
            Assert.That(FrameAt(page, 0).Url, Does.Contain("/frames/one-frame.html"));
            Assert.That(FrameAt(page, 1).Url, Does.Contain("/frames/frame.html"));

            IResponse response = await FrameAt(page, 1).GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
            Assert.That(response.Frame, Is.SameAs(FrameAt(page, 1)));
        }

        [PlaywrightTest("frame-goto.spec.ts", "should reject when frame detaches")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRejectWhenFrameDetaches()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/frames/one-frame.html").ConfigureAwait(false);

            Server.SetRoute("/one-style.css", _ => Task.Delay(Timeout.Infinite));
            Task<IResponse> navigationTask = FrameAt(page, 1).GoToAsync(Prefix + "/one-style.html");
            await Server.WaitForRequest("/one-style.css").ConfigureAwait(false);

            await page.EvalOnSelectorAsync<object>("iframe", "frame => frame.remove()").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => navigationTask);
            Assert.That(error, Is.Not.Null);
            if (TestConstants.IsChromium)
            {
                Assert.That(
                    error.Message.Contains("net::ERR_ABORTED", StringComparison.Ordinal)
                    || error.Message.ToLowerInvariant().Contains("frame was detached"),
                    Is.True);
            }
            else
            {
                Assert.That(error.Message.ToLowerInvariant(), Does.Contain("frame was detached"));
            }
        }

        [PlaywrightTest("frame-goto.spec.ts", "should continue after client redirect")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldContinueAfterClientRedirect()
        {
            EnsureServer();
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore("script.js is requested before navigationCommitted arrives");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Server.SetRoute("/frames/script.js", _ => Task.Delay(Timeout.Infinite));
            string url = Prefix + "/frames/child-redirect.html";
            TimeoutException error = Assert.ThrowsAsync<TimeoutException>(
                () => page.GoToAsync(url, WaitUntilState.NetworkIdle, 5000));
            Assert.That(error.Message, Does.Contain("page.goto: Timeout 5000ms exceeded."));
            Assert.That(error.Message, Does.Contain("navigating to \"" + url + "\", waiting until \"networkidle\""));
        }

        [PlaywrightTest("frame-goto.spec.ts", "should return matching responses")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnMatchingResponses()
        {
            EnsureServer();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);

            IFrame[] frames =
            {
                await AttachFrameAsync(page, "frame1", EmptyPage).ConfigureAwait(false),
                await AttachFrameAsync(page, "frame2", EmptyPage).ConfigureAwait(false),
                await AttachFrameAsync(page, "frame3", EmptyPage).ConfigureAwait(false),
            };

            TaskCompletionSource<string>[] bodies =
            {
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
                new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously),
            };

            Server.SetRoute("/0.html", http => WriteWhenReadyAsync(http, bodies[0]));
            Server.SetRoute("/1.html", http => WriteWhenReadyAsync(http, bodies[1]));
            Server.SetRoute("/2.html", http => WriteWhenReadyAsync(http, bodies[2]));

            Task<IResponse>[] navigations = new Task<IResponse>[3];
            for (int i = 0; i < 3; i++)
            {
                navigations[i] = frames[i].GoToAsync(Prefix + "/" + i.ToString(CultureInfo.InvariantCulture) + ".html");
                await Server.WaitForRequest("/" + i.ToString(CultureInfo.InvariantCulture) + ".html").ConfigureAwait(false);
            }

            string[] serverResponseTexts = { "AAA", "BBB", "CCC" };
            int[] order = { 1, 2, 0 };
            foreach (int i in order)
            {
                bodies[i].TrySetResult(serverResponseTexts[i]);
                IResponse response = await navigations[i].ConfigureAwait(false);
                Assert.That(response.Frame, Is.SameAs(frames[i]));
                Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(serverResponseTexts[i]));
            }
        }

        private static async Task WriteWhenReadyAsync(HttpContext http, TaskCompletionSource<string> body)
        {
            string text = await body.Task.ConfigureAwait(false);
            await http.Response.WriteAsync(text).ConfigureAwait(false);
        }
    }
}
