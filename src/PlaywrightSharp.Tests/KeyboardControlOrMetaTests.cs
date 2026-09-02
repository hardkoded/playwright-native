/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Direct-connection tests for <c>ControlOrMeta</c> keyboard chords.
    /// </summary>
    [TestFixture]
    public class KeyboardControlOrMetaTests : PageTestEx
    {
        [PlaywrightTest("page-keyboard.spec.ts", "ControlOrMeta presses Control on Linux")]
        [Test]
        [Timeout(30_000)]
        public async Task ControlOrMetaShouldPressThePlatformModifier()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
                <input id='t'>
                <script>
                  window.last = null;
                  document.addEventListener('keydown', e => {
                    window.last = { key: e.key, ctrl: e.ctrlKey, meta: e.metaKey };
                  });
                </script>").ConfigureAwait(false);

            await page.FocusAsync("#t").ConfigureAwait(false);
            await page.Keyboard.PressAsync("ControlOrMeta+a").ConfigureAwait(false);

            bool ctrl = await page.EvaluateAsync<bool>("window.last && window.last.ctrl").ConfigureAwait(false);
            bool meta = await page.EvaluateAsync<bool>("window.last && window.last.meta").ConfigureAwait(false);
            string key = await page.EvaluateAsync<string>("window.last && window.last.key").ConfigureAwait(false);
            Assert.That(key, Is.EqualTo("a"));
            if (OperatingSystem.IsMacOS())
            {
                Assert.That(meta, Is.True);
                Assert.That(ctrl, Is.False);
            }
            else
            {
                Assert.That(ctrl, Is.True);
                Assert.That(meta, Is.False);
            }
        }
    }
}
