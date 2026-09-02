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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect-to-have-accessible.spec.ts</c> parity for
    /// accessible name, description, error message, and role. Skipped
    /// (JS <c>@ts-expect-error</c> regex role): toHaveRole throw when
    /// given a regular expression.
    /// </summary>
    [TestFixture]
    public class ExpectToHaveAccessibleParityTests : PageTestEx
    {
        private const string ErrorMessageText = "Error message";

        private static async Task<ILocator> SetupAriaInvalidPageAsync(IPage page, string ariaInvalidValue)
        {
            string ariaInvalidAttr = ariaInvalidValue == null
                ? string.Empty
                : "aria-invalid=\"" + ariaInvalidValue + "\"";
            await page.SetContentAsync(@"
        <form>
          <input id=""node"" role=""textbox"" " + ariaInvalidAttr + @" aria-errormessage=""error-msg"" />
          <div id=""error-msg"">Error message</div>
        </form>
      ").ConfigureAwait(false);
            return page.Locator("#node");
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "toHaveAccessibleName")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAccessibleName()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div role=""button"" aria-label=""Hello""></div>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleNameAsync("Hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).Not.ToHaveAccessibleNameAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleNameAsync("hello", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleNameAsync(new Regex(@"ell\w")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).Not.ToHaveAccessibleNameAsync(new Regex("hello")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleNameAsync(new Regex("hello"), new() { IgnoreCase = true }).ConfigureAwait(false);

            await page.SetContentAsync(@"<button>foo&nbsp;bar
baz</button>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button")).ToHaveAccessibleNameAsync("foo bar baz").ConfigureAwait(false);

            await page.SetContentAsync(@"
    <select>
      <option>&nbsp;HTML</option>
    </select>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("option")).ToHaveAccessibleNameAsync("HTML").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "toHaveAccessibleDescription")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAccessibleDescription()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div role=""button"" aria-description=""Hello""></div>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleDescriptionAsync("Hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).Not.ToHaveAccessibleDescriptionAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleDescriptionAsync("hello", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleDescriptionAsync(new Regex(@"ell\w")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).Not.ToHaveAccessibleDescriptionAsync(new Regex("hello")).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleDescriptionAsync(new Regex("hello"), new() { IgnoreCase = true }).ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div role=""button"" aria-describedby=""desc""></div>
    <span id=""desc"">foo&nbsp;bar
baz</span>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveAccessibleDescriptionAsync("foo bar baz").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "toHaveAccessibleErrorMessage")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAccessibleErrorMessage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <form>
      <input role=""textbox"" aria-invalid=""true"" aria-errormessage=""error-message"" />
      <div id=""error-message"">Hello</div>
      <div id=""irrelevant-error"">This should not be considered.</div>
    </form>
  ").ConfigureAwait(false);

            ILocator locator = page.Locator("input[role=\"textbox\"]");
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync("Hello").ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync("hello").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync("hello", new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(new Regex(@"ell\w")).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync(new Regex("hello")).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(new Regex("hello"), new() { IgnoreCase = true }).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync("This should not be considered.").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "toHaveAccessibleErrorMessage should handle multiple aria-errormessage references")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAccessibleErrorMessageShouldHandleMultipleAriaErrormessageReferences()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <form>
      <input role=""textbox"" aria-invalid=""true"" aria-errormessage=""error1 error2"" />
      <div id=""error1"">First error message.</div>
      <div id=""error2"">Second error message.</div>
      <div id=""irrelevant-error"">This should not be considered.</div>
    </form>
  ").ConfigureAwait(false);

            ILocator locator = page.Locator("input[role=\"textbox\"]");

            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync("First error message. Second error message.").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(new Regex("first error message.", RegexOptions.IgnoreCase)).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(new Regex("second error message.", RegexOptions.IgnoreCase)).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync(new Regex("This should not be considered.", RegexOptions.IgnoreCase)).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "no aria-invalid attribute")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NoAriaInvalidAttribute()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ILocator locator = await SetupAriaInvalidPageAsync(page, ariaInvalidValue: null).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "aria-invalid=\"false\"")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AriaInvalidFalse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ILocator locator = await SetupAriaInvalidPageAsync(page, "false").ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "aria-invalid=\"\" (empty string)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AriaInvalidEmptyString()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ILocator locator = await SetupAriaInvalidPageAsync(page, string.Empty).ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "aria-invalid=\"true\"")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AriaInvalidTrue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ILocator locator = await SetupAriaInvalidPageAsync(page, "true").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "aria-invalid=\"foo\" (unrecognized value)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AriaInvalidFooUnrecognizedValue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            ILocator locator = await SetupAriaInvalidPageAsync(page, "foo").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "should show error message when validity is false and aria-invalid is true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowErrorMessageWhenValidityIsFalseAndAriaInvalidIsTrue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <form>
      <input id=""node"" role=""textbox"" type=""number"" min=""1"" max=""100"" aria-invalid=""true"" aria-errormessage=""error-msg"" />
      <div id=""error-msg"">Error message</div>
    </form>
  ").ConfigureAwait(false);
            ILocator locator = page.Locator("#node");
            await locator.FillAsync("101").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "should show error message when validity is true and aria-invalid is true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowErrorMessageWhenValidityIsTrueAndAriaInvalidIsTrue()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <form>
      <input id=""node"" role=""textbox"" type=""number"" min=""1"" max=""100"" aria-invalid=""true"" aria-errormessage=""error-msg"" />
      <div id=""error-msg"">Error message</div>
    </form>
  ").ConfigureAwait(false);
            ILocator locator = page.Locator("#node");
            await locator.FillAsync("99").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "should show error message when validity is false and aria-invalid is false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowErrorMessageWhenValidityIsFalseAndAriaInvalidIsFalse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <form>
      <input id=""node"" role=""textbox"" type=""number"" min=""1"" max=""100"" aria-invalid=""false"" aria-errormessage=""error-msg"" />
      <div id=""error-msg"">Error message</div>
    </form>
  ").ConfigureAwait(false);
            ILocator locator = page.Locator("#node");
            await locator.FillAsync("101").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "should not show error message when validity is true and aria-invalid is false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotShowErrorMessageWhenValidityIsTrueAndAriaInvalidIsFalse()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <form>
      <input id=""node"" role=""textbox"" type=""number"" min=""1"" max=""100"" aria-invalid=""false"" aria-errormessage=""error-msg"" />
      <div id=""error-msg"">Error message</div>
    </form>
  ").ConfigureAwait(false);
            ILocator locator = page.Locator("#node");
            await locator.FillAsync("99").ConfigureAwait(false);
            await Assertions.Expect(locator).Not.ToHaveAccessibleErrorMessageAsync(ErrorMessageText).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "should show error message for all roles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowErrorMessageForAllRoles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <label for=""file"">File input</label>
    <input id=""file"" aria-invalid=""true"" aria-errormessage=""file-error-id"" type=""file"" />
    <p id=""file-error-id"" role=""alert"">File is incorrect</p>
  ").ConfigureAwait(false);
            ILocator locator = page.GetByLabel("File input");
            await Assertions.Expect(locator).ToHaveAccessibleErrorMessageAsync("File is incorrect").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-accessible.spec.ts", "toHaveRole")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveRole()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"<div role=""button"">Button!</div>").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).ToHaveRoleAsync(AriaRole.Button).ConfigureAwait(false);
            await Assertions.Expect(page.Locator("div")).Not.ToHaveRoleAsync(AriaRole.Checkbox).ConfigureAwait(false);
        }
    }
}
