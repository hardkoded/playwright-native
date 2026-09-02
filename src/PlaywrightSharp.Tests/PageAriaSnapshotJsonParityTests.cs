/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-aria-snapshot-json.spec.ts</c> parity.
    /// Do not edit leftover <c>PageAriaSnapshotJsonTests.cs</c> or
    /// <c>LocatorAriaSnapshotJsonTests.cs</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class PageAriaSnapshotJsonParityTests : PageTestEx
    {
        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [SetUp]
        public async Task SetUpAsync()
        {
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            try
            {
                if (_context != null)
                {
                    await _context.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                if (_browser != null)
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }
        }

        private IPage Page => _page;

        private static void AssertJsonEqual(string actual, string expected)
        {
            using JsonDocument actualDoc = JsonDocument.Parse(actual);
            using JsonDocument expectedDoc = JsonDocument.Parse(expected);
            AssertJsonEqual(actualDoc.RootElement, expectedDoc.RootElement, "$");
        }

        private static void AssertJsonEqual(JsonElement actual, JsonElement expected, string path)
        {
            Assert.That(actual.ValueKind, Is.EqualTo(expected.ValueKind), path);
            switch (expected.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (JsonProperty prop in expected.EnumerateObject())
                    {
                        Assert.That(actual.TryGetProperty(prop.Name, out JsonElement value), Is.True, path + "." + prop.Name);
                        AssertJsonEqual(value, prop.Value, path + "." + prop.Name);
                    }

                    foreach (JsonProperty prop in actual.EnumerateObject())
                    {
                        Assert.That(expected.TryGetProperty(prop.Name, out _), Is.True, path + " extra " + prop.Name + "=" + prop.Value.GetRawText());
                    }

                    break;
                case JsonValueKind.Array:
                    Assert.That(actual.GetArrayLength(), Is.EqualTo(expected.GetArrayLength()), path + ".length");
                    int index = 0;
                    foreach (JsonElement expectedItem in expected.EnumerateArray())
                    {
                        AssertJsonEqual(actual[index], expectedItem, path + "[" + index.ToString(System.Globalization.CultureInfo.InvariantCulture) + "]");
                        index++;
                    }

                    break;
                case JsonValueKind.String:
                    Assert.That(actual.GetString(), Is.EqualTo(expected.GetString()), path);
                    break;
                case JsonValueKind.Number:
                    Assert.That(actual.GetRawText(), Is.EqualTo(expected.GetRawText()), path);
                    break;
                case JsonValueKind.True:
                case JsonValueKind.False:
                case JsonValueKind.Null:
                    break;
                default:
                    Assert.Fail(path + " unexpected kind " + expected.ValueKind);
                    break;
            }
        }

        private static JsonElement? FindNode(JsonElement nodes, Func<JsonElement, bool> predicate)
        {
            if (nodes.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement node in nodes.EnumerateArray())
                {
                    JsonElement? hit = FindNode(node, predicate);
                    if (hit != null)
                    {
                        return hit;
                    }
                }

                return null;
            }

            if (nodes.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (predicate(nodes))
            {
                return nodes;
            }

            if (nodes.TryGetProperty("children", out JsonElement children))
            {
                return FindNode(children, predicate);
            }

            return null;
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot roles, names and text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotRolesNamesAndText()
        {
            await Page.SetContentAsync(@"
    <h1>title</h1>
    <ul aria-label=""my list"">
      <li>one</li>
      <li>two</li>
    </ul>
  ").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync().ConfigureAwait(false),
                @"[
  { ""role"": ""heading"", ""name"": ""title"", ""level"": 1 },
  {
    ""role"": ""list"",
    ""name"": ""my list"",
    ""children"": [
      { ""role"": ""listitem"", ""text"": ""one"" },
      { ""role"": ""listitem"", ""text"": ""two"" }
    ]
  }
]");
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot flags as properties")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotFlagsAsProperties()
        {
            await Page.SetContentAsync(@"
    <input type=""checkbox"" title=""Check"" checked>
    <button disabled>Click</button>
  ").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync().ConfigureAwait(false),
                @"[
  { ""role"": ""checkbox"", ""name"": ""Check"", ""checked"": true },
  { ""role"": ""button"", ""name"": ""Click"", ""disabled"": true }
]");
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot link url and textbox value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotLinkUrlAndTextboxValue()
        {
            await Page.SetContentAsync(@"
    <a href=""https://example.com/"">Link</a>
    <input title=""Input"" value=""hello"">
  ").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync().ConfigureAwait(false),
                @"[
  { ""role"": ""link"", ""name"": ""Link"", ""url"": ""https://example.com/"" },
  { ""role"": ""textbox"", ""name"": ""Input"", ""text"": ""hello"" }
]");
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot text fragments in children")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotTextFragmentsInChildren()
        {
            await Page.SetContentAsync(@"<p>Hello <a href=""/link"">world</a> again</p>").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync().ConfigureAwait(false),
                @"[
  {
    ""role"": ""paragraph"",
    ""children"": [
      ""Hello"",
      { ""role"": ""link"", ""name"": ""world"", ""url"": ""/link"" },
      ""again""
    ]
  }
]");
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot top-level text fragments as text nodes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotTopLevelTextFragmentsAsTextNodes()
        {
            await Page.SetContentAsync(@"Hello <button>One</button> again").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync().ConfigureAwait(false),
                @"[
  { ""role"": ""text"", ""text"": ""Hello"" },
  { ""role"": ""button"", ""name"": ""One"" },
  { ""role"": ""text"", ""text"": ""again"" }
]");
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should generate refs in ai mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldGenerateRefsInAiMode()
        {
            await Page.SetContentAsync(@"
    <button>One</button>
    <button>Two</button>
  ").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync(mode: AriaSnapshotMode.Ai).ConfigureAwait(false),
                @"[
  {
    ""role"": ""generic"",
    ""active"": true,
    ""ref"": ""e1"",
    ""children"": [
      { ""role"": ""button"", ""name"": ""One"", ""ref"": ""e2"" },
      { ""role"": ""button"", ""name"": ""Two"", ""ref"": ""e3"" }
    ]
  }
]");
            await Assertions.Expect(Page.Locator("aria-ref=e2")).ToHaveTextAsync("One").ConfigureAwait(false);
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should mark clickable elements with cursor in ai mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldMarkClickableElementsWithCursorInAiMode()
        {
            await Page.SetContentAsync(@"<button style=""cursor: pointer"">One</button>").ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(
                await Page.AriaSnapshotJsonAsync(mode: AriaSnapshotMode.Ai).ConfigureAwait(false));
            JsonElement? button = FindNode(document.RootElement, node => node.TryGetProperty("role", out JsonElement role) && role.GetString() == "button");
            Assert.That(button, Is.Not.Null);
            Assert.That(button.Value.TryGetProperty("cursor", out JsonElement cursor), Is.True);
            Assert.That(cursor.GetString(), Is.EqualTo("pointer"));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot iframes in ai mode")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotIframesInAiMode()
        {
            await Page.SetContentAsync(@"
    <h1>Hello</h1>
    <iframe srcdoc=""<button>In frame</button>""></iframe>
  ").ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(
                await Page.AriaSnapshotJsonAsync(mode: AriaSnapshotMode.Ai).ConfigureAwait(false));
            JsonElement? iframe = FindNode(document.RootElement, node => node.TryGetProperty("role", out JsonElement role) && role.GetString() == "iframe");
            Assert.That(iframe, Is.Not.Null);
            Assert.That(iframe.Value.TryGetProperty("ref", out JsonElement iframeRef) && !string.IsNullOrEmpty(iframeRef.GetString()), Is.True);
            Assert.That(iframe.Value.TryGetProperty("children", out JsonElement children), Is.True);
            JsonElement? button = FindNode(children, node => node.TryGetProperty("role", out JsonElement role) && role.GetString() == "button");
            Assert.That(button, Is.Not.Null);
            Assert.That(button.Value.GetProperty("name").GetString(), Is.EqualTo("In frame"));
            Assert.That(button.Value.GetProperty("ref").GetString(), Does.Match(new Regex(@"^f\d+e\d+$")));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should limit depth")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldLimitDepth()
        {
            await Page.SetContentAsync(@"<ul><li><button>One</button></li></ul>").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.AriaSnapshotJsonAsync(depth: 1).ConfigureAwait(false),
                @"[
  {
    ""role"": ""list"",
    ""children"": [
      { ""role"": ""listitem"" }
    ]
  }
]");
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should include boxes when requested")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIncludeBoxesWhenRequested()
        {
            await Page.SetContentAsync(@"<button>One</button>").ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(
                await Page.AriaSnapshotJsonAsync(boxes: true).ConfigureAwait(false));
            JsonElement? button = FindNode(document.RootElement, node => node.TryGetProperty("role", out JsonElement role) && role.GetString() == "button");
            Assert.That(button, Is.Not.Null);
            JsonElement box = button.Value.GetProperty("box");
            Assert.That(box.TryGetProperty("x", out JsonElement x), Is.True);
            Assert.That(box.TryGetProperty("y", out JsonElement y), Is.True);
            Assert.That(box.TryGetProperty("width", out JsonElement width), Is.True);
            Assert.That(box.TryGetProperty("height", out JsonElement height), Is.True);
            Assert.That(x.ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(y.ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(width.GetInt32(), Is.GreaterThan(0));
            Assert.That(height.GetInt32(), Is.GreaterThan(0));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "should snapshot a locator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSnapshotALocator()
        {
            await Page.SetContentAsync(@"
    <h1>title</h1>
    <ul>
      <li>one</li>
      <li>two</li>
    </ul>
  ").ConfigureAwait(false);
            AssertJsonEqual(
                await Page.Locator("ul").AriaSnapshotJsonAsync().ConfigureAwait(false),
                @"[
  {
    ""role"": ""list"",
    ""children"": [
      { ""role"": ""listitem"", ""text"": ""one"" },
      { ""role"": ""listitem"", ""text"": ""two"" }
    ]
  }
]");
        }
    }
}
