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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
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
