/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Upstream <c>page-check.spec.ts</c> parity for Check, Uncheck, SetChecked, and IsChecked.
    /// </summary>
    [TestFixture]
    public class PageCheckParityTests : PageTestEx
    {
        [PlaywrightTest("page-check.spec.ts", "should check the box @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            await page.CheckAsync("input").ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "should not check the checked box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotCheckTheCheckedBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' checked></input>").ConfigureAwait(false);
            await page.CheckAsync("input").ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "should uncheck the box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUncheckTheBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' checked></input>").ConfigureAwait(false);
            await page.UncheckAsync("input").ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "should not uncheck the unchecked box")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldNotUncheckTheUncheckedBox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            await page.UncheckAsync("input").ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "should check radio")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckRadio()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <input type='radio'>one</input>
    <input id='two' type='radio'>two</input>
    <input type='radio'>three</input>").ConfigureAwait(false);
            await page.CheckAsync("#two").ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['two'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "should check radio by aria role")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckRadioByAriaRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<div role='radio' id='checkbox'>CHECKBOX</div>
    <script>
      checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'true'));
    </script>").ConfigureAwait(false);
            await page.CheckAsync("div").ConfigureAwait(false);
            string aria = await page.EvaluateAsync<string>("(() => window['checkbox'].getAttribute('aria-checked'))()").ConfigureAwait(false);
            Assert.That(aria, Is.EqualTo("true"));
        }

        [PlaywrightTest("page-check.spec.ts", "should uncheck radio by aria role")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUncheckRadioByAriaRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<div role='radio' id='checkbox' aria-checked=""true"">CHECKBOX</div>
    <script>
      checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'false'));
    </script>").ConfigureAwait(false);
            await page.UncheckAsync("div").ConfigureAwait(false);
            string aria = await page.EvaluateAsync<string>("(() => window['checkbox'].getAttribute('aria-checked'))()").ConfigureAwait(false);
            Assert.That(aria, Is.EqualTo("false"));
        }

        [PlaywrightTest("page-check.spec.ts", "should check the box by aria role")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBoxByAriaRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string[] roles = { "checkbox", "menuitemcheckbox", "option", "radio", "switch", "menuitemradio", "treeitem" };
            foreach (string role in roles)
            {
                await page.SetContentAsync($@"<div role='{role}' id='checkbox'>CHECKBOX</div>
        <script>
          checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'true'));
        </script>").ConfigureAwait(false);
                await page.CheckAsync("div").ConfigureAwait(false);
                string aria = await page.EvaluateAsync<string>("(() => window['checkbox'].getAttribute('aria-checked'))()").ConfigureAwait(false);
                Assert.That(aria, Is.EqualTo("true"));
            }
        }

        [PlaywrightTest("page-check.spec.ts", "should uncheck the box by aria role")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldUncheckTheBoxByAriaRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string[] roles = { "checkbox", "menuitemcheckbox", "option", "radio", "switch", "menuitemradio", "treeitem" };
            foreach (string role in roles)
            {
                await page.SetContentAsync($@"<div role='{role}' id='checkbox' aria-checked=""true"">CHECKBOX</div>
        <script>
          checkbox.addEventListener('click', () => checkbox.setAttribute('aria-checked', 'false'));
        </script>").ConfigureAwait(false);
                await page.UncheckAsync("div").ConfigureAwait(false);
                string aria = await page.EvaluateAsync<string>("(() => window['checkbox'].getAttribute('aria-checked'))()").ConfigureAwait(false);
                Assert.That(aria, Is.EqualTo("false"));
            }
        }

        [PlaywrightTest("page-check.spec.ts", "should throw when not a checkbox")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenNotACheckbox()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div>Check me</div>").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.CheckAsync("div"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Not a checkbox or radio button"));
        }

        [PlaywrightTest("page-check.spec.ts", "should throw when not a checkbox 2")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenNotACheckbox2()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div role=button>Check me</div>").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.CheckAsync("div"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Not a checkbox or radio button"));
        }

        [PlaywrightTest("page-check.spec.ts", "should check the box inside a button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBoxInsideAButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div role='button'><input type='checkbox'></div>").ConfigureAwait(false);
            await page.CheckAsync("input").ConfigureAwait(false);
            bool viaEval = await page.EvalOnSelectorAsync<bool>("input", "input => input.checked").ConfigureAwait(false);
            bool viaPage = await page.IsCheckedAsync("input").ConfigureAwait(false);
            IElementHandle handle = await page.QuerySelectorAsync("input").ConfigureAwait(false);
            bool viaHandle = await handle.IsCheckedAsync().ConfigureAwait(false);
            Assert.That(viaEval, Is.True);
            Assert.That(viaPage, Is.True);
            Assert.That(viaHandle, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "should check the label with position")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheLabelWithPosition()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string emptyPageJson = JsonSerializer.Serialize(TestConstants.EmptyPage);
            await page.SetContentAsync($@"
    <input id='checkbox' type='checkbox' style='width: 5px; height: 5px;'>
    <label for='checkbox'>
      <a href={emptyPageJson}>I am a long link that goes away so that nothing good will happen if you click on me</a>
      Click me
    </label>").ConfigureAwait(false);
            IElementHandle clickMe = await page.QuerySelectorAsync("text=Click me").ConfigureAwait(false);
            var box = await clickMe.BoundingBoxAsync().ConfigureAwait(false);
            await page.CheckAsync("text=Click me", new() { Position = new Position { X = box.Width - 10, Y = 2 } }).ConfigureAwait(false);
            bool isChecked = await page.EvalOnSelectorAsync<bool>("input", "input => input.checked").ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "trial run should not check")]
        [Test]
        [Timeout(30_000)]
        public async Task TrialRunShouldNotCheck()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            await page.CheckAsync("input", new() { Trial = true }).ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "trial run should not uncheck")]
        [Test]
        [Timeout(30_000)]
        public async Task TrialRunShouldNotUncheck()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox' checked></input>").ConfigureAwait(false);
            await page.UncheckAsync("input", new() { Trial = true }).ConfigureAwait(false);
            bool isChecked = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "should check the box using setChecked")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldCheckTheBoxUsingSetChecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id='checkbox' type='checkbox'></input>").ConfigureAwait(false);
            await page.SetCheckedAsync("input", true).ConfigureAwait(false);
            bool checkedAfterSet = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(checkedAfterSet, Is.True);
            await page.SetCheckedAsync("input", false).ConfigureAwait(false);
            bool checkedAfterUnset = await page.EvaluateAsync<bool>("(() => window['checkbox'].checked)()").ConfigureAwait(false);
            Assert.That(checkedAfterUnset, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "should throw when trying to uncheck radio button")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldThrowWhenTryingToUncheckRadioButton()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input type='radio' name='test' checked id='radio'>").ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.UncheckAsync("#radio"));
            Assert.That(error, Is.Not.Null);
            Assert.That(error.Message, Does.Contain("Cannot uncheck radio button"));
        }
    }
}
