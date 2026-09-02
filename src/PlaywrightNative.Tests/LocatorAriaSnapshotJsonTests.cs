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
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>locator.ariaSnapshotJSON()</c>.
    /// </summary>
    [TestFixture]
    public class LocatorAriaSnapshotJsonTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "AriaSnapshotJSON includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task AriaSnapshotJsonShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string json = await page.Locator("#go").AriaSnapshotJsonAsync().ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(document.RootElement.GetArrayLength(), Is.EqualTo(1));
            JsonElement node = document.RootElement[0];
            Assert.That(node.GetProperty("role").GetString(), Is.EqualTo("button"));
            Assert.That(node.GetProperty("name").GetString(), Is.EqualTo("Go"));
            Assert.That(node.TryGetProperty("ref", out _), Is.False);
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "AI mode adds ref")]
        [Test]
        [Timeout(30_000)]
        public async Task AiModeShouldAddRef()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string json = await page.Locator("#go")
                .AriaSnapshotJsonAsync(mode: AriaSnapshotMode.Ai)
                .ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement node = document.RootElement[0];
            Assert.That(node.GetProperty("role").GetString(), Is.EqualTo("button"));
            Assert.That(node.GetProperty("ref").GetString(), Does.StartWith("e"));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "Boxes add a box object")]
        [Test]
        [Timeout(30_000)]
        public async Task BoxesShouldAddABoxObject()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string json = await page.Locator("#go")
                .AriaSnapshotJsonAsync(boxes: true)
                .ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement box = document.RootElement[0].GetProperty("box");
            Assert.That(box.TryGetProperty("x", out JsonElement x), Is.True);
            Assert.That(box.TryGetProperty("y", out JsonElement y), Is.True);
            Assert.That(box.TryGetProperty("width", out JsonElement width), Is.True);
            Assert.That(box.TryGetProperty("height", out JsonElement height), Is.True);
            Assert.That(width.GetInt32(), Is.GreaterThanOrEqualTo(0));
            Assert.That(height.GetInt32(), Is.GreaterThanOrEqualTo(0));
            Assert.That(x.ValueKind, Is.EqualTo(JsonValueKind.Number));
            Assert.That(y.ValueKind, Is.EqualTo(JsonValueKind.Number));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "Depth 0 is only the root")]
        [Test]
        [Timeout(30_000)]
        public async Task DepthZeroShouldBeOnlyTheRoot()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(
                    "<div role='menubar' id='root'>" +
                    "<div role='menu'><div role='menuitem'>alpha" +
                    "<div role='menu'><div role='menuitem'>omega</div></div>" +
                    "</div></div></div>")
                .ConfigureAwait(false);

            string json = await page.Locator("#root")
                .AriaSnapshotJsonAsync(depth: 0)
                .ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement node = document.RootElement[0];
            Assert.That(node.GetProperty("role").GetString(), Does.Contain("menu"));
            Assert.That(node.TryGetProperty("children", out _), Is.False);
            Assert.That(json, Does.Not.Contain("omega"));
        }
    }
}
