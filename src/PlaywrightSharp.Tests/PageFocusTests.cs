/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>page-focus.spec.ts</c>.
    /// </summary>
    [TestFixture]
    public class PageFocusTests : PageTestEx
    {
        [PlaywrightTest("page-focus.spec.ts", "should work")]
        [PlaywrightTest("page-focus.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldWork()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=d1 tabIndex=0></div>").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.nodeName").ConfigureAwait(false), Is.EqualTo("BODY"));
            await page.FocusAsync("#d1").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("d1"));
        }

        [PlaywrightTest("page-focus.spec.ts", "should emit focus event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitFocusEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=d1 tabIndex=0></div>").ConfigureAwait(false);
            bool focused = false;
            await page.ExposeFunctionAsync("focusEvent", () => focused = true).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#d1", "d1 => d1.addEventListener('focus', window['focusEvent'])").ConfigureAwait(false);
            await page.FocusAsync("#d1").ConfigureAwait(false);
            Assert.That(focused, Is.True);
        }

        [PlaywrightTest("page-focus.spec.ts", "should emit blur event")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldEmitBlurEvent()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div id=d1 tabIndex=0>DIV1</div><div id=d2 tabIndex=0>DIV2</div>").ConfigureAwait(false);
            await page.FocusAsync("#d1").ConfigureAwait(false);
            bool focused = false;
            bool blurred = false;
            await page.ExposeFunctionAsync("focusEvent", () => focused = true).ConfigureAwait(false);
            await page.ExposeFunctionAsync("blurEvent", () => blurred = true).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#d1", "d1 => d1.addEventListener('blur', window['blurEvent'])").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#d2", "d2 => d2.addEventListener('focus', window['focusEvent'])").ConfigureAwait(false);
            await page.FocusAsync("#d2").ConfigureAwait(false);
            Assert.That(focused, Is.True);
            Assert.That(blurred, Is.True);
        }

        [PlaywrightTest("page-focus.spec.ts", "should traverse focus")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTraverseFocus()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input id=\"i1\"><input id=\"i2\">").ConfigureAwait(false);
            bool focused = false;
            await page.ExposeFunctionAsync("focusEvent", () => focused = true).ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("#i2", "i2 => i2.addEventListener('focus', window['focusEvent'])").ConfigureAwait(false);

            await page.FocusAsync("#i1").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("First").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            await page.Keyboard.TypeAsync("Last").ConfigureAwait(false);

            Assert.That(focused, Is.True);
            Assert.That(await page.EvalOnSelectorAsync<string>("#i1", "e => e.value").ConfigureAwait(false), Is.EqualTo("First"));
            Assert.That(await page.EvalOnSelectorAsync<string>("#i2", "e => e.value").ConfigureAwait(false), Is.EqualTo("Last"));
        }

        [PlaywrightTest("page-focus.spec.ts", "should traverse focus in all directions")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTraverseFocusInAllDirections()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input value=\"1\"><input value=\"2\"><input value=\"3\">").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.value").ConfigureAwait(false), Is.EqualTo("1"));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.value").ConfigureAwait(false), Is.EqualTo("2"));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.value").ConfigureAwait(false), Is.EqualTo("3"));
            await page.Keyboard.PressAsync("Shift+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.value").ConfigureAwait(false), Is.EqualTo("2"));
            await page.Keyboard.PressAsync("Shift+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.value").ConfigureAwait(false), Is.EqualTo("1"));
        }

        [PlaywrightTest("page-focus.spec.ts", "should traverse only form elements")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldTraverseOnlyFormElements()
        {
            if (!(TestConstants.IsMacOSX && TestConstants.IsWebKit))
            {
                Assert.Ignore("Chromium and WebKit both have settings for tab traversing all links, but it is only on by default in WebKit.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <input id=""input-1"">
    <button id=""button"">button</button>
    <a href id=""link"">link</a>
    <input id=""input-2"">
  ").ConfigureAwait(false);
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input-1"));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input-2"));
            await page.Keyboard.PressAsync("Shift+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input-1"));
            await page.Keyboard.PressAsync("Alt+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("button"));
            await page.Keyboard.PressAsync("Alt+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("link"));
            await page.Keyboard.PressAsync("Alt+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input-2"));
            await page.Keyboard.PressAsync("Alt+Shift+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("link"));
            await page.Keyboard.PressAsync("Alt+Shift+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("button"));
            await page.Keyboard.PressAsync("Alt+Shift+Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input-1"));
        }

        [PlaywrightTest("page-focus.spec.ts", "clicking checkbox should activate it")]
        [Test]
        [Timeout(30_000)]
        public async Task ClickingCheckboxShouldActivateIt()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("Safari does not focus on click");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            await page.ClickAsync("input").ConfigureAwait(false);
            string nodeName = await page.EvaluateAsync<string>("document.activeElement.nodeName").ConfigureAwait(false);
            Assert.That(nodeName, Is.EqualTo("INPUT"));
        }

        [PlaywrightTest("page-focus.spec.ts", "tab should cycle between single input and browser")]
        [Test]
        [Timeout(30_000)]
        public async Task TabShouldCycleBetweenSingleInputAndBrowser()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium keeps input focused.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"<label for=""input1"">input1</label>
    <input id=""input1"">
    <script>
    {
      window.focusEvents = [];
      const input = document.getElementById('input1');
      input.addEventListener('blur', () => focusEvents.push('blur'));
      input.addEventListener('focus', () => focusEvents.push('focus'));
    }
    </script>").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.tagName").ConfigureAwait(false), Is.EqualTo("BODY"));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input1"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus" }));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.tagName").ConfigureAwait(false), Is.EqualTo("BODY"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus", "blur" }));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input1"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus", "blur", "focus" }));
        }

        [PlaywrightTest("page-focus.spec.ts", "tab should cycle between document elements and browser")]
        [Test]
        [Timeout(30_000)]
        public async Task TabShouldCycleBetweenDocumentElementsAndBrowser()
        {
            if (TestConstants.IsChromium)
            {
                Assert.Ignore("Chromium keeps last input focused.");
            }

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <input id=""input1"">
    <input id=""input2"">
    <script>
      window.focusEvents = [];
      {
        const input = document.getElementById('input1');
        input.addEventListener('blur', () => focusEvents.push('blur1'));
        input.addEventListener('focus', () => focusEvents.push('focus1'));
      }
      {
        const input = document.getElementById('input2');
        input.addEventListener('blur', () => focusEvents.push('blur2'));
        input.addEventListener('focus', () => focusEvents.push('focus2'));
      }
    </script>").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.tagName").ConfigureAwait(false), Is.EqualTo("BODY"));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input1"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus1" }));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input2"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus1", "blur1", "focus2" }));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.tagName").ConfigureAwait(false), Is.EqualTo("BODY"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus1", "blur1", "focus2", "blur2" }));
            await page.Keyboard.PressAsync("Tab").ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement.id").ConfigureAwait(false), Is.EqualTo("input1"));
            Assert.That(await page.EvaluateAsync<string[]>("window.focusEvents").ConfigureAwait(false), Is.EqualTo(new[] { "focus1", "blur1", "focus2", "blur2", "focus1" }));
        }

        [PlaywrightTest("page-focus.spec.ts", "keeps focus on element when attempting to focus a non-focusable element")]
        [Test]
        [Timeout(30_000)]
        public async Task KeepsFocusOnElementWhenAttemptingToFocusANonFocusableElement()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
      <div id=""focusable"" tabindex=""0"">focusable</div>
      <div id=""non-focusable"">not focusable</div>
      <script>
        window.eventLog = [];

        const focusable = document.getElementById(""focusable"");

        focusable.addEventListener('blur', () => window.eventLog.push('blur focusable'));
        focusable.addEventListener('focus', () => window.eventLog.push('focus focusable'));

        const nonFocusable = document.getElementById(""non-focusable"");
        nonFocusable.addEventListener('blur', () => window.eventLog.push('blur non-focusable'));
        nonFocusable.addEventListener('focus', () => window.eventLog.push('focus non-focusable'));
      </script>
    ").ConfigureAwait(false);
            await page.Locator("#focusable").ClickAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false), Is.EqualTo("focusable"));
            await page.Locator("#non-focusable").FocusAsync().ConfigureAwait(false);
            Assert.That(await page.EvaluateAsync<string>("document.activeElement && document.activeElement.id").ConfigureAwait(false), Is.EqualTo("focusable"));
            Assert.That(await page.EvaluateAsync<string[]>("window['eventLog']").ConfigureAwait(false), Is.EqualTo(new[] { "focus focusable" }));
        }
    }
}
