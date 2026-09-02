/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>KeyboardModifier.ControlOrMeta</c> resolves to Control
    /// on Linux/Windows and Meta on macOS.
    /// </summary>
    [TestFixture]
    public class ControlOrMetaTests : PageTestEx
    {
        [PlaywrightTest("page-keyboard.spec.ts", "Page click honors ControlOrMeta")]
        [Test]
        [Timeout(30_000)]
        public async Task PageClickShouldHonorControlOrMeta()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\">go</button>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#b').addEventListener('click', e => { window.ctrl = e.ctrlKey; window.meta = e.metaKey; })")
                .ConfigureAwait(false);

            await page.ClickAsync("#b", new() { Modifiers = new List<KeyboardModifier> { KeyboardModifier.ControlOrMeta } })
                .ConfigureAwait(false);

            await AssertControlOrMetaAsync(page).ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "Frame click honors ControlOrMeta")]
        [Test]
        [Timeout(30_000)]
        public async Task FrameClickShouldHonorControlOrMeta()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);

            IFrame frame = null;
            foreach (IFrame child in page.MainFrame.ChildFrames)
            {
                frame = child;
                break;
            }

            Assert.That(frame, Is.Not.Null);
            await frame.SetContentAsync("<button id=\"b\">go</button>").ConfigureAwait(false);
            await frame.EvaluateAsync<object>(
                "document.querySelector('#b').addEventListener('click', e => { window.ctrl = e.ctrlKey; window.meta = e.metaKey; })")
                .ConfigureAwait(false);

            await frame.ClickAsync("#b", new() { Modifiers = new List<KeyboardModifier> { KeyboardModifier.ControlOrMeta } })
                .ConfigureAwait(false);

            await AssertControlOrMetaAsync(frame).ConfigureAwait(false);
        }

        [PlaywrightTest("page-keyboard.spec.ts", "Locator click honors ControlOrMeta")]
        [Test]
        [Timeout(30_000)]
        public async Task LocatorClickShouldHonorControlOrMeta()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<button id=\"b\">go</button>").ConfigureAwait(false);
            await page.EvaluateAsync<object>(
                "document.querySelector('#b').addEventListener('click', e => { window.ctrl = e.ctrlKey; window.meta = e.metaKey; })")
                .ConfigureAwait(false);

            await page.Locator("#b").ClickAsync(new() { Modifiers = new List<KeyboardModifier> { KeyboardModifier.ControlOrMeta } })
                .ConfigureAwait(false);

            await AssertControlOrMetaAsync(page).ConfigureAwait(false);
        }

        private static async Task AssertControlOrMetaAsync(IPage page)
        {
            bool ctrl = await page.EvaluateAsync<bool>("window.ctrl === true").ConfigureAwait(false);
            bool meta = await page.EvaluateAsync<bool>("window.meta === true").ConfigureAwait(false);
            AssertResolvedModifier(ctrl, meta);
        }

        private static async Task AssertControlOrMetaAsync(IFrame frame)
        {
            bool ctrl = await frame.EvaluateAsync<bool>("window.ctrl === true").ConfigureAwait(false);
            bool meta = await frame.EvaluateAsync<bool>("window.meta === true").ConfigureAwait(false);
            AssertResolvedModifier(ctrl, meta);
        }

        private static void AssertResolvedModifier(bool ctrl, bool meta)
        {
            if (OperatingSystem.IsMacOS())
            {
                Assert.That(meta, Is.True);
                Assert.That(ctrl, Is.False);
                return;
            }

            Assert.That(ctrl, Is.True);
            Assert.That(meta, Is.False);
        }
    }
}
