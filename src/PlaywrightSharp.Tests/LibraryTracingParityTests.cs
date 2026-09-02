/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/tracing.spec.ts</c> parity. Official
    /// <c>context.tracing</c> action traces. Do not edit leftover
    /// <c>ApiRequestTracingTests</c> or
    /// <c>ContextTracing*</c> CDP leftovers.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryTracingParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("tracing.spec.ts", "should collect trace with resources, but no js")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectTraceWithResourcesButNoJs()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = Path.Combine(Path.GetTempPath(), "pwsharp-trace-" + Path.GetRandomFileName() + ".zip");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/frames/frame.html").ConfigureAwait(false);
                await page.SetContentAsync("<button>Click</button>").ConfigureAwait(false);
                await page.ClickAsync("\"Click\"").ConfigureAwait(false);
                await page.Mouse.MoveAsync(20, 20).ConfigureAwait(false);
                await page.Mouse.DblClickAsync(30, 30).ConfigureAwait(false);
                await page.Keyboard.InsertTextAsync("abc").ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/input/fileupload.html").ConfigureAwait(false);
                await page.Locator("input[type=\"file\"]").SetInputFilesAsync(TestUtils.GetWebServerFile("file-to-upload.txt")).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(2000).ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                Assert.That(parsed.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(parsed.Actions, Is.EqualTo(new[]
                {
                    "Navigate /frames/frame.html",
                    "Set content",
                    "Click locator('text=\"Click\"')",
                    "Mouse move",
                    "Double click",
                    "Insert \"abc\"",
                    "Navigate /input/fileupload.html",
                    "Set input files locator('input[type=\"file\"]')",
                    "Wait for timeout",
                    "Close page",
                }));
                Assert.That(HasEvent(parsed, "frame-snapshot"), Is.True);
                Assert.That(HasEvent(parsed, "screencast-frame"), Is.True);
                JsonElement? style = FindResource(parsed, "style.css");
                Assert.That(style.HasValue, Is.True);
                Assert.That(style.Value.GetProperty("snapshot").GetProperty("response").GetProperty("content").TryGetProperty("_file", out JsonElement styleFile) && styleFile.ValueKind == JsonValueKind.String, Is.True);
                JsonElement? script = FindResource(parsed, "script.js");
                Assert.That(script.HasValue, Is.True);
                Assert.That(script.Value.GetProperty("snapshot").GetProperty("response").GetProperty("content").TryGetProperty("_file", out _), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should throw when starting with different options")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenStartingWithDifferentOptions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
            PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                () => context.Tracing.StartAsync(new TracingStartOptions { Screenshots = false, Snapshots = false }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Tracing has been already started"));
        }

        [PlaywrightTest("tracing.spec.ts", "should throw when stopping without start")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenStoppingWithoutStart()
        {
            string path = Path.Combine(Path.GetTempPath(), "pwsharp-trace-" + Path.GetRandomFileName() + ".zip");
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                PlaywrightSharpException error = Assert.CatchAsync<PlaywrightSharpException>(
                    () => context.Tracing.StopAsync(new TracingStopOptions { Path = path }));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Must start tracing before stopping"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not throw when stopping without start but not exporting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenStoppingWithoutStartButNotExporting()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await context.Tracing.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should use the correct title for event driven callbacks")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldUseTheCorrectTitleForEventDrivenCallbacks()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                await page.RouteAsync("**/empty.html", route => route.ContinueAsync()).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
                await page.EvaluateAsync("() => alert('yo')").ConfigureAwait(false);
                await page.ReloadAsync().ConfigureAwait(false);
                page.Dialog += (_, dialog) =>
                {
                    _ = dialog.AcceptAsync("answer!");
                };
                await page.EvaluateAsync("() => alert('yo')").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                Assert.That(parsed.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(parsed.Actions, Is.EqualTo(new[]
                {
                    "Route requests",
                    "Navigate /empty.html",
                    "Continue request",
                    "Navigate /grid.html",
                    "Evaluate",
                    "Reload",
                    "Evaluate",
                    "Accept dialog",
                }));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not collect snapshots by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCollectSnapshotsByDefault()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<button>Click</button>").ConfigureAwait(false);
                await page.ClickAsync("\"Click\"").ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                Assert.That(HasEvent(parsed, "frame-snapshot"), Is.False);
                Assert.That(HasEvent(parsed, "resource-snapshot"), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not collect action screenshots and aria snapshots by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCollectActionScreenshotsAndAriaSnapshotsByDefault()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                Assert.That(HasEvent(parsed, "screenshot"), Is.False);
                Assert.That(HasEvent(parsed, "aria-snapshot"), Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should collect action screenshots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectActionScreenshots()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { ScreenSnapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                string clickCallId = FindBefore(parsed, "click").GetProperty("callId").GetString();
                List<string> phases = new List<string>();
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "screenshot"
                        && item.GetProperty("callId").GetString() == clickCallId)
                    {
                        phases.Add(item.GetProperty("phase").GetString());
                        string file = item.GetProperty("file").GetString();
                        Assert.That(file, Is.EqualTo("screenshots/" + clickCallId + "-" + item.GetProperty("phase").GetString() + ".png"));
                        Assert.That(parsed.Resources.ContainsKey(file), Is.True);
                        Assert.That(PngWidth(parsed.Resources[file]), Is.GreaterThan(0));
                    }
                }

                Assert.That(phases, Is.EqualTo(new[] { "before", "action", "after" }));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should collect aria snapshots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectAriaSnapshots()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { AriaSnapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                string clickCallId = FindBefore(parsed, "click").GetProperty("callId").GetString();
                List<string> phases = new List<string>();
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "aria-snapshot"
                        && item.GetProperty("callId").GetString() == clickCallId)
                    {
                        phases.Add(item.GetProperty("phase").GetString());
                        string file = item.GetProperty("file").GetString();
                        Assert.That(file, Is.EqualTo("aria/" + clickCallId + "-" + item.GetProperty("phase").GetString() + ".json"));
                        using JsonDocument document = JsonDocument.Parse(parsed.Resources[file]);
                        Assert.That(HasButton(document.RootElement), Is.True);
                    }
                }

                Assert.That(phases, Is.EqualTo(new[] { "before", "action", "after" }));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "can call tracing.group/groupEnd at any time and auto-close")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CanCallTracingGroupGroupEndAtAnyTimeAndAutoClose()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.GroupAsync("ignored").ConfigureAwait(false);
                await context.Tracing.GroupEndAsync().ConfigureAwait(false);
                await context.Tracing.GroupAsync("ignored2").ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                await context.Tracing.GroupAsync("actual").ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(path).ConfigureAwait(false);
                await context.Tracing.GroupAsync("ignored3").ConfigureAwait(false);
                await context.Tracing.GroupEndAsync().ConfigureAwait(false);
                await context.Tracing.GroupEndAsync().ConfigureAwait(false);
                await context.Tracing.GroupEndAsync().ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                List<JsonElement> groups = new List<JsonElement>();
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("method", out JsonElement method)
                        && method.GetString() == "tracingGroup")
                    {
                        groups.Add(item);
                    }
                }

                Assert.That(groups, Has.Count.EqualTo(1));
                Assert.That(groups[0].GetProperty("title").GetString(), Is.EqualTo("actual"));
                string callId = groups[0].GetProperty("callId").GetString();
                bool hasAfter = false;
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "after"
                        && item.GetProperty("callId").GetString() == callId)
                    {
                        hasAfter = true;
                    }
                }

                Assert.That(hasAfter, Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not include buffers in the trace")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotIncludeBuffersInTheTrace()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/empty.html").ConfigureAwait(false);
                await page.ScreenshotAsync().ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                OfficialTraceParser.ActionObject screenshot = parsed.ActionObjects.Find(a => a.Method == "screenshot");
                Assert.That(screenshot, Is.Not.Null);
                List<string> phases = new List<string>();
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "frame-snapshot"
                        && item.TryGetProperty("snapshot", out JsonElement snapshot)
                        && snapshot.TryGetProperty("callId", out JsonElement callId)
                        && callId.GetString() == screenshot.CallId
                        && snapshot.TryGetProperty("phase", out JsonElement phase))
                    {
                        phases.Add(phase.GetString());
                    }
                }

                Assert.That(phases, Does.Contain("before"));
                Assert.That(phases, Does.Contain("after"));
                Assert.That(screenshot.Result.HasValue, Is.True);
                Assert.That(screenshot.Result.Value.GetProperty("binary").GetString(), Is.EqualTo("<Buffer>"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should exclude internal pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldExcludeInternalPages()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                await context.StorageStateAsync().ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                HashSet<string> pageIds = new HashSet<string>(StringComparer.Ordinal);
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("pageId", out JsonElement pageId)
                        && pageId.ValueKind == JsonValueKind.String
                        && !string.IsNullOrEmpty(pageId.GetString()))
                    {
                        pageIds.Add(pageId.GetString());
                    }

                    if (item.TryGetProperty("params", out JsonElement parameters)
                        && parameters.ValueKind == JsonValueKind.Object
                        && parameters.TryGetProperty("pageId", out JsonElement nested)
                        && nested.ValueKind == JsonValueKind.String
                        && !string.IsNullOrEmpty(nested.GetString()))
                    {
                        pageIds.Add(nested.GetString());
                    }
                }

                Assert.That(pageIds, Has.Count.EqualTo(1));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should record context API request trace independently")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordContextApiRequestTraceIndependently()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string browserTracePath = TempZip();
            string apiTracePath = TempZip();
            string apiUrl = TestConstants.ServerUrl + "/simple.json";
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                Assert.That(context.APIRequest.Tracing, Is.Not.SameAs(context.Tracing));

                await context.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await context.APIRequest.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/one-style.html").ConfigureAwait(false);
                await page.APIRequest.PostAsync(apiUrl, new() { DataObject = new { foo = "bar" } }).ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = browserTracePath }).ConfigureAwait(false);
                await context.APIRequest.Tracing.StopAsync(new TracingStopOptions { Path = apiTracePath }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace browserTrace = OfficialTraceParser.Parse(browserTracePath);
                Assert.That(browserTrace.Actions, Does.Contain("Navigate /one-style.html"));
                Assert.That(browserTrace.Actions, Does.Not.Contain("POST /simple.json"));
                Assert.That(FindResource(browserTrace, "/simple.json").HasValue, Is.False);
                Assert.That(FindResource(browserTrace, "/one-style.html").HasValue, Is.True);

                OfficialTraceParser.ParsedTrace apiTrace = OfficialTraceParser.Parse(apiTracePath);
                Assert.That(apiTrace.Actions, Does.Contain("POST /simple.json"));
                Assert.That(apiTrace.Actions, Does.Not.Contain("Navigate /one-style.html"));
                OfficialTraceParser.ActionObject apiAction = apiTrace.ActionObjects.Find(a => a.Class == "APIRequestContext" && a.Method == "fetch");
                Assert.That(apiAction, Is.Not.Null);
                Assert.That(OfficialTraceParser.RelativeStack(apiAction, apiTrace.Stacks), Is.EqualTo(new[] { "tracing.spec.ts" }));
                List<string> urls = new List<string>();
                List<string> refs = new List<string>();
                foreach (JsonElement item in apiTrace.Events)
                {
                    if (!item.TryGetProperty("type", out JsonElement type) || type.GetString() != "resource-snapshot")
                    {
                        continue;
                    }

                    urls.Add(item.GetProperty("snapshot").GetProperty("request").GetProperty("url").GetString());
                    refs.Add(item.GetProperty("snapshot").GetProperty("_apiRequestRef").GetString());
                }

                Assert.That(urls, Is.EqualTo(new[] { apiUrl }));
                Assert.That(refs, Has.Count.EqualTo(1));
                Assert.That(refs[0], Does.Match("^request-context@"));
            }
            finally
            {
                TryDelete(browserTracePath);
                TryDelete(apiTracePath);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should collect two traces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCollectTwoTraces()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string first = TempZip();
            string second = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<button>Click</button>").ConfigureAwait(false);
                await page.ClickAsync("\"Click\"").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = first }).ConfigureAwait(false);

                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                await page.DblClickAsync("\"Click\"").ConfigureAwait(false);
                await page.CloseAsync().ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = second }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace trace1 = OfficialTraceParser.Parse(first);
                Assert.That(trace1.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(trace1.Actions, Is.EqualTo(new[]
                {
                    "Navigate /empty.html",
                    "Set content",
                    "Click locator('text=\"Click\"')",
                }));

                OfficialTraceParser.ParsedTrace trace2 = OfficialTraceParser.Parse(second);
                Assert.That(trace2.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(trace2.Actions, Is.EqualTo(new[]
                {
                    "Double click locator('text=\"Click\"')",
                    "Close page",
                }));
            }
            finally
            {
                TryDelete(first);
                TryDelete(second);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should respect tracesDir and name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRespectTracesDirAndName()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string tracesDir = Path.Combine(Path.GetTempPath(), "pwsharp-traces-" + Path.GetRandomFileName());
            string first = TempZip();
            string second = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions { TracesDir = tracesDir }).ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Name = "name1", Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/one-style.html").ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(first).ConfigureAwait(false);
                Assert.That(File.Exists(Path.Combine(tracesDir, "name1.trace")), Is.True);
                Assert.That(File.Exists(Path.Combine(tracesDir, "name1.network")), Is.True);

                await context.Tracing.StartChunkAsync(new() { Name = "name2" }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/har.html").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = second }).ConfigureAwait(false);
                Assert.That(File.Exists(Path.Combine(tracesDir, "name2.trace")), Is.True);
                Assert.That(File.Exists(Path.Combine(tracesDir, "name2.network")), Is.True);

                OfficialTraceParser.ParsedTrace trace1 = OfficialTraceParser.Parse(first);
                Assert.That(trace1.Actions, Is.EqualTo(new[] { "Navigate /one-style.html" }));
                Assert.That(ResourceNames(trace1), Is.EqualTo(new[]
                {
                    "resources/XXX.css",
                    "resources/XXX.html",
                    "trace.network",
                    "trace.stacks",
                    "trace.trace",
                }));

                OfficialTraceParser.ParsedTrace trace2 = OfficialTraceParser.Parse(second);
                Assert.That(trace2.Actions, Is.EqualTo(new[] { "Navigate /har.html" }));
                Assert.That(ResourceNames(trace2), Is.EqualTo(new[]
                {
                    "resources/XXX.css",
                    "resources/XXX.html",
                    "resources/XXX.html",
                    "trace.network",
                    "trace.stacks",
                    "trace.trace",
                }));
            }
            finally
            {
                TryDelete(first);
                TryDelete(second);
                TryDeleteDir(tracesDir);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not include trace resources from the previous chunks")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotIncludeTraceResourcesFromThePreviousChunks()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string first = TempZip();
            string second = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true }).ConfigureAwait(false);
                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync(@"
    <style>
      @keyframes move {
        from { marign-left: 0; }
        to   { margin-left: 1000px; }
      }
      button {
        animation: 20s linear move;
        animation-iteration-count: infinite;
      }
    </style>
    <button>Click</button>
  ").ConfigureAwait(false);
                await page.ClickAsync("\"Click\"", new() { Force = true }).ConfigureAwait(false);
                await Task.Delay(3000).ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(first).ConfigureAwait(false);

                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(second).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace trace1 = OfficialTraceParser.Parse(first);
                List<string> names1 = new List<string>(trace1.Resources.Keys);
                Assert.That(CountSuffix(names1, ".html"), Is.EqualTo(1));
                List<string> jpegs = names1.FindAll(n => n.EndsWith(".jpeg", StringComparison.Ordinal));
                Assert.That(CountPrefix(names1, "src/"), Is.EqualTo(1));

                OfficialTraceParser.ParsedTrace trace2 = OfficialTraceParser.Parse(second);
                List<string> names2 = new List<string>(trace2.Resources.Keys);
                Assert.That(CountSuffix(names2, ".html"), Is.EqualTo(1));
                int preserved = 0;
                foreach (string jpeg in jpegs)
                {
                    if (names2.Contains(jpeg))
                    {
                        preserved++;
                    }
                }

                Assert.That(preserved, Is.EqualTo(0));
                Assert.That(CountPrefix(names2, "src/"), Is.EqualTo(0));
            }
            finally
            {
                TryDelete(first);
                TryDelete(second);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should overwrite existing file")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldOverwriteExistingFile()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<button>Click</button>").ConfigureAwait(false);
                await page.ClickAsync("\"Click\"").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);
                OfficialTraceParser.ParsedTrace first = OfficialTraceParser.Parse(path);
                Assert.That(CountSuffix(new List<string>(first.Resources.Keys), ".html"), Is.EqualTo(1));

                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true, Sources = true }).ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);
                OfficialTraceParser.ParsedTrace second = OfficialTraceParser.Parse(path);
                Assert.That(CountSuffix(new List<string>(second.Resources.Keys), ".html"), Is.EqualTo(0));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should collect sources")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldCollectSources()
        {
            Assert.Ignore("Node-only: traces sources hash __filename into src/{sha}.ts.");
        }

        [PlaywrightTest("tracing.spec.ts", "should record network failures")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordNetworkFailures()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await page.RouteAsync("**/*", route => route.AbortAsync("connectionaborted")).ConfigureAwait(false);
                try
                {
                    await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                }

                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);
                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                JsonElement? requestEvent = null;
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "resource-snapshot"
                        && item.GetProperty("snapshot").GetProperty("response").TryGetProperty("_failureText", out JsonElement failure)
                        && failure.ValueKind == JsonValueKind.String)
                    {
                        requestEvent = item;
                        break;
                    }
                }

                Assert.That(requestEvent.HasValue, Is.True);
                Assert.That(requestEvent.Value.GetProperty("snapshot").GetProperty("_monotonicTime").GetDouble(), Is.GreaterThan(0));
                Assert.That(requestEvent.Value.GetProperty("snapshot").GetProperty("time").GetDouble(), Is.GreaterThanOrEqualTo(0));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should recover tracing after a failed stop")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecoverTracingAfterAFailedStop()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string blocker = Path.Combine(Path.GetTempPath(), "pwsharp-trace-blocker-" + Path.GetRandomFileName());
            string failed = Path.Combine(blocker, "trace1.zip");
            string recovered = TempZip();
            try
            {
                File.WriteAllText(blocker, string.Empty);
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                Exception error = Assert.CatchAsync<Exception>(
                    () => context.Tracing.StopAsync(new TracingStopOptions { Path = failed }));
                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Match("ENOTDIR|ENOENT|EEXIST"));

                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = recovered }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(recovered);
                Assert.That(parsed.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(parsed.Actions, Does.Contain("Click locator('button')"));
            }
            finally
            {
                TryDelete(blocker);
                TryDelete(recovered);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should release the stack session when saving the trace fails")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldReleaseTheStackSessionWhenSavingTheTraceFails()
        {
            Assert.Ignore("Node-only: (context.tracing as any)._stacksId.");
        }

        [PlaywrightTest("tracing.spec.ts", "should not crash when browser closes mid-trace")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotCrashWhenBrowserClosesMidTrace()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.Context.Tracing.StartAsync(new TracingStartOptions { Snapshots = true, Screenshots = true }).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await browser.CloseAsync().ConfigureAwait(false);
            await Task.Delay(1000).ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should survive browser.close with auto-created traces dir")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldSurviveBrowserCloseWithAutoCreatedTracesDir()
        {
            Assert.Ignore("Node-only: (browserType as any)._playwright._defaultTracesDir.");
        }

        [PlaywrightTest("tracing.spec.ts", "should not stall on dialogs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotStallOnDialogs()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            page.Dialog += async (_, dialog) =>
            {
                await dialog.AcceptAsync().ConfigureAwait(false);
            };
            await page.EvaluateAsync("() => { confirm('are you sure'); }").ConfigureAwait(false);
            await context.Tracing.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should produce screencast frames fit")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldProduceScreencastFramesFit()
        {
            Assert.Ignore("Screenshot pixel-diff suite (expectRed/expectBlue, jpegjs).");
        }

        [PlaywrightTest("tracing.spec.ts", "should produce screencast frames crop")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldProduceScreencastFramesCrop()
        {
            Assert.Ignore("Screenshot pixel-diff suite (expectRed/expectBlue, jpegjs).");
        }

        [PlaywrightTest("tracing.spec.ts", "should produce screencast frames scale")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldProduceScreencastFramesScale()
        {
            Assert.Ignore("Screenshot pixel-diff suite (expectRed/expectBlue, jpegjs).");
        }

        [PlaywrightTest("tracing.spec.ts", "should include interrupted actions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeInterruptedActions()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<button>Click</button>").ConfigureAwait(false);
                _ = page.ClickAsync("\"ClickNoButton\"");
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);
                await context.CloseAsync().ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                Assert.That(parsed.Actions, Does.Contain("Click locator('text=\"ClickNoButton\"')"));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should work with multiple chunks")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithMultipleChunks()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string first = TempZip();
            string second = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/frames/frame.html").ConfigureAwait(false);
                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.SetContentAsync("<button>Click</button>").ConfigureAwait(false);
                await page.ClickAsync("\"Click\"").ConfigureAwait(false);
                _ = page.ClickAsync("\"ClickNoButton\"", new() { Timeout = 0 });
                await page.EvaluateAsync("() => {}").ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(first).ConfigureAwait(false);

                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.HoverAsync("\"Click\"").ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(second).ConfigureAwait(false);

                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.ClickAsync("\"Click\"").ConfigureAwait(false);
                await context.Tracing.StopChunkAsync().ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace trace1 = OfficialTraceParser.Parse(first);
                Assert.That(trace1.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(trace1.Actions, Is.EqualTo(new[]
                {
                    "Set content",
                    "Click locator('text=\"Click\"')",
                    "Click locator('text=\"ClickNoButton\"')",
                    "Evaluate",
                }));
                Assert.That(HasEvent(trace1, "frame-snapshot"), Is.True);
                Assert.That(FindResource(trace1, "style.css").HasValue, Is.True);

                OfficialTraceParser.ParsedTrace trace2 = OfficialTraceParser.Parse(second);
                Assert.That(trace2.Events[0].GetProperty("type").GetString(), Is.EqualTo("context-options"));
                Assert.That(trace2.Actions, Is.EqualTo(new[] { "Hover locator('text=\"Click\"')" }));
                Assert.That(HasEvent(trace2, "frame-snapshot"), Is.True);
                Assert.That(FindResource(trace2, "style.css").HasValue, Is.True);
            }
            finally
            {
                TryDelete(first);
                TryDelete(second);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should export trace concurrently to second navigation")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldExportTraceConcurrentlyToSecondNavigation()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            for (int timeout = 0; timeout < 200; timeout += 20)
            {
                string path = TempZip();
                try
                {
                    await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                    await page.GoToAsync(TestConstants.ServerUrl + "/grid.html").ConfigureAwait(false);
                    Task<IResponse> promise = page.GoToAsync(TestConstants.ServerUrl + "/grid.html");
                    await page.WaitForTimeoutAsync(timeout).ConfigureAwait(false);
                    await Task.WhenAll(promise, context.Tracing.StopAsync(new TracingStopOptions { Path = path })).ConfigureAwait(false);
                }
                finally
                {
                    TryDelete(path);
                }
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not hang for clicks that open dialogs")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotHangForClicksThatOpenDialogs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
            Task<IDialog> dialogTask = page.WaitForEventAsync(PageEvent.Dialog);
            await page.SetContentAsync("<div onclick='window.alert(123)'>Click me</div>").ConfigureAwait(false);
            try
            {
                await page.ClickAsync("div", new() { Timeout = 3500 }).ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }

            IDialog dialog = await dialogTask.ConfigureAwait(false);
            await dialog.DismissAsync().ConfigureAwait(false);
            await context.Tracing.StopAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("tracing.spec.ts", "should ignore iframes in head")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreIframesInHead()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
                await page.EvaluateAsync(@"() => {
    document.head.appendChild(document.createElement('iframe'));
    const div = document.createElement('div');
    document.head.appendChild(div);
    const shadow = div.attachShadow({ mode: 'open' });
    shadow.appendChild(document.createElement('iframe'));
  }").ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Screenshots = true, Snapshots = true }).ConfigureAwait(false);
                await page.ClickAsync("button").ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(path).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                Assert.That(parsed.Actions, Is.EqualTo(new[] { "Click locator('button')" }));
                Assert.That(HasEvent(parsed, "frame-snapshot"), Is.True);
                bool iframeInHead = false;
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type)
                        && type.GetString() == "frame-snapshot"
                        && item.TryGetProperty("snapshot", out JsonElement snapshot)
                        && snapshot.TryGetProperty("html", out JsonElement html)
                        && html.GetRawText().Contains("IFRAME", StringComparison.Ordinal))
                    {
                        iframeInHead = true;
                    }
                }

                Assert.That(iframeInHead, Is.False);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should hide internal stack frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldHideInternalStackFrames()
        {
            Assert.Ignore("Node-only: client stack frames in trace.stacks.");
        }

        [PlaywrightTest("tracing.spec.ts", "should hide internal stack frames in expect")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldHideInternalStackFramesInExpect()
        {
            Assert.Ignore("Node-only: client stack frames in expect.");
        }

        [PlaywrightTest("tracing.spec.ts", "should record global request trace")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRecordGlobalRequestTrace()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            string url = TestConstants.ServerUrl + "/simple.json";
            try
            {
                await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
                await request.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await request.GetAsync(url).ConfigureAwait(false);
                await request.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                List<JsonElement> actions = new List<JsonElement>();
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type) && type.GetString() == "resource-snapshot")
                    {
                        actions.Add(item);
                    }
                }

                Assert.That(actions, Has.Count.EqualTo(1));
                JsonElement requestSnap = actions[0].GetProperty("snapshot").GetProperty("request");
                Assert.That(requestSnap.GetProperty("method").GetString(), Is.EqualTo("GET"));
                Assert.That(requestSnap.GetProperty("url").GetString(), Is.EqualTo(url));
                JsonElement response = actions[0].GetProperty("snapshot").GetProperty("response");
                Assert.That(response.GetProperty("status").GetInt32(), Is.EqualTo(200));
                Assert.That(response.GetProperty("statusText").GetString(), Is.EqualTo("OK"));
                bool hasLength = false;
                foreach (JsonElement header in response.GetProperty("headers").EnumerateArray())
                {
                    if (string.Equals(header.GetProperty("name").GetString(), "Content-Length", StringComparison.OrdinalIgnoreCase)
                        && header.GetProperty("value").GetString() == "15")
                    {
                        hasLength = true;
                    }
                }

                Assert.That(hasLength, Is.True);
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should store global request traces separately")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStoreGlobalRequestTracesSeparately()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string first = TempZip();
            string second = TempZip();
            string url = TestConstants.ServerUrl + "/simple.json";
            try
            {
                await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
                await using IAPIRequestContext request2 = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
                await Task.WhenAll(
                    request.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }),
                    request2.Tracing.StartAsync(new TracingStartOptions { Snapshots = true })).ConfigureAwait(false);
                await Task.WhenAll(request.GetAsync(url), request2.PostAsync(url)).ConfigureAwait(false);
                await Task.WhenAll(
                    request.Tracing.StopAsync(new TracingStopOptions { Path = first }),
                    request2.Tracing.StopAsync(new TracingStopOptions { Path = second })).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace trace1 = OfficialTraceParser.Parse(first);
                List<JsonElement> actions1 = ResourceSnapshots(trace1);
                Assert.That(actions1, Has.Count.EqualTo(1));
                Assert.That(actions1[0].GetProperty("snapshot").GetProperty("request").GetProperty("method").GetString(), Is.EqualTo("GET"));
                Assert.That(actions1[0].GetProperty("snapshot").GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(url));

                OfficialTraceParser.ParsedTrace trace2 = OfficialTraceParser.Parse(second);
                List<JsonElement> actions2 = ResourceSnapshots(trace2);
                Assert.That(actions2, Has.Count.EqualTo(1));
                Assert.That(actions2[0].GetProperty("snapshot").GetProperty("request").GetProperty("method").GetString(), Is.EqualTo("POST"));
                Assert.That(actions2[0].GetProperty("snapshot").GetProperty("request").GetProperty("url").GetString(), Is.EqualTo(url));
            }
            finally
            {
                TryDelete(first);
                TryDelete(second);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should store postData for global request")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStorePostDataForGlobalRequest()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            string path = TempZip();
            string url = TestConstants.ServerUrl + "/simple.json";
            try
            {
                await using IAPIRequestContext request = await Playwright.APIRequest.NewContextAsync().ConfigureAwait(false);
                await request.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await request.PostAsync(url, new() { Data = "test" }).ConfigureAwait(false);
                await request.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);

                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                List<JsonElement> actions = ResourceSnapshots(parsed);
                Assert.That(actions, Has.Count.EqualTo(1));
                JsonElement req = actions[0].GetProperty("snapshot").GetProperty("request");
                Assert.That(req.GetProperty("postData").GetProperty("_file").GetString(), Is.Not.Null.And.Not.Empty);
                Assert.That(req.GetProperty("method").GetString(), Is.EqualTo("POST"));
                Assert.That(req.GetProperty("url").GetString(), Is.EqualTo(url));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not flush console events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldNotFlushConsoleEvents()
        {
            Assert.Ignore("Node-only: artifactsFolderName / worker traces dir.");
        }

        [PlaywrightTest("tracing.spec.ts", "should flush console events on tracing stop")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFlushConsoleEventsOnTracingStop()
        {
            string path = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions()).ConfigureAwait(false);
                TaskCompletionSource<bool> done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                int counter = 0;
                page.Console += (_, _) =>
                {
                    if (++counter == 100)
                    {
                        done.TrySetResult(true);
                    }
                };
                await page.EvaluateAsync(@"() => {
    setTimeout(() => {
      for (let i = 0; i < 100; ++i)
        console.log('hello ' + i);
    });
  }").ConfigureAwait(false);
                await done.Task.ConfigureAwait(false);
                await context.Tracing.StopAsync(new TracingStopOptions { Path = path }).ConfigureAwait(false);
                OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(path);
                int consoles = 0;
                foreach (JsonElement item in parsed.Events)
                {
                    if (item.TryGetProperty("type", out JsonElement type) && type.GetString() == "console")
                    {
                        consoles++;
                    }
                }

                Assert.That(consoles, Is.EqualTo(100));
            }
            finally
            {
                TryDelete(path);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should not emit after w/o before")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotEmitAfterWithoutBefore()
        {
            string tracesDir = Path.Combine(Path.GetTempPath(), "pwsharp-traces-" + Path.GetRandomFileName());
            string first = TempZip();
            string second = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync(new BrowserTypeLaunchOptions { TracesDir = tracesDir }).ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Name = "name1", Snapshots = true }).ConfigureAwait(false);
                Task evaluatePromise = page.EvaluateAsync(@"() => {
    console.log('started');
    return new Promise(f => window.callback = f);
  }");
                await page.WaitForEventAsync(PageEvent.Console).ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(first).ConfigureAwait(false);
                Assert.That(File.Exists(Path.Combine(tracesDir, "name1.trace")), Is.True);

                await context.Tracing.StartChunkAsync(new() { Name = "name2" }).ConfigureAwait(false);
                await page.EvaluateHandleAsync("() => window.callback()").ConfigureAwait(false);
                try
                {
                    await evaluatePromise.ConfigureAwait(false);
                }
                catch (PlaywrightSharpException)
                {
                }

                await context.Tracing.StopAsync(new TracingStopOptions { Path = second }).ConfigureAwait(false);
                Assert.That(File.Exists(Path.Combine(tracesDir, "name2.trace")), Is.True);

                int minCallId = 100000;
                List<Dictionary<string, object>> Sanitize(OfficialTraceParser.ParsedTrace parsed)
                {
                    List<Dictionary<string, object>> sanitized = new List<Dictionary<string, object>>();
                    foreach (JsonElement item in parsed.Events)
                    {
                        if (!item.TryGetProperty("type", out JsonElement type))
                        {
                            continue;
                        }

                        string typeName = type.GetString();
                        if (typeName != "after" && typeName != "before")
                        {
                            continue;
                        }

                        int id = int.Parse(item.GetProperty("callId").GetString().Split('@')[1], System.Globalization.CultureInfo.InvariantCulture);
                        minCallId = Math.Min(minCallId, id);
                        var row = new Dictionary<string, object>
                        {
                            ["type"] = typeName,
                            ["callId"] = id,
                        };
                        if (item.TryGetProperty("title", out JsonElement title) && title.ValueKind == JsonValueKind.String)
                        {
                            row["title"] = title.GetString();
                        }

                        sanitized.Add(row);
                    }

                    foreach (Dictionary<string, object> row in sanitized)
                    {
                        row["callId"] = (int)row["callId"] - minCallId;
                    }

                    return sanitized;
                }

                OfficialTraceParser.ParsedTrace trace1 = OfficialTraceParser.Parse(first);
                List<Dictionary<string, object>> sanitized1 = Sanitize(trace1);
                Assert.That(sanitized1, Has.Count.EqualTo(3));
                Assert.That(sanitized1[0]["type"], Is.EqualTo("before"));
                Assert.That(sanitized1[1]["type"], Is.EqualTo("before"));
                Assert.That(sanitized1[1]["title"], Is.EqualTo("Wait for event \"console\""));
                Assert.That(sanitized1[2]["type"], Is.EqualTo("after"));
                int call1 = (int)sanitized1[0]["callId"];
                Assert.That(sanitized1[1]["callId"], Is.EqualTo(sanitized1[2]["callId"]));

                OfficialTraceParser.ParsedTrace trace2 = OfficialTraceParser.Parse(second);
                List<Dictionary<string, object>> sanitized2 = Sanitize(trace2);
                Assert.That(sanitized2, Has.Count.EqualTo(2));
                Assert.That(sanitized2[0]["type"], Is.EqualTo("before"));
                Assert.That(sanitized2[1]["type"], Is.EqualTo("after"));
                int call2Before = (int)sanitized2[0]["callId"];
                int call2After = (int)sanitized2[1]["callId"];
                Assert.That(call2Before, Is.GreaterThan(call1));
                Assert.That(call2After, Is.EqualTo(call2Before));
            }
            finally
            {
                TryDelete(first);
                TryDelete(second);
                TryDeleteDir(tracesDir);
            }
        }

        [PlaywrightTest("tracing.spec.ts", "should save trace while a WebSocket keeps streaming frames")]
        [Test]
        [Timeout(60_000)]
        public async Task ShouldSaveTraceWhileAWebSocketKeepsStreamingFrames()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            bool streaming = true;
            Server.OnceWebSocketConnection(async ws =>
            {
                byte[] payload = Encoding.UTF8.GetBytes(new string('x', 16 * 1024));
                while (streaming && ws.State == WebSocketState.Open)
                {
                    try
                    {
                        await ws.SendAsync(payload, WebSocketMessageType.Text, true, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (WebSocketException)
                    {
                        break;
                    }
                    catch (ObjectDisposedException)
                    {
                        break;
                    }

                    await Task.Delay(1).ConfigureAwait(false);
                }
            });

            string first = TempZip();
            string second = TempZip();
            try
            {
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                await context.Tracing.StartAsync(new TracingStartOptions { Snapshots = true }).ConfigureAwait(false);
                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                string wsUrl = TestConstants.ServerUrl.Replace("http://", "ws://", StringComparison.Ordinal) + "/ws";
                await page.EvaluateAsync(
                    @"url => {
                        window.ws = new WebSocket(url);
                        return new Promise(resolve => window.ws.addEventListener('open', () => resolve()));
                    }",
                    wsUrl).ConfigureAwait(false);
                await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(first).ConfigureAwait(false);

                streaming = false;
                await context.Tracing.StartChunkAsync().ConfigureAwait(false);
                await page.WaitForTimeoutAsync(100).ConfigureAwait(false);
                await context.Tracing.StopChunkAsync(second).ConfigureAwait(false);

                await page.EvaluateAsync(@"() => new Promise(resolve => {
    const ws = window.ws;
    if (ws.readyState === WebSocket.CLOSED) {
      resolve();
      return;
    }
    ws.addEventListener('close', () => resolve(), { once: true });
    ws.close();
  })").ConfigureAwait(false);

                Assert.That(first, Is.Not.EqualTo(second));
                foreach (string zip in new[] { first, second })
                {
                    OfficialTraceParser.ParsedTrace parsed = OfficialTraceParser.Parse(zip);
                    KeyValuePair<string, byte[]> websocket = default;
                    foreach (KeyValuePair<string, byte[]> item in parsed.Resources)
                    {
                        if (item.Key.EndsWith(".jsonl", StringComparison.Ordinal))
                        {
                            websocket = item;
                            break;
                        }
                    }

                    Assert.That(websocket.Key, Is.Not.Null);
                    string[] lines = Encoding.UTF8.GetString(websocket.Value).Split('\n', StringSplitOptions.RemoveEmptyEntries);
                    Assert.That(lines.Length, Is.GreaterThan(0));
                    foreach (string line in lines)
                    {
                        Assert.DoesNotThrow(() => JsonDocument.Parse(line).Dispose());
                    }
                }
            }
            finally
            {
                streaming = false;
                TryDelete(first);
                TryDelete(second);
            }
        }

        private static JsonElement? FindResource(OfficialTraceParser.ParsedTrace parsed, string suffix)
        {
            foreach (JsonElement item in parsed.Events)
            {
                if (!item.TryGetProperty("type", out JsonElement type) || type.GetString() != "resource-snapshot")
                {
                    continue;
                }

                if (item.TryGetProperty("snapshot", out JsonElement snapshot)
                    && snapshot.TryGetProperty("request", out JsonElement request)
                    && request.TryGetProperty("url", out JsonElement url)
                    && url.GetString() != null
                    && url.GetString().EndsWith(suffix, StringComparison.Ordinal))
                {
                    return item;
                }
            }

            return null;
        }

        private static bool HasEvent(OfficialTraceParser.ParsedTrace parsed, string type)
        {
            foreach (JsonElement item in parsed.Events)
            {
                if (item.TryGetProperty("type", out JsonElement value)
                    && value.GetString() == type)
                {
                    return true;
                }
            }

            return false;
        }

        private static string TempZip()
            => Path.Combine(Path.GetTempPath(), "pwsharp-trace-" + Path.GetRandomFileName() + ".zip");

        private static JsonElement FindBefore(OfficialTraceParser.ParsedTrace parsed, string method)
        {
            foreach (JsonElement item in parsed.Events)
            {
                if (item.TryGetProperty("type", out JsonElement type)
                    && type.GetString() == "before"
                    && item.TryGetProperty("method", out JsonElement value)
                    && value.GetString() == method)
                {
                    return item;
                }
            }

            throw new AssertionException("missing before event for " + method);
        }

        private static bool HasButton(JsonElement node)
        {
            if (node.ValueKind == JsonValueKind.Object)
            {
                if (node.TryGetProperty("role", out JsonElement role)
                    && role.GetString() == "button"
                    && node.TryGetProperty("name", out JsonElement name)
                    && name.GetString() == "Click target")
                {
                    return true;
                }

                if (node.TryGetProperty("children", out JsonElement children)
                    && children.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement child in children.EnumerateArray())
                    {
                        if (HasButton(child))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }

        private static string[] ResourceNames(OfficialTraceParser.ParsedTrace parsed)
        {
            List<string> names = new List<string>();
            foreach (string file in parsed.Resources.Keys)
            {
                names.Add(System.Text.RegularExpressions.Regex.Replace(file, @"^resources/.*\.(html|css)$", "resources/XXX.$1"));
            }

            names.Sort(StringComparer.Ordinal);
            return names.ToArray();
        }

        private static int CountSuffix(List<string> names, string suffix)
        {
            int count = 0;
            foreach (string name in names)
            {
                if (name.EndsWith(suffix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int CountPrefix(List<string> names, string prefix)
        {
            int count = 0;
            foreach (string name in names)
            {
                if (name.StartsWith(prefix, StringComparison.Ordinal))
                {
                    count++;
                }
            }

            return count;
        }

        private static int PngWidth(byte[] png)
        {
            if (png == null || png.Length < 24)
            {
                return 0;
            }

            return (png[16] << 24) | (png[17] << 16) | (png[18] << 8) | png[19];
        }

        private static List<JsonElement> ResourceSnapshots(OfficialTraceParser.ParsedTrace parsed)
        {
            List<JsonElement> list = new List<JsonElement>();
            foreach (JsonElement item in parsed.Events)
            {
                if (item.TryGetProperty("type", out JsonElement type) && type.GetString() == "resource-snapshot")
                {
                    list.Add(item);
                }
            }

            return list;
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static void TryDeleteDir(string path)
        {
            try
            {
                if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
                {
                    Directory.Delete(path, recursive: true);
                }
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }
    }
}
