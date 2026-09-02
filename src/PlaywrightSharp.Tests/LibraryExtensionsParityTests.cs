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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/chromium/extensions.spec.ts</c> parity. Chromium-only.
    /// File-level skip: Headless Shell has no support for extensions. MV3 skip:
    /// <c>--load-extension</c> is not supported in Chrome anymore. Do not edit
    /// leftover <c>ConnectOverCdpTests</c>, leftover CDP session tests, or
    /// leftover MV2 <c>wwwroot/simple-extension</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryExtensionsParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [SetUp]
        public void SkipNonChromiumExtensions()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Official Chromium-only extensions.spec.ts.");
            }

            if (IsHeadlessShell)
            {
                Assert.Ignore("Headless Shell has no support for extensions");
            }

            string channel = Environment.GetEnvironmentVariable("PWTEST_CHANNEL");
            if (!string.IsNullOrEmpty(channel)
                && channel.StartsWith("chrome", StringComparison.Ordinal))
            {
                Assert.Ignore("--load-extension is not supported in Chrome anymore. https://groups.google.com/a/chromium.org/g/chromium-extensions/c/1-g8EFx2BBY/m/S0ET5wPjCAAJ");
            }

            if (IsBrandedChrome(ResolveExtensionChromiumPath()))
            {
                Assert.Ignore("--load-extension is not supported in Chrome anymore. https://groups.google.com/a/chromium.org/g/chromium-extensions/c/1-g8EFx2BBY/m/S0ET5wPjCAAJ");
            }
        }

        [TearDown]
        public void ResetServer()
        {
            Server?.Reset();
        }

        [PlaywrightTest("extensions.spec.ts", "should support service worker stop and restart lifecycle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportServiceWorkerStopAndRestartLifecycle()
        {
            await using IBrowserContext context = await LaunchPersistentAsync(Asset("extension-mv3-sw-lifecycle")).ConfigureAwait(false);
            IWorker sw1 = await FirstServiceWorkerAsync(context).ConfigureAwait(false);
            double startTime1 = await sw1.EvaluateAsync<double>("() => globalThis.startTime").ConfigureAwait(false);

            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ICDPSession cdp = await context.NewCDPSessionAsync(page).ConfigureAwait(false);

            Task<string> versionPromise = WaitForCdpValueAsync(
                cdp,
                "ServiceWorker.workerVersionUpdated",
                payload => FirstVersionField(payload, "versionId"));
            Task<string> scopePromise = WaitForCdpValueAsync(
                cdp,
                "ServiceWorker.workerRegistrationUpdated",
                payload => FirstRegistrationScope(payload));
            await cdp.SendAsync("ServiceWorker.enable").ConfigureAwait(false);
            string versionId = await versionPromise.ConfigureAwait(false);
            string scopeURL = await scopePromise.ConfigureAwait(false);

            Task<string> stoppedPromise = WaitForCdpValueAsync(
                cdp,
                "ServiceWorker.workerVersionUpdated",
                payload => FirstVersionStatus(payload, "stopped"));
            await cdp.SendAsync("ServiceWorker.stopWorker", new { versionId }).ConfigureAwait(false);
            await stoppedPromise.ConfigureAwait(false);

            Task<string> runningPromise = WaitForCdpValueAsync(
                cdp,
                "ServiceWorker.workerVersionUpdated",
                payload => FirstVersionStatus(payload, "running"));
            await cdp.SendAsync("ServiceWorker.startWorker", new { scopeURL }).ConfigureAwait(false);
            await runningPromise.ConfigureAwait(false);

            double startTime2 = await sw1.EvaluateAsync<double>("() => globalThis.startTime").ConfigureAwait(false);
            Assert.That(startTime2, Is.GreaterThan(startTime1));
            IReadOnlyCollection<IWorker> workers = context.ServiceWorkers();
            Assert.That(workers.Count, Is.EqualTo(1));
            Assert.That(workers.First(), Is.SameAs(sw1));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("extensions.spec.ts", "should give access to the service worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGiveAccessToTheServiceWorker()
        {
            await using IBrowserContext context = await LaunchPersistentAsync(Asset("extension-mv3-simple")).ConfigureAwait(false);
            IWorker serviceWorker = await FirstServiceWorkerAsync(context).ConfigureAwait(false);
            Assert.That(serviceWorker, Is.Not.Null);
            Assert.That(context.ServiceWorkers(), Does.Contain(serviceWorker));
            await PollEqualAsync(() => serviceWorker.EvaluateAsync<int>("() => globalThis.MAGIC"), 42).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
            Assert.That(context.BackgroundPages.Count, Is.EqualTo(0));
        }

        [PlaywrightTest("extensions.spec.ts", "should give access to the service worker when recording video")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGiveAccessToTheServiceWorkerWhenRecordingVideo()
        {
            string videoDir = Path.Combine(Path.GetTempPath(), "pwsharp-wave906-video-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(videoDir);
            await using IBrowserContext context = await LaunchPersistentAsync(
                Asset("extension-mv3-simple"),
                new BrowserTypeLaunchPersistentContextOptions
                {
                    RecordVideoDir = videoDir,
                }).ConfigureAwait(false);
            IWorker serviceWorker = await FirstServiceWorkerAsync(context).ConfigureAwait(false);
            Assert.That(serviceWorker, Is.Not.Null);
            Assert.That(context.ServiceWorkers(), Does.Contain(serviceWorker));
            await PollEqualAsync(() => serviceWorker.EvaluateAsync<int>("() => globalThis.MAGIC"), 42).ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("extensions.spec.ts", "should support request/response events in the service worker")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportRequestResponseEventsInTheServiceWorker()
        {
            await using IBrowserContext context = await LaunchPersistentAsync(Asset("extension-mv3-simple")).ConfigureAwait(false);
            if (ChromiumMajorVersion(context) < 143)
            {
                await context.CloseAsync().ConfigureAwait(false);
                Assert.Ignore("needs workerScriptLoaded event");
            }

            Server.SetRoute("/empty.html", async ctx =>
            {
                ctx.Response.ContentType = "text/html";
                ctx.Response.Headers["x-response-foobar"] = "BarFoo";
                await ctx.Response.WriteAsync(" hello world! ").ConfigureAwait(false);
            });

            IWorker serviceWorker = await FirstServiceWorkerAsync(context).ConfigureAwait(false);
            Assert.That(serviceWorker.Url, Does.Match(new Regex("chrome-extension://.*")));
            Task<IRequest> requestTask = context.WaitForEventAsync(BrowserContextEvent.Request);
            Task<IResponse> responseTask = context.WaitForEventAsync(BrowserContextEvent.Response);
            Task evaluateTask = serviceWorker.EvaluateAsync<object>(
                "url => fetch(url, { method: 'POST', body: 'foobar', headers: { 'X-FOOBAR': 'KEKBAR' } })",
                TestConstants.EmptyPage);
            await Task.WhenAll(requestTask, responseTask, evaluateTask).ConfigureAwait(false);
            IRequest request = requestTask.Result;
            IResponse response = responseTask.Result;

            Assert.That(request.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(request.Method, Is.EqualTo("POST"));
            Dictionary<string, string> requestHeaders = await request.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(requestHeaders, Does.ContainKey("x-foobar"));
            Assert.That(requestHeaders["x-foobar"], Is.EqualTo("KEKBAR"));
            Assert.That(request.PostData, Is.EqualTo("foobar"));

            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(response.Url, Is.EqualTo(TestConstants.EmptyPage));
            Assert.That(response.Request, Is.SameAs(request));
            Assert.That(await response.TextAsync().ConfigureAwait(false), Is.EqualTo(" hello world! "));
            Dictionary<string, string> responseHeaders = await response.AllHeadersAsync().ConfigureAwait(false);
            Assert.That(responseHeaders, Does.ContainKey("x-response-foobar"));
            Assert.That(responseHeaders["x-response-foobar"], Is.EqualTo("BarFoo"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("extensions.spec.ts", "should report console messages from content script")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportConsoleMessagesFromContentScript()
        {
            await using IBrowserContext context = await LaunchPersistentAsync(Asset("extension-mv3-with-logging")).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            Task<IConsoleMessage> consolePromise = page.WaitForEventAsync(
                PageEvent.Console,
                e => e.Text.Contains("Test console log from a third-party execution context", StringComparison.Ordinal));
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            IConsoleMessage message = await consolePromise.ConfigureAwait(false);
            Assert.That(message.Text, Does.Contain("Test console log from a third-party execution context"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("extensions.spec.ts", "should use custom userAgent in service worker fetch requests")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseCustomUserAgentInServiceWorkerFetchRequests()
        {
            Server.SetRoute("/ua-echo", async ctx =>
            {
                ctx.Response.ContentType = "text/plain";
                await ctx.Response.WriteAsync(ctx.Request.Headers["user-agent"].ToString()).ConfigureAwait(false);
            });

            await using IBrowserContext context = await LaunchPersistentAsync(
                Asset("extension-mv3-simple"),
                new BrowserTypeLaunchPersistentContextOptions
                {
                    UserAgent = "MyTestAgent/1.0",
                }).ConfigureAwait(false);
            IWorker sw = await FirstServiceWorkerAsync(context).ConfigureAwait(false);
            string userAgent = await sw.EvaluateAsync<string>(
                "async url => { const response = await fetch(url); return response.text(); }",
                TestConstants.ServerUrl + "/ua-echo").ConfigureAwait(false);
            Assert.That(userAgent, Is.EqualTo("MyTestAgent/1.0"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static bool IsHeadlessShell
        {
            get
            {
                string path = ResolveExtensionChromiumPath() ?? string.Empty;
                return path.IndexOf("headless-shell", StringComparison.OrdinalIgnoreCase) >= 0;
            }
        }

        private static bool IsBrandedChrome(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return false;
            }

            return path.IndexOf("/opt/google/chrome", StringComparison.OrdinalIgnoreCase) >= 0
                || path.IndexOf("Google Chrome", StringComparison.OrdinalIgnoreCase) >= 0;
        }

        private static string ResolveExtensionChromiumPath()
        {
            string cache = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
                ".cache",
                "ms-playwright");
            if (Directory.Exists(cache))
            {
                foreach (string dir in Directory.GetDirectories(cache, "chromium-*").OrderByDescending(d => d, StringComparer.Ordinal))
                {
                    string exe = Path.Combine(dir, "chrome-linux", "chrome");
                    if (File.Exists(exe)
                        && exe.IndexOf("headless-shell", StringComparison.OrdinalIgnoreCase) < 0)
                    {
                        return exe;
                    }
                }
            }

            return BrowserExecutableFixture.ChromiumExecutablePath;
        }

        private static string Asset(string name)
        {
            return Path.Combine(
                TestUtils.FindParentDirectory("PlaywrightSharp.TestServer"),
                "wwwroot",
                name);
        }

        private static async Task<IBrowserContext> LaunchPersistentAsync(
            string extensionPath,
            BrowserTypeLaunchPersistentContextOptions options = null)
        {
            options ??= new BrowserTypeLaunchPersistentContextOptions();
            options.Headless = true;
            string executablePath = ResolveExtensionChromiumPath();
            if (string.IsNullOrEmpty(executablePath))
            {
                Assert.Ignore("Chromium executable not available (download skipped or failed).");
            }

            options.ExecutablePath = executablePath;
            options.Args = new[]
            {
                "--disable-extensions-except=" + extensionPath,
                "--load-extension=" + extensionPath,
            };
            return await Playwright.Chromium.LaunchPersistentContextAsync(string.Empty, options).ConfigureAwait(false);
        }

        private static async Task<IWorker> FirstServiceWorkerAsync(IBrowserContext context)
        {
            IReadOnlyCollection<IWorker> serviceWorkers = context.ServiceWorkers();
            if (serviceWorkers.Count > 0)
            {
                return serviceWorkers.First();
            }

            return await context.WaitForEventAsync(BrowserContextEvent.ServiceWorker).ConfigureAwait(false);
        }

        private static int ChromiumMajorVersion(IBrowserContext context)
        {
            string version = context.Browser?.Version ?? string.Empty;
            int dot = version.IndexOf('.');
            string major = dot >= 0 ? version.Substring(0, dot) : version;
            return int.TryParse(major, out int value) ? value : 0;
        }

        private static Task<string> WaitForCdpValueAsync(
            ICDPSession session,
            string method,
            Func<JsonElement, string> select)
        {
            TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            ICDPSessionEvent subscription = session.Event(method);
            void Handler(object sender, JsonElement? parameters)
            {
                if (!parameters.HasValue)
                {
                    return;
                }

                string value = select(parameters.Value);
                if (!string.IsNullOrEmpty(value))
                {
                    subscription.OnEvent -= Handler;
                    tcs.TrySetResult(value);
                }
            }

            subscription.OnEvent += Handler;
            return tcs.Task;
        }

        private static string FirstVersionField(JsonElement payload, string name)
        {
            if (!payload.TryGetProperty("versions", out JsonElement versions)
                || versions.ValueKind != JsonValueKind.Array
                || versions.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = versions[0];
            return first.TryGetProperty(name, out JsonElement value) ? value.GetString() : null;
        }

        private static string FirstVersionStatus(JsonElement payload, string expected)
        {
            string status = FirstVersionField(payload, "runningStatus");
            return string.Equals(status, expected, StringComparison.Ordinal) ? expected : null;
        }

        private static string FirstRegistrationScope(JsonElement payload)
        {
            if (!payload.TryGetProperty("registrations", out JsonElement registrations)
                || registrations.ValueKind != JsonValueKind.Array
                || registrations.GetArrayLength() == 0)
            {
                return null;
            }

            JsonElement first = registrations[0];
            return first.TryGetProperty("scopeURL", out JsonElement value) ? value.GetString() : null;
        }

        private static async Task PollEqualAsync<T>(Func<Task<T>> getValue, T expected)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            T last = default;
            while (DateTime.UtcNow < deadline)
            {
                last = await getValue().ConfigureAwait(false);
                if (Equals(last, expected))
                {
                    return;
                }

                await Task.Delay(20).ConfigureAwait(false);
            }

            Assert.That(last, Is.EqualTo(expected));
        }
    }
}
