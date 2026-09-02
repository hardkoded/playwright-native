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
using System.Collections.Generic;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>page-select-option.spec.ts</c> parity for <see cref="IPage.SelectOptionAsync"/>.
    /// <c>should throw if passed wrong types</c> is skipped: C# SelectOptionAsync is typed.
    /// </summary>
    [TestFixture]
    public class PageSelectOptionParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null && await FixtureReachableAsync(TestConstants.ServerUrl).ConfigureAwait(false))
            {
                Prefix = TestConstants.ServerUrl;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19227;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    Prefix = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }

            Assert.Ignore("Test server is unavailable.");
        }

        private static async Task<bool> FixtureReachableAsync(string prefix)
        {
            try
            {
                using System.Net.Http.HttpClient client = new System.Net.Http.HttpClient
                {
                    Timeout = TimeSpan.FromSeconds(2),
                };
                System.Net.Http.HttpResponseMessage response = await client.GetAsync(prefix + "/input/select.html").ConfigureAwait(false);
                return response.IsSuccessStatusCode;
            }
            catch (Exception)
            {
                return false;
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }
        }

        private static async Task GiveItAChanceToResolveAsync(IPage page)
        {
            for (int i = 0; i < 5; i++)
            {
                await page.EvaluateAsync<object>("new Promise(r => requestAnimationFrame(() => r(true)))").ConfigureAwait(false);
            }
        }

        private static Task GoToSelectAsync(IPage page)
            => page.GoToAsync(Prefix + "/input/select.html");

        private static Task<string[]> OnInputAsync(IPage page)
            => page.EvaluateAsync<string[]>("(() => window['result'].onInput)()");

        private static Task<string[]> OnChangeAsync(IPage page)
            => page.EvaluateAsync<string[]>("(() => window['result'].onChange)()");

        private static bool AllInExpected(IReadOnlyList<string> result, params string[] expected)
        {
            foreach (string current in result)
            {
                bool included = false;
                foreach (string item in expected)
                {
                    if (item == current)
                    {
                        included = true;
                        break;
                    }
                }

                if (!included)
                {
                    return false;
                }
            }

            return true;
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option")]
        [PlaywrightTest("page-select-option.spec.ts", "should select single option @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", "blue").ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option by value")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOptionByValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new SelectOptionValue { Value = "blue" }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should fall back to selecting by label")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFallBackToSelectingByLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", "Blue").ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option by label")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOptionByLabel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new SelectOptionValue { Label = "Indigo" }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "indigo" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "indigo" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option by label with html whitespace")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOptionByLabelWithHtmlWhitespace()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new SelectOptionValue { Label = "HTML" }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "html" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "html" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option by handle")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOptionByHandle()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            IElementHandle option = await page.QuerySelectorAsync("[id=whiteOption]").ConfigureAwait(false);
            await page.SelectOptionAsync("select", option).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "white" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "white" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option by index")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOptionByIndex()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new SelectOptionValue { Index = 2 }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "brown" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "brown" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select single option by multiple attributes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectSingleOptionByMultipleAttributes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new SelectOptionValue { Value = "green", Label = "Green" }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "green" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "green" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should not select single option when some attributes do not match")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotSelectSingleOptionWhenSomeAttributesDoNotMatch()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("select", "s => s.value = undefined").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.SelectOptionAsync("select", new SelectOptionValue { Value = "green", Label = "Brown" }, timeout: 1000));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Timeout"));
            string value = await page.EvaluateAsync<string>("(() => document.querySelector('select').value)()").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(string.Empty));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select only first option")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectOnlyFirstOption()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new[] { "blue", "green", "red" }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should not throw when select causes navigation")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotThrowWhenSelectCausesNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("select", "select => select.addEventListener('input', () => window.location.href = '/empty.html')").ConfigureAwait(false);
            await Task.WhenAll(
                page.SelectOptionAsync("select", "blue"),
                page.WaitForNavigationAsync()).ConfigureAwait(false);
            Assert.That(page.Url, Does.Contain("empty.html"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select multiple options")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectMultipleOptions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            await page.SelectOptionAsync("select", new[] { "blue", "green", "red" }).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue", "green", "red" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue", "green", "red" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should select multiple options with attributes")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldSelectMultipleOptionsWithAttributes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            SelectOptionValue[] options =
            {
                new SelectOptionValue { Value = "blue" },
                new SelectOptionValue { Label = "Green" },
                new SelectOptionValue { Index = 4 },
            };
            await page.SelectOptionAsync("select", (IEnumerable<SelectOptionValue>)options).ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue", "gray", "green" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue", "gray", "green" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should respect event bubbling")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldRespectEventBubbling()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", "blue").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string[]>("(() => window['result'].onBubblingInput)()").ConfigureAwait(false),
                Is.EqualTo(new[] { "blue" }));
            Assert.That(
                await page.EvaluateAsync<string[]>("(() => window['result'].onBubblingChange)()").ConfigureAwait(false),
                Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should throw when element is not a <select>")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenElementIsNotASelect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.SelectOptionAsync("body", string.Empty));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Element is not a <select> element"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should return [] on no matched values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyArrayOnNoMatchedValues()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            IReadOnlyList<string> result = await page.SelectOptionAsync("select", Array.Empty<string>()).ConfigureAwait(false);
            Assert.That(result, Is.Empty);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should return an array of matched values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnAnArrayOfMatchedValues()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            IReadOnlyList<string> result = await page.SelectOptionAsync("select", new[] { "blue", "black", "magenta" }).ConfigureAwait(false);
            Assert.That(AllInExpected(result, "blue", "black", "magenta"), Is.True);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should return an array of one element when multiple is not set")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnAnArrayOfOneElementWhenMultipleIsNotSet()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            IReadOnlyList<string> result = await page.SelectOptionAsync("select", new[] { "42", "blue", "black", "magenta" }).ConfigureAwait(false);
            Assert.That(result.Count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should return [] on no values")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReturnEmptyArrayOnNoValues()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            IReadOnlyList<string> result = await page.SelectOptionAsync("select", Array.Empty<string>()).ConfigureAwait(false);
            Assert.That(result, Is.Empty);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should not allow null items")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotAllowNullItems()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.SelectOptionAsync("select", new string[] { "blue", null, "black", "magenta" }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("options[1]: expected object, got null"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should unselect with null")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUnselectWithNull()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            IReadOnlyList<string> result = await page.SelectOptionAsync("select", new[] { "blue", "black", "magenta" }).ConfigureAwait(false);
            Assert.That(AllInExpected(result, "blue", "black", "magenta"), Is.True);
            await page.SelectOptionAsync("select", (string)null).ConfigureAwait(false);
            bool allCleared = await page.EvalOnSelectorAsync<bool>("select", "select => Array.from(select.options).every(option => !option.selected)").ConfigureAwait(false);
            Assert.That(allCleared, Is.True);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should deselect all options when passed no values for a multiple select")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDeselectAllOptionsWhenPassedNoValuesForAMultipleSelect()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            await page.SelectOptionAsync("select", new[] { "blue", "black", "magenta" }).ConfigureAwait(false);
            await page.SelectOptionAsync("select", Array.Empty<string>()).ConfigureAwait(false);
            bool allCleared = await page.EvalOnSelectorAsync<bool>("select", "select => Array.from(select.options).every(option => !option.selected)").ConfigureAwait(false);
            Assert.That(allCleared, Is.True);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should deselect all options when passed no values for a select without multiple")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDeselectAllOptionsWhenPassedNoValuesForASelectWithoutMultiple()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.SelectOptionAsync("select", new[] { "blue", "black", "magenta" }).ConfigureAwait(false);
            await page.SelectOptionAsync("select", Array.Empty<string>()).ConfigureAwait(false);
            bool allCleared = await page.EvalOnSelectorAsync<bool>("select", "select => Array.from(select.options).every(option => !option.selected)").ConfigureAwait(false);
            Assert.That(allCleared, Is.True);
        }

        [PlaywrightTest("page-select-option.spec.ts", "should throw if passed wrong types")]
        [Test]
        [Timeout(30_000)]
        public void ShouldThrowIfPassedWrongTypes()
        {
            Assert.Ignore("C# SelectOptionAsync is typed");
        }

        [PlaywrightTest("page-select-option.spec.ts", "should work when re-defining top-level Event class")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWorkWhenReDefiningTopLevelEventClass()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => { window.Event = null; })()").ConfigureAwait(false);
            await page.SelectOptionAsync("select", "blue").ConfigureAwait(false);
            Assert.That(await OnInputAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
            Assert.That(await OnChangeAsync(page).ConfigureAwait(false), Is.EqualTo(new[] { "blue" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for option to be present")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForOptionToBePresent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            Task<IReadOnlyList<string>> selectTask = page.SelectOptionAsync("select", "scarlet");
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(selectTask.IsCompleted, Is.False);
            await page.EvalOnSelectorAsync<object>(
                "select",
                @"select => {
                    const option = document.createElement('option');
                    option.value = 'scarlet';
                    option.textContent = 'Scarlet';
                    select.appendChild(option);
                }").ConfigureAwait(false);
            IReadOnlyList<string> items = await selectTask.ConfigureAwait(false);
            Assert.That(items, Is.EqualTo(new[] { "scarlet" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for option index to be present")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForOptionIndexToBePresent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            int len = await page.EvalOnSelectorAsync<int>("select", "select => select.options.length").ConfigureAwait(false);
            Task<IReadOnlyList<string>> selectTask = page.SelectOptionAsync("select", new SelectOptionValue { Index = len });
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(selectTask.IsCompleted, Is.False);
            await page.EvalOnSelectorAsync<object>(
                "select",
                @"select => {
                    const option = document.createElement('option');
                    option.value = 'scarlet';
                    option.textContent = 'Scarlet';
                    select.appendChild(option);
                }").ConfigureAwait(false);
            IReadOnlyList<string> items = await selectTask.ConfigureAwait(false);
            Assert.That(items, Is.EqualTo(new[] { "scarlet" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for multiple options to be present")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForMultipleOptionsToBePresent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await GoToSelectAsync(page).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['makeMultiple']())()").ConfigureAwait(false);
            Task<IReadOnlyList<string>> selectTask = page.SelectOptionAsync("select", new[] { "green", "scarlet" });
            await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
            Assert.That(selectTask.IsCompleted, Is.False);
            await page.EvalOnSelectorAsync<object>(
                "select",
                @"select => {
                    const option = document.createElement('option');
                    option.value = 'scarlet';
                    option.textContent = 'Scarlet';
                    select.appendChild(option);
                }").ConfigureAwait(false);
            IReadOnlyList<string> items = await selectTask.ConfigureAwait(false);
            Assert.That(items, Is.EqualTo(new[] { "green", "scarlet" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "input event.composed should be true and cross shadow dom boundary")]
        [Test]
        [Timeout(30_000)]
        public async Task InputEventComposedShouldBeTrueAndCrossShadowDomBoundary()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(Prefix + "/empty.html").ConfigureAwait(false);
            await page.SetContentAsync(@"<body><script>
  const div = document.createElement('div');
  const shadowRoot = div.attachShadow({mode: 'open'});
  shadowRoot.innerHTML = `<select>
    <option value=""black"">Black</option>
    <option value=""blue"">Blue</option>
  </select>`;
  document.body.appendChild(div);
</script></body>").ConfigureAwait(false);
            await page.Locator("body").EvaluateAsync<object>(@"select => {
    window['firedBodyEvents'] = [];
    for (const event of ['input', 'change']) {
      select.addEventListener(event, e => {
        window['firedBodyEvents'].push(e.type + ':' + e.composed);
      }, false);
    }
  }").ConfigureAwait(false);
            await page.Locator("select").EvaluateAsync<object>(@"select => {
    window['firedEvents'] = [];
    for (const event of ['input', 'change']) {
      select.addEventListener(event, e => {
        window['firedEvents'].push(e.type + ':' + e.composed);
      }, false);
    }
  }").ConfigureAwait(false);
            await page.SelectOptionAsync("select", "blue").ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string[]>("(() => window['firedEvents'])()").ConfigureAwait(false),
                Is.EqualTo(new[] { "input:true", "change:false" }));
            Assert.That(
                await page.EvaluateAsync<string[]>("(() => window['firedBodyEvents'])()").ConfigureAwait(false),
                Is.EqualTo(new[] { "input:true" }));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for select to be enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForSelectToBeEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <select disabled>
      <option>one</option>
      <option>two</option>
    </select>

    <script>
    function hydrate() {
      const select = document.querySelector('select');
      select.removeAttribute('disabled');
      select.addEventListener('change', () => {
        window['result'] = select.value;
      });
    }
    </script>
  ").ConfigureAwait(false);
            Task<IReadOnlyList<string>> selectTask = page.Locator("select").SelectOptionAsync("two");
            await Task.Delay(1000).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['hydrate']())()").ConfigureAwait(false);
            await selectTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("two"));
            Assert.That(await page.Locator("select").InputValueAsync().ConfigureAwait(false), Is.EqualTo("two"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for option to be enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForOptionToBeEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <select>
      <option>one</option>
      <option disabled id=myoption>two</option>
    </select>

    <script>
    function hydrate() {
      const option = document.querySelector('#myoption');
      option.removeAttribute('disabled');
      const select = document.querySelector('select');
      select.addEventListener('change', () => {
        window['result'] = select.value;
      });
    }
    </script>
  ").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("select").SelectOptionAsync("two", options: new() { Timeout = 1000 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("option being selected is not enabled"));

            Task<IReadOnlyList<string>> selectTask = page.Locator("select").SelectOptionAsync("two");
            await Task.Delay(1000).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['hydrate']())()").ConfigureAwait(false);
            await selectTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("two"));
            Assert.That(await page.Locator("select").InputValueAsync().ConfigureAwait(false), Is.EqualTo("two"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for optgroup to be enabled")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForOptgroupToBeEnabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <select>
      <option>one</option>
      <optgroup label=""Group"" disabled id=mygroup>
        <option>two</option>
      </optgroup>
    </select>

    <script>
    function hydrate() {
      const group = document.querySelector('#mygroup');
      group.removeAttribute('disabled');
      const select = document.querySelector('select');
      select.addEventListener('change', () => {
        window['result'] = select.value;
      });
    }
    </script>
  ").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => page.Locator("select").SelectOptionAsync("two", options: new() { Timeout = 1000 }));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("option being selected is not enabled"));

            Task<IReadOnlyList<string>> selectTask = page.Locator("select").SelectOptionAsync("two");
            await Task.Delay(1000).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['hydrate']())()").ConfigureAwait(false);
            await selectTask.ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("two"));
            Assert.That(await page.Locator("select").InputValueAsync().ConfigureAwait(false), Is.EqualTo("two"));
        }

        [PlaywrightTest("page-select-option.spec.ts", "should wait for select to be swapped")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWaitForSelectToBeSwapped()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <select disabled>
      <option>one</option>
      <option>two</option>
    </select>

    <script>
    function hydrate() {
      const select = document.querySelector('select');
      select.remove();

      const newSelect = document.createElement('select');
      const option1 = document.createElement('option');
      option1.textContent = 'one';
      newSelect.appendChild(option1);
      const option2 = document.createElement('option');
      option2.textContent = 'two';
      newSelect.appendChild(option2);

      document.body.appendChild(newSelect);

      newSelect.addEventListener('change', () => {
        window['result'] = newSelect.value;
      });
    }
    </script>
  ").ConfigureAwait(false);
            Task<IReadOnlyList<string>> selectTask = page.Locator("select").SelectOptionAsync("two");
            await Task.Delay(1000).ConfigureAwait(false);
            await page.EvaluateAsync<object>("(() => window['hydrate']())()").ConfigureAwait(false);
            await selectTask.ConfigureAwait(false);
            Assert.That(await page.Locator("select").InputValueAsync().ConfigureAwait(false), Is.EqualTo("two"));
            Assert.That(await page.EvaluateAsync<string>("(() => window['result'])()").ConfigureAwait(false), Is.EqualTo("two"));
        }
    }
}
