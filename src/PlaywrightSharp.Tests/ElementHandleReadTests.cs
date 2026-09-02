/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
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
