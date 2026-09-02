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
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Integration tests for DirectElementHandle read/state surface: QuerySelector,
    /// TextContent, GetAttribute, IsVisible, BoundingBox.
    /// </summary>
    [TestFixture]
    public class ElementHandleReadTests : PageTestEx
    {
        [PlaywrightTest("elementhandle-convenience.spec.ts", "Query selector should return handle for match")]
        [Test]
        [Timeout(30_000)]
        public async Task QuerySelectorShouldReturnHandleForMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='target'>hello</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#target").ConfigureAwait(false);

            Assert.That(handle, Is.Not.Null);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "Query selector should return null for no match")]
        [Test]
        [Timeout(30_000)]
        public async Task QuerySelectorShouldReturnNullForNoMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div>body</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#not-there").ConfigureAwait(false);

            Assert.That(handle, Is.Null);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "Text content should return inner text")]
        [Test]
        [Timeout(30_000)]
        public async Task TextContentShouldReturnInnerText()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='t'>hello-world</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            string text = await handle.TextContentAsync().ConfigureAwait(false);

            Assert.That(text, Is.EqualTo("hello-world"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "Get attribute should return attribute value")]
        [Test]
        [Timeout(30_000)]
        public async Task GetAttributeShouldReturnAttributeValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='t' data-kind='primary'>x</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            string kind = await handle.GetAttributeAsync("data-kind").ConfigureAwait(false);

            Assert.That(kind, Is.EqualTo("primary"));
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "Is visible should return true for visible element")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleShouldReturnTrueForVisibleElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='t' style='width:50px;height:50px;background:red'>v</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            bool visible = await handle.IsVisibleAsync().ConfigureAwait(false);

            Assert.That(visible, Is.True);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "Is visible should return false for hidden element")]
        [Test]
        [Timeout(30_000)]
        public async Task IsVisibleShouldReturnFalseForHiddenElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='t' style='display:none'>h</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            bool visible = await handle.IsVisibleAsync().ConfigureAwait(false);

            Assert.That(visible, Is.False);
        }

        [PlaywrightTest("elementhandle-convenience.spec.ts", "Bounding box should return geometry for positioned element")]
        [Test]
        [Timeout(30_000)]
        public async Task BoundingBoxShouldReturnGeometryForPositionedElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.GoToAsync("data:text/html,<div id='t' style='position:absolute;left:10px;top:20px;width:30px;height:40px;background:blue'>b</div>").ConfigureAwait(false);

            IElementHandle handle = await page.QuerySelectorAsync("#t").ConfigureAwait(false);
            var box = await handle.BoundingBoxAsync().ConfigureAwait(false);

            Assert.That(box, Is.Not.Null);
            Assert.That(box.Width, Is.EqualTo(30f).Within(1f));
            Assert.That(box.Height, Is.EqualTo(40f).Within(1f));
            Assert.That(box.X, Is.EqualTo(10f).Within(1f));
            Assert.That(box.Y, Is.EqualTo(20f).Within(1f));
        }

    }
}
