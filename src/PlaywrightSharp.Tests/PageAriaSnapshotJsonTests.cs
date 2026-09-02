/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page.ariaSnapshotJSON()</c>.
    /// </summary>
    [TestFixture]
    public class PageAriaSnapshotJsonTests : PageTestEx
    {
        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "Page AriaSnapshotJSON includes the button")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotJsonShouldIncludeTheButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string json = await page.AriaSnapshotJsonAsync().ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(json, Does.Contain("\"role\":\"button\""));
            Assert.That(json, Does.Contain("\"name\":\"Go\""));
            Assert.That(json, Does.Not.Contain("\"ref\""));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "Page AriaSnapshotJSON AI mode adds refs")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotJsonAiModeShouldAddRefs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string json = await page.AriaSnapshotJsonAsync(mode: AriaSnapshotMode.Ai).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
            Assert.That(json, Does.Contain("\"role\":\"button\""));
            Assert.That(json, Does.Contain("\"ref\":\"e"));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "Page AriaSnapshotJSON boxes")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotJsonShouldAddBoxes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id='go'>Go</button>").ConfigureAwait(false);

            string json = await page.AriaSnapshotJsonAsync(boxes: true).ConfigureAwait(false);
            using JsonDocument document = JsonDocument.Parse(json);
            Assert.That(json, Does.Contain("\"box\""));
            Assert.That(json, Does.Contain("\"width\""));
            Assert.That(json, Does.Contain("\"height\""));
            Assert.That(document.RootElement.ValueKind, Is.EqualTo(JsonValueKind.Array));
        }

        [PlaywrightTest("page-aria-snapshot-json.spec.ts", "Page AriaSnapshotJSON honors depth")]
        [Test]
        [Timeout(30_000)]
        public async Task PageAriaSnapshotJsonShouldHonorDepth()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button>Go</button>").ConfigureAwait(false);

            string rootOnly = await page.AriaSnapshotJsonAsync(depth: 0).ConfigureAwait(false);
            Assert.That(rootOnly, Does.Not.Contain("\"role\":\"button\""));

            string json = await page.AriaSnapshotJsonAsync().ConfigureAwait(false);
            Assert.That(json, Does.Contain("\"role\":\"button\""));
        }
    }
}
