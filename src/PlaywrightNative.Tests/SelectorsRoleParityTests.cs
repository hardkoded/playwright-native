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
    /// Official <c>selectors-role.spec.ts</c> parity for the <c>role=</c>
    /// engine, ARIA states, hidden filtering, and getByRole. Do not edit
    /// leftover <c>GetByTests.cs</c>.
    /// </summary>
    [TestFixture]
    public class SelectorsRoleParityTests : PageTestEx
    {
        [PlaywrightTest("selectors-role.spec.ts", "should detect roles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldDetectRoles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <button>Hello</button>
    <select multiple="""" size=""2""></select>
    <select></select>
    <h3>Heading</h3>
    <details><summary>Hello</summary></details>
    <div role=""dialog"">I am a dialog</div>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=button").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Hello</button>" }));
            Assert.That(await page.Locator("role=listbox").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<select multiple=\"\" size=\"2\"></select>" }));
            Assert.That(await page.Locator("role=combobox").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<select></select>" }));
            Assert.That(await page.Locator("role=heading").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<h3>Heading</h3>" }));
            Assert.That(await page.Locator("role=group").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<details><summary>Hello</summary></details>" }));
            Assert.That(await page.Locator("role=dialog").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"dialog\">I am a dialog</div>" }));
            Assert.That(await page.Locator("role=menuitem").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
            Assert.That(await page.GetByRole("menuitem").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.Empty);
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support selected")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSelected()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <select>
      <option>Hi</option>
      <option selected>Hello</option>
    </select>
    <div>
      <div role=""option"" aria-selected=""true"">Hi</div>
      <div role=""option"" aria-selected=""false"">Hello</div>
    </div>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=option[selected]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option selected=\"\">Hello</option>", "<div role=\"option\" aria-selected=\"true\">Hi</div>" }));
            Assert.That(await page.Locator("role=option[selected=true]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option selected=\"\">Hello</option>", "<div role=\"option\" aria-selected=\"true\">Hi</div>" }));
            Assert.That(await page.GetByRole("option", selected: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option selected=\"\">Hello</option>", "<div role=\"option\" aria-selected=\"true\">Hi</div>" }));
            Assert.That(await page.Locator("role=option[selected=false]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option>Hi</option>", "<div role=\"option\" aria-selected=\"false\">Hello</div>" }));
            Assert.That(await page.GetByRole("option", selected: false).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option>Hi</option>", "<div role=\"option\" aria-selected=\"false\">Hello</div>" }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support checked")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportChecked()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <input type=checkbox>
    <input type=checkbox checked>
    <input type=checkbox indeterminate>
    <div role=checkbox aria-checked=""true"">Hi</div>
    <div role=checkbox aria-checked=""false"">Hello</div>
    <div role=checkbox>Unknown</div>
  ").ConfigureAwait(false);
            await page.EvalOnSelectorAsync<object>("[indeterminate]", "input => input.indeterminate = true").ConfigureAwait(false);

            Assert.That(await page.Locator("role=checkbox[checked]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input type=\"checkbox\" checked=\"\">", "<div role=\"checkbox\" aria-checked=\"true\">Hi</div>" }));
            Assert.That(await page.Locator("role=checkbox[checked=true]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input type=\"checkbox\" checked=\"\">", "<div role=\"checkbox\" aria-checked=\"true\">Hi</div>" }));
            Assert.That(await page.GetByRole("checkbox", checkedState: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input type=\"checkbox\" checked=\"\">", "<div role=\"checkbox\" aria-checked=\"true\">Hi</div>" }));
            Assert.That(await page.Locator("role=checkbox[checked=false]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input type=\"checkbox\">", "<div role=\"checkbox\" aria-checked=\"false\">Hello</div>", "<div role=\"checkbox\">Unknown</div>" }));
            Assert.That(await page.GetByRole("checkbox", checkedState: false).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input type=\"checkbox\">", "<div role=\"checkbox\" aria-checked=\"false\">Hello</div>", "<div role=\"checkbox\">Unknown</div>" }));
            Assert.That(await page.Locator("role=checkbox[checked=\"mixed\"]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input type=\"checkbox\" indeterminate=\"\">" }));
            Assert.That(await page.Locator("role=checkbox").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<input type=\"checkbox\">",
                "<input type=\"checkbox\" checked=\"\">",
                "<input type=\"checkbox\" indeterminate=\"\">",
                "<div role=\"checkbox\" aria-checked=\"true\">Hi</div>",
                "<div role=\"checkbox\" aria-checked=\"false\">Hello</div>",
                "<div role=\"checkbox\">Unknown</div>",
            }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support pressed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportPressed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <button>Hi</button>
    <button aria-pressed=""true"">Hello</button>
    <button aria-pressed=""false"">Bye</button>
    <button aria-pressed=""mixed"">Mixed</button>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=button[pressed]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button aria-pressed=\"true\">Hello</button>" }));
            Assert.That(await page.Locator("role=button[pressed=true]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button aria-pressed=\"true\">Hello</button>" }));
            Assert.That(await page.GetByRole("button", pressed: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button aria-pressed=\"true\">Hello</button>" }));
            Assert.That(await page.Locator("role=button[pressed=false]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Hi</button>", "<button aria-pressed=\"false\">Bye</button>" }));
            Assert.That(await page.GetByRole("button", pressed: false).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Hi</button>", "<button aria-pressed=\"false\">Bye</button>" }));
            Assert.That(await page.Locator("role=button[pressed=\"mixed\"]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button aria-pressed=\"mixed\">Mixed</button>" }));
            Assert.That(await page.Locator("role=button").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<button>Hi</button>",
                "<button aria-pressed=\"true\">Hello</button>",
                "<button aria-pressed=\"false\">Bye</button>",
                "<button aria-pressed=\"mixed\">Mixed</button>",
            }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support expanded")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportExpanded()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div role=""treeitem"">Hi</div>
    <div role=""treeitem"" aria-expanded=""true"">Hello</div>
    <div role=""treeitem"" aria-expanded=""false"">Bye</div>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=treeitem").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<div role=\"treeitem\">Hi</div>",
                "<div role=\"treeitem\" aria-expanded=\"true\">Hello</div>",
                "<div role=\"treeitem\" aria-expanded=\"false\">Bye</div>",
            }));
            Assert.That(await page.GetByRole("treeitem").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<div role=\"treeitem\">Hi</div>",
                "<div role=\"treeitem\" aria-expanded=\"true\">Hello</div>",
                "<div role=\"treeitem\" aria-expanded=\"false\">Bye</div>",
            }));
            Assert.That(await page.Locator("role=treeitem[expanded]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"treeitem\" aria-expanded=\"true\">Hello</div>" }));
            Assert.That(await page.Locator("role=treeitem[expanded=true]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"treeitem\" aria-expanded=\"true\">Hello</div>" }));
            Assert.That(await page.GetByRole("treeitem", expanded: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"treeitem\" aria-expanded=\"true\">Hello</div>" }));
            Assert.That(await page.Locator("role=treeitem[expanded=false]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"treeitem\" aria-expanded=\"false\">Bye</div>" }));
            Assert.That(await page.GetByRole("treeitem", expanded: false).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"treeitem\" aria-expanded=\"false\">Bye</div>" }));
            Assert.That(await page.Locator("[role=treeitem]:not([aria-expanded])").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"treeitem\">Hi</div>" }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support disabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportDisabled()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <button>Hi</button>
    <button disabled>Bye</button>
    <button aria-disabled=""true"">Hello</button>
    <button aria-disabled=""false"">Oh</button>
    <fieldset disabled>
      <button>Yay</button>
    </fieldset>
    <select>
      <optgroup disabled>
        <option>one</option>
      </optgroup>
      <optgroup>
        <option>two</option>
      </optgroup>
      <option disabled>three</option>
    </select>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=button[disabled]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button disabled=\"\">Bye</button>", "<button aria-disabled=\"true\">Hello</button>", "<button>Yay</button>" }));
            Assert.That(await page.Locator("role=button[disabled=true]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button disabled=\"\">Bye</button>", "<button aria-disabled=\"true\">Hello</button>", "<button>Yay</button>" }));
            Assert.That(await page.GetByRole("button", disabled: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button disabled=\"\">Bye</button>", "<button aria-disabled=\"true\">Hello</button>", "<button>Yay</button>" }));
            Assert.That(await page.Locator("role=button[disabled=false]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Hi</button>", "<button aria-disabled=\"false\">Oh</button>" }));
            Assert.That(await page.GetByRole("button", disabled: false).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>Hi</button>", "<button aria-disabled=\"false\">Oh</button>" }));
            Assert.That(await page.GetByRole("option", disabled: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option>one</option>", "<option disabled=\"\">three</option>" }));
            Assert.That(await page.GetByRole("option", disabled: false).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option>two</option>" }));
            Assert.That(await page.GetByRole("option").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option>one</option>", "<option>two</option>", "<option disabled=\"\">three</option>" }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should inherit disabled from the ancestor")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldInheritDisabledFromTheAncestor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <span aria-disabled=""true"">
      <button>Click me!</button>
    </span>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("button")).ToBeDisabledAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <span aria-disabled=""true"">
      <h1>Heading</h1>
    </span>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.Locator("h1")).Not.ToBeDisabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support disabled fieldset")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportDisabledFieldset()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <fieldset disabled>
      <input></input>
      <button data-testid=""inside-fieldset-element"">x</button>
      <legend>
        <button data-testid=""inside-legend-element"">legend</button>
      </legend>
    </fieldset>

    <fieldset disabled>
      <legend>
        <div>
          <button data-testid=""nested-inside-legend-element"">x</button>
        </div>
      </legend>
    </fieldset>

    <fieldset disabled>
      <div></div>
      <legend>
        <button data-testid=""first-legend-element"">x</button>
      </legend>
      <legend>
        <button data-testid=""second-legend-element"">x</button>
      </legend>
    </fieldset>

    <fieldset disabled>
      <fieldset>
        <button data-testid=""deep-button"">x</button>
      </fieldset>
    </fieldset>
  ").ConfigureAwait(false);

            await Assertions.Expect(page.GetByTestId("inside-legend-element")).ToBeEnabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("nested-inside-legend-element")).ToBeEnabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("first-legend-element")).ToBeEnabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("second-legend-element")).ToBeDisabledAsync().ConfigureAwait(false);
            await Assertions.Expect(page.GetByTestId("deep-button")).ToBeDisabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support level")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportLevel()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <h1>Hello</h1>
    <h3>Hi</h3>
    <div role=""heading"" aria-level=""5"">Bye</div>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=heading[level=1]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<h1>Hello</h1>" }));
            Assert.That(await page.GetByRole("heading", level: 1).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<h1>Hello</h1>" }));
            Assert.That(await page.Locator("role=heading[level=3]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<h3>Hi</h3>" }));
            Assert.That(await page.GetByRole("heading", level: 3).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<h3>Hi</h3>" }));
            Assert.That(await page.Locator("role=heading[level=5]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"heading\" aria-level=\"5\">Bye</div>" }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should filter hidden, unless explicitly asked for")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFilterHiddenUnlessExplicitlyAskedFor()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <button>Hi</button>
    <button hidden>Hello</button>
    <button aria-hidden=""true"">Yay</button>
    <button aria-hidden=""false"">Nay</button>
    <button style=""visibility:hidden"">Bye</button>
    <div style=""visibility:hidden"">
      <button>Oh</button>
    </div>
    <div style=""visibility:hidden"">
      <button style=""visibility:visible"">Still here</button>
    </div>
    <button style=""display:none"">Never</button>
    <div id=host1></div>
    <div id=host2 style=""display:none""></div>

    <input name=""one"">
    <details>
      <summary>Open form</summary>
      <label>
         Label
         <input name=""two"">
      </label>
    </details>

    <select>
      <option style=""visibility:hidden"">One</option>
      <option style=""display:none"">Two</option>
      <option>Three</option>
    </select>

    <script>
      function addButton(host, text) {
        const root = host.attachShadow({ mode: 'open' });
        const button = document.createElement('button');
        button.textContent = text;
        root.appendChild(button);
      }
      addButton(document.getElementById('host1'), 'Shadow1');
      addButton(document.getElementById('host2'), 'Shadow2');
    </script>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=button").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<button>Hi</button>",
                "<button aria-hidden=\"false\">Nay</button>",
                "<button style=\"visibility:visible\">Still here</button>",
                "<button>Shadow1</button>",
            }));
            Assert.That(await page.Locator("role=button[include-hidden]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<button>Hi</button>",
                "<button hidden=\"\">Hello</button>",
                "<button aria-hidden=\"true\">Yay</button>",
                "<button aria-hidden=\"false\">Nay</button>",
                "<button style=\"visibility:hidden\">Bye</button>",
                "<button>Oh</button>",
                "<button style=\"visibility:visible\">Still here</button>",
                "<button style=\"display:none\">Never</button>",
                "<button>Shadow1</button>",
                "<button>Shadow2</button>",
            }));
            Assert.That(await page.Locator("role=button[include-hidden=true]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<button>Hi</button>",
                "<button hidden=\"\">Hello</button>",
                "<button aria-hidden=\"true\">Yay</button>",
                "<button aria-hidden=\"false\">Nay</button>",
                "<button style=\"visibility:hidden\">Bye</button>",
                "<button>Oh</button>",
                "<button style=\"visibility:visible\">Still here</button>",
                "<button style=\"display:none\">Never</button>",
                "<button>Shadow1</button>",
                "<button>Shadow2</button>",
            }));
            Assert.That(await page.Locator("role=button[include-hidden=false]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<button>Hi</button>",
                "<button aria-hidden=\"false\">Nay</button>",
                "<button style=\"visibility:visible\">Still here</button>",
                "<button>Shadow1</button>",
            }));
            Assert.That(await page.Locator("role=textbox").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<input name=\"one\">" }));
            Assert.That(await page.Locator("role=option").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<option style=\"visibility:hidden\">One</option>", "<option>Three</option>" }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportName()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div role=""button"" aria-label="" Hello ""></div>
    <div role=""button"" aria-label=""Hallo""></div>
    <div role=""button"" aria-label=""Hello"" aria-hidden=""true""></div>
    <div role=""button"" aria-label=""123"" aria-hidden=""true""></div>
    <div role=""button"" aria-label='foo""bar' aria-hidden=""true""></div>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=button[name=\"Hello\"]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>" }));
            Assert.That(await page.Locator("role=button[name=\" \n Hello \"]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>" }));
            Assert.That(await page.GetByRole("button", name: "Hello").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>" }));
            Assert.That(await page.Locator("role=button[name*=\"all\"]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\"Hallo\"></div>" }));
            Assert.That(await page.Locator("role=button[name=/^H[ae]llo$/]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hallo\"></div>" }));
            Assert.That(await page.GetByRole("button", nameRegex: new Regex("^H[ae]llo$")).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hallo\"></div>" }));
            Assert.That(await page.Locator("role=button[name=/h.*o/i]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hallo\"></div>" }));
            Assert.That(await page.GetByRole("button", nameRegex: new Regex("h.*o", RegexOptions.IgnoreCase)).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hallo\"></div>" }));
            Assert.That(await page.Locator("role=button[name=\"Hello\"][include-hidden]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hello\" aria-hidden=\"true\"></div>" }));
            Assert.That(await page.GetByRole("button", name: "Hello", includeHidden: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hello\" aria-hidden=\"true\"></div>" }));
            Assert.That(await page.GetByRole("button", name: "hello", includeHidden: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>", "<div role=\"button\" aria-label=\"Hello\" aria-hidden=\"true\"></div>" }));
            Assert.That(await page.Locator("role=button[name=Hello]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\" Hello \"></div>" }));
            Assert.That(await page.Locator("role=button[name=123][include-hidden]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\"123\" aria-hidden=\"true\"></div>" }));
            Assert.That(await page.GetByRole("button", name: "123", includeHidden: true).EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<div role=\"button\" aria-label=\"123\" aria-hidden=\"true\"></div>" }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support option name with html whitespace")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportOptionNameWithHtmlWhitespace()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <select>
      <option value=""html"">&nbsp;HTML</option>
    </select>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("option", name: "HTML")).ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-role.spec.ts", "errors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task Errors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            PlaywrightNativeException e0 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=[bar]"));
            Assert.That(e0.Message, Does.Contain("Role must not be empty"));

            PlaywrightNativeException e1 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=foo[sElected]"));
            Assert.That(e1.Message, Does.Contain("Unknown attribute \"sElected\", must be one of \"checked\", \"description\", \"disabled\", \"expanded\", \"include-hidden\", \"level\", \"name\", \"pressed\", \"selected\""));

            PlaywrightNativeException e2 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=foo[bar . qux=true]"));
            Assert.That(e2.Message, Does.Contain("Unknown attribute \"bar.qux\""));

            PlaywrightNativeException e3 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=heading[level=\"bar\"]"));
            Assert.That(e3.Message, Does.Contain("\"level\" attribute must be compared to a number"));

            PlaywrightNativeException e4 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=checkbox[checked=\"bar\"]"));
            Assert.That(e4.Message, Does.Contain("\"checked\" must be one of true, false, \"mixed\""));

            PlaywrightNativeException e5 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=checkbox[checked~=true]"));
            Assert.That(e5.Message, Does.Contain("cannot use ~= in attribute with non-string matching value"));

            PlaywrightNativeException e6 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=button[level=3]"));
            Assert.That(e6.Message, Does.Contain("\"level\" attribute is only supported for roles: \"heading\", \"listitem\", \"row\", \"treeitem\""));

            PlaywrightNativeException e7 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=button[name]"));
            Assert.That(e7.Message, Does.Contain("\"name\" attribute must have a value"));

            PlaywrightNativeException e8 = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("role=treeitem[expanded=\"none\"]"));
            Assert.That(e8.Message, Does.Contain("\"expanded\" must be one of true, false"));
        }

        [PlaywrightTest("selectors-role.spec.ts", "hidden with shadow dom slots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HiddenWithShadowDomSlots()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <div make-hidden>
      <button>hidden1</button>
    </div>
    <div make-hidden>
      <span><button>hidden2</button></v>
    </div>
    <div>
      <button>visible1</button>
    </div>
    <div>
      <span><button>visible2</button></span>
    </div>
    <script>
      for (const div of document.querySelectorAll('div')) {
        const hidden = div.hasAttribute('make-hidden');
        div.attachShadow({ mode: 'open' }).innerHTML = hidden ? 'nothing to see here' : '<slot></slot>';
      }
    </script>
  ").ConfigureAwait(false);
            Assert.That(await page.Locator("role=button").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[] { "<button>visible1</button>", "<button>visible2</button>" }));
            Assert.That(await page.Locator("role=button[include-hidden]").EvaluateAllAsync<string[]>("els => els.map(e => e.outerHTML)").ConfigureAwait(false), Is.EqualTo(new[]
            {
                "<button>hidden1</button>",
                "<button>hidden2</button>",
                "<button>visible1</button>",
                "<button>visible2</button>",
            }));
        }

        [PlaywrightTest("selectors-role.spec.ts", "should support output accessible name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportOutputAccessibleName()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<label>Output1<output>output</output></label>").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("status", name: "Output1")).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-role.spec.ts", "should not match scope by default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotMatchScopeByDefault()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync(@"
    <ul>
      <li aria-label=""Parent list"">
        Parent list
        <ul>
          <li>child 1</li>
          <li>child 2</li>
        </ul>
      </li>
    </ul>
  ").ConfigureAwait(false);
            ILocator children = page.GetByRole("listitem", name: "Parent list").GetByRole("listitem");
            await Assertions.Expect(children).ToHaveCountAsync(2).ConfigureAwait(false);
            await Assertions.Expect(children).ToHaveTextAsync(new[] { "child 1", "child 2" }).ConfigureAwait(false);
        }
    }
}
