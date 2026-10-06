/*
 * Copyright (c) Microsoft Corporation.
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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/webmcp.spec.ts</c> parity. The browser is launched
    /// with WebMCP enabled (<c>--enable-features=WebMCP</c>, or the
    /// <c>dom.modelcontext.*</c> preferences on Firefox).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryWebmcpParityTests : PlaywrightTestEx
    {
        private const string AddTool = @"
  modelContext.registerTool({
    name: 'add',
    description: 'Adds two numbers',
    inputSchema: { type: 'object', properties: { a: { type: 'number' }, b: { type: 'number' } }, required: ['a', 'b'] },
    annotations: { readOnlyHint: true },
    async execute(input) { return { content: [{ type: 'text', text: String(input.a + input.b) }] }; },
  });
";

        private const string AddSchema = "{ \"type\": \"object\", \"properties\": { \"a\": { \"type\": \"number\" }, \"b\": { \"type\": \"number\" } }, \"required\": [\"a\", \"b\"] }";

        private const string IframeSkipReason = "Firefox does not support registering WebMCP tools in iframes yet, https://bugzilla.mozilla.org/show_bug.cgi?id=2019743";

        // Keyed by test id: a timed-out test can still run its TearDown later,
        // and it must close its own context, not the one of the next test.
        private readonly ConcurrentDictionary<string, IBrowserContext> _contexts = new();

        private IBrowser _browser;

        private IPage Page { get; set; }

        [OneTimeSetUp]
        public async Task LaunchWebMCPBrowserAsync()
        {
            if (TestConstants.IsWebKit)
            {
                return;
            }

            BrowserTypeLaunchOptions options = TestConstants.IsFirefox
                ? new BrowserTypeLaunchOptions
                {
                    FirefoxUserPrefs = new Dictionary<string, object>
                    {
                        ["dom.modelcontext.enabled"] = true,
                        ["dom.modelcontext.testing.enabled"] = true,
                    },
                }
                : new BrowserTypeLaunchOptions { Args = ["--enable-features=WebMCP"] };
            _browser = await BrowserLauncher.LaunchAsync(options).ConfigureAwait(false);
        }

        [OneTimeTearDown]
        public async Task CloseWebMCPBrowserAsync()
        {
            if (_browser != null)
            {
                await _browser.CloseAsync().ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task NewPageAsync()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit does not implement WebMCP");
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            IBrowserContext context = await _browser.NewContextAsync().ConfigureAwait(false);
            _contexts[TestContext.CurrentContext.Test.ID] = context;
            Page = await context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task CloseContextAsync()
        {
            if (_contexts.TryRemove(TestContext.CurrentContext.Test.ID, out IBrowserContext context))
            {
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("webmcp.spec.ts", "should list tools registered by the page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldListToolsRegisteredByThePage()
        {
            Serve("/", RegisterScript(AddTool + @"
    modelContext.registerTool({
      name: 'noop',
      description: 'Does nothing',
      async execute() {},
    });
  "));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            // Browsers list tools in their own order.
            List<WebMCPTool> tools = (await Page.Webmcp().ToolsAsync().ConfigureAwait(false)).OrderBy(tool => tool.Name, StringComparer.Ordinal).ToList();
            Assert.That(tools, Has.Count.EqualTo(2));
            Assert.That(tools[0].Name, Is.EqualTo("add"));
            Assert.That(tools[0].Description, Is.EqualTo("Adds two numbers"));
            AssertJson(tools[0].InputSchema, AddSchema);
            Assert.That(tools[0].Annotations, Is.Not.Null);
            Assert.That(tools[0].Annotations.ReadOnly, Is.True);
            Assert.That(tools[0].Annotations.UntrustedContent, Is.Null);
            Assert.That(tools[0].Annotations.Consequential, Is.Null);
            AssertNameAndDescriptionOnly(tools[1], "noop", "Does nothing");
        }

        [PlaywrightTest("webmcp.spec.ts", "should report no tools when the page registers none")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReportNoToolsWhenThePageRegistersNone()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.Webmcp().ToolsAsync().ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("webmcp.spec.ts", "should call a tool")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCallATool()
        {
            Serve("/", RegisterScript(AddTool));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            AssertJson(
                await Page.Webmcp().CallToolAsync("add", new { a = 2, b = 40 }).ConfigureAwait(false),
                "{ \"content\": [{ \"type\": \"text\", \"text\": \"42\" }] }");
        }

        [PlaywrightTest("webmcp.spec.ts", "should return isError results as is")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldReturnIsErrorResultsAsIs()
        {
            Serve("/", RegisterScript(@"
    modelContext.registerTool({
      name: 'broken',
      description: 'Always fails',
      async execute() { return { content: [{ type: 'text', text: 'nope' }], isError: true }; },
    });
  "));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            AssertJson(
                await Page.Webmcp().CallToolAsync("broken").ConfigureAwait(false),
                "{ \"content\": [{ \"type\": \"text\", \"text\": \"nope\" }], \"isError\": true }");
        }

        [PlaywrightTest("webmcp.spec.ts", "should throw when the tool throws")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowWhenTheToolThrows()
        {
            Serve("/", RegisterScript(@"
    modelContext.registerTool({
      name: 'throwing',
      description: 'Throws',
      async execute() { throw new Error('boom'); },
    });
  "));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => Page.Webmcp().CallToolAsync("throwing"));

            // Chromium does not report the error that the tool threw.
            Assert.That(error.Message, Does.Contain(TestConstants.IsChromium ? "the invocation failed" : "boom"));
        }

        [PlaywrightTest("webmcp.spec.ts", "should throw for an unknown tool")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForAnUnknownTool()
        {
            Serve("/", RegisterScript(AddTool));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => Page.Webmcp().CallToolAsync("missing"));
            Assert.That(error.Message, Does.Contain("No WebMCP tool named \"missing\""));
            Assert.That(error.Message, Does.Contain("Available tools: add"));
        }

        [PlaywrightTest("webmcp.spec.ts", "should time out when the tool does not settle")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeOutWhenTheToolDoesNotSettle()
        {
            Serve("/", RegisterScript(@"
    modelContext.registerTool({
      name: 'stuck',
      description: 'Never resolves',
      execute() { return new Promise(() => {}); },
    });
  "));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            Exception error = Assert.CatchAsync(() => Page.Webmcp().CallToolAsync("stuck", new { }, timeout: 500));
            Assert.That(error.Message, Does.Contain("Timeout 500ms exceeded"));
        }

        [PlaywrightTest("webmcp.spec.ts", "should drop tools after navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDropToolsAfterNavigation()
        {
            Serve("/", RegisterScript(AddTool));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);
            Assert.That(await Page.Webmcp().ToolsAsync().ConfigureAwait(false), Has.Count.EqualTo(1));

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(await Page.Webmcp().ToolsAsync().ConfigureAwait(false), Is.Empty);
            Exception error = Assert.CatchAsync(() => Page.Webmcp().CallToolAsync("add", new { a = 1, b = 1 }));
            Assert.That(error.Message, Does.Contain("The frame does not register any WebMCP tools"));
        }

        [PlaywrightTest("webmcp.spec.ts", "should list a tool registered after load")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldListAToolRegisteredAfterLoad()
        {
            Serve("/", RegisterScript(AddTool));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);
            Assert.That(await Page.Webmcp().ToolsAsync().ConfigureAwait(false), Has.Count.EqualTo(1));

            await Page.EvaluateAsync(@"() => {
    const modelContext = document.modelContext || navigator.modelContext;
    modelContext.registerTool({ name: 'late', description: 'Registered later', async execute() {} });
  }").ConfigureAwait(false);
            IEnumerable<string> names = (await Page.Webmcp().ToolsAsync().ConfigureAwait(false)).Select(tool => tool.Name).Order(StringComparer.Ordinal);
            Assert.That(names, Is.EqualTo(new[] { "add", "late" }));
        }

        [PlaywrightTest("webmcp.spec.ts", "should list tools while a tool is running")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldListToolsWhileAToolIsRunning()
        {
            Serve("/", RegisterScript(AddTool + @"
    modelContext.registerTool({
      name: 'gated',
      description: 'Resolves when released',
      execute() { return new Promise(resolve => window.release = () => resolve({ content: [{ type: 'text', text: 'released' }] })); },
    });
  "));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            Task<JsonElement?> result = Page.Webmcp().CallToolAsync("gated");
            await Page.WaitForFunctionAsync("() => !!window.release").ConfigureAwait(false);
            Assert.That(await Page.Webmcp().ToolsAsync().ConfigureAwait(false), Has.Count.EqualTo(2));
            await Page.EvaluateAsync("() => window.release()").ConfigureAwait(false);
            AssertJson(await result.ConfigureAwait(false), "{ \"content\": [{ \"type\": \"text\", \"text\": \"released\" }] }");
        }

        [PlaywrightTest("webmcp.spec.ts", "should list declarative tools")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldListDeclarativeTools()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Only Chromium implements declarative tools");
            }

            Serve("/", @"
    <form toolname=""subscribe"" tooldescription=""Subscribes to the newsletter"">
      <input name=""email"" type=""email"">
      <button>Subscribe</button>
    </form>
  ");
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            IReadOnlyList<WebMCPTool> tools = await Page.Webmcp().ToolsAsync().ConfigureAwait(false);
            Assert.That(tools, Has.Count.EqualTo(1));
            Assert.That(tools[0].Name, Is.EqualTo("subscribe"));
            Assert.That(tools[0].Description, Is.EqualTo("Subscribes to the newsletter"));
            Assert.That(tools[0].InputSchema, Is.Not.Null);
            Assert.That(tools[0].InputSchema.Value.GetProperty("type").GetString(), Is.EqualTo("object"));
            AssertJson(tools[0].InputSchema.Value.GetProperty("properties"), "{ \"email\": { \"type\": \"string\" } }");

            // A declarative tool waits for the user to submit the form.
            Exception error = Assert.CatchAsync(() => Page.Webmcp().CallToolAsync("subscribe", new { email = "me@example.com" }, timeout: 500));
            Assert.That(error.Message, Does.Contain("Timeout 500ms exceeded"));
        }

        [PlaywrightTest("webmcp.spec.ts", "should scope tools to their frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldScopeToolsToTheirFrame()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore(IframeSkipReason);
            }

            Serve("/", RegisterScript(AddTool) + "<iframe src=\"/frame.html\"></iframe>");
            Serve("/frame.html", RegisterScript(@"
    modelContext.registerTool({
      name: 'subscribe',
      description: 'Subscribes to the newsletter',
      async execute() { return { content: [{ type: 'text', text: 'subscribed' }] }; },
    });
  "));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            IFrame childFrame = Page.Frames[1];
            Assert.That((await Page.Webmcp().ToolsAsync().ConfigureAwait(false)).Select(tool => tool.Name), Is.EqualTo(new[] { "add" }));
            Assert.That((await childFrame.Webmcp().ToolsAsync().ConfigureAwait(false)).Select(tool => tool.Name), Is.EqualTo(new[] { "subscribe" }));
            AssertJson(
                await childFrame.Webmcp().CallToolAsync("subscribe").ConfigureAwait(false),
                "{ \"content\": [{ \"type\": \"text\", \"text\": \"subscribed\" }] }");

            Exception error = Assert.CatchAsync(() => Page.Webmcp().CallToolAsync("subscribe"));
            Assert.That(error.Message, Does.Contain("No WebMCP tool named \"subscribe\""));
            Assert.That(error.Message, Does.Contain("Available tools: add"));
        }

        [PlaywrightTest("webmcp.spec.ts", "should call same-name tools in their own frame")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCallSameNameToolsInTheirOwnFrame()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore(IframeSkipReason);
            }

            static string RegisterEcho(string text) => RegisterScript($@"
    modelContext.registerTool({{
      name: 'echo',
      description: 'Echoes',
      async execute() {{ return {{ content: [{{ type: 'text', text: '{text}' }}] }}; }},
    }});
  ");
            Serve("/", RegisterEcho("main") + "<iframe src=\"/frame.html\"></iframe>");
            Serve("/frame.html", RegisterEcho("frame"));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);
            IFrame childFrame = Page.Frames[1];

            AssertJson(await Page.Webmcp().CallToolAsync("echo").ConfigureAwait(false), "{ \"content\": [{ \"type\": \"text\", \"text\": \"main\" }] }");
            AssertJson(await childFrame.Webmcp().CallToolAsync("echo").ConfigureAwait(false), "{ \"content\": [{ \"type\": \"text\", \"text\": \"frame\" }] }");
        }

        [PlaywrightTest("webmcp.spec.ts", "should list and call tools in cross-origin and nested frames")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldListAndCallToolsInCrossOriginAndNestedFrames()
        {
            if (TestConstants.IsFirefox)
            {
                Assert.Ignore(IframeSkipReason);
            }

            static string RegisterEcho(string text) => RegisterScript($@"
    modelContext.registerTool({{
      name: 'echo-{text}',
      description: 'Echoes {text}',
      async execute() {{ return {{ content: [{{ type: 'text', text: '{text}' }}] }}; }},
    }});
  ");
            Serve("/", RegisterEcho("main") + $"<iframe allow=\"tools\" src=\"{TestConstants.CrossProcessHttpPrefix}/frame.html\"></iframe>");
            Serve("/frame.html", RegisterEcho("frame") + "<iframe src=\"/nested.html\"></iframe>");
            Serve("/nested.html", RegisterEcho("nested"));
            await Page.GoToAsync(TestConstants.ServerUrl + "/").ConfigureAwait(false);

            IFrame mainFrame = Page.Frames[0];
            IFrame frame = Page.Frames[1];
            IFrame nested = Page.Frames[2];
            AssertNameAndDescriptionOnly((await mainFrame.Webmcp().ToolsAsync().ConfigureAwait(false)).Single(), "echo-main", "Echoes main");
            AssertNameAndDescriptionOnly((await frame.Webmcp().ToolsAsync().ConfigureAwait(false)).Single(), "echo-frame", "Echoes frame");
            AssertNameAndDescriptionOnly((await nested.Webmcp().ToolsAsync().ConfigureAwait(false)).Single(), "echo-nested", "Echoes nested");
            AssertJson(await frame.Webmcp().CallToolAsync("echo-frame").ConfigureAwait(false), "{ \"content\": [{ \"type\": \"text\", \"text\": \"frame\" }] }");
            AssertJson(await nested.Webmcp().CallToolAsync("echo-nested").ConfigureAwait(false), "{ \"content\": [{ \"type\": \"text\", \"text\": \"nested\" }] }");
        }

        private static string RegisterScript(string tools)
            => $@"<script>
    const modelContext = document.modelContext || navigator.modelContext;
    {tools}
  </script>";

        private static void AssertJson(JsonElement? actual, string expected)
        {
            Assert.That(actual, Is.Not.Null);
            Assert.That(
                JsonNode.DeepEquals(JsonNode.Parse(actual.Value.GetRawText()), JsonNode.Parse(expected)),
                Is.True,
                $"Expected {expected} but was {actual.Value.GetRawText()}");
        }

        private static void AssertNameAndDescriptionOnly(WebMCPTool tool, string name, string description)
        {
            Assert.That(tool.Name, Is.EqualTo(name));
            Assert.That(tool.Description, Is.EqualTo(description));
            Assert.That(tool.InputSchema, Is.Null);
            Assert.That(tool.Annotations, Is.Null);
        }

        private void Serve(string path, string body)
            => Server.SetRoute(path, http =>
            {
                http.Response.StatusCode = 200;
                http.Response.ContentType = "text/html";
                return http.Response.WriteAsync("<title>WebMCP</title>" + body);
            });
    }
}
