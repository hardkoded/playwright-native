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
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/role-utils.spec.ts</c> parity. Upstream reads names and
    /// roles through <c>window.__injectedScript.utils</c>; here the same upstream
    /// <c>roleUtils</c> bundle that backs the role engine is evaluated in the page.
    /// </summary>
    [TestFixture]
    public class LibraryRoleUtilsParityTests : PageTestEx
    {
        private static readonly string[] _wptRanges =
        {
            "name_1.0_combobox-focusable-alternative-manual.html",
            "name_test_case_539-manual.html",
            "name_test_case_721-manual.html",
        };

        private static readonly string _nameAndRoleScript = "e => { " + RoleSelectorEngine.RoleUtilsSource + @"
  return { name: roleUtils.getElementAccessibleNameText(e), role: roleUtils.getAriaRole(e) };
}";

        private static readonly string _nameScript = "e => { " + RoleSelectorEngine.RoleUtilsSource + @"
  return roleUtils.getElementAccessibleNameText(e);
}";

        [PlaywrightTest("role-utils.spec.ts", "wpt accname #0")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task WptAccname0() => WptAccnameAsync(0);

        [PlaywrightTest("role-utils.spec.ts", "wpt accname #1")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task WptAccname1() => WptAccnameAsync(1);

        [PlaywrightTest("role-utils.spec.ts", "wpt accname #2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task WptAccname2() => WptAccnameAsync(2);

        [PlaywrightTest("role-utils.spec.ts", "wpt accname #3")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task WptAccname3() => WptAccnameAsync(3);

        [PlaywrightTest("role-utils.spec.ts", "wpt accname non-manual")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task WptAccnameNonManual()
        {
            await Page.AddInitScriptAsync(@"() => {
    const self = window;
    self.AriaUtils = {};
    self.AriaUtils.verifyLabelsBySelector = selector => self.__selector = selector;
  }").ConfigureAwait(false);

            string[] failing =
            {
                // Chromium thinks it should use "3" from the span, but Safari does not. Spec is unclear.
                "checkbox label with embedded combobox (span)",
                "checkbox label with embedded combobox (div)",

                // We do not allow nested visible elements inside parent invisible. Chromium does, but Safari does not. Spec is unclear.
                "heading with name from content, containing element that is visibility:hidden with nested content that is visibility:visible",

                // TODO: dd/dt elements have roles that prohibit naming. However, both Chromium and Safari still support naming.
                "label valid on dd element",
                "label valid on dt element",

                // TODO: recursive bugs
                "heading with link referencing image using aria-labelledby, that in turn references text element via aria-labelledby",
                "heading with link referencing image using aria-labelledby, that in turn references itself and another element via aria-labelledby",
                "button's hidden referenced name (visibility:hidden) with hidden aria-labelledby traversal falls back to aria-label",

                // TODO: preserve "tab" character and non-breaking-spaces from "aria-label" attribute
                "link with text node, with tab char",
                "nav with trailing nbsp char aria-label is valid (nbsp is preserved in name)",
                "button with leading nbsp char in aria-label is valid (and uses aria-label)",
            };

            string testDir = TestUtils.GetWebServerFile("wpt/accname/name");
            List<string> testFiles = HtmlFileNames(testDir).Select(name => "/wpt/accname/name/" + name).ToList();
            testFiles.AddRange(HtmlFileNames(Path.Combine(testDir, "shadowdom")).Select(name => "/wpt/accname/name/shadowdom" + name));

            string script = "() => { " + RoleSelectorEngine.RoleUtilsSource + @"
  const result = [];
  for (const element of document.querySelectorAll(window.__selector)) {
    const title = element.getAttribute('data-testname');
    const expected = element.getAttribute('data-expectedlabel');
    const received = roleUtils.getElementAccessibleNameText(element);
    result.push({ title, expected, received });
  }
  return result;
}";
            await Assert.MultipleAsync(async () =>
            {
                foreach (string testFile in testFiles)
                {
                    await Page.GoToAsync(TestConstants.ServerUrl + testFile).ConfigureAwait(false);
                    LabelResult[] result = await Page.EvaluateAsync<LabelResult[]>(script).ConfigureAwait(false);
                    foreach (LabelResult item in result)
                    {
                        if (!failing.Contains(item.Title))
                        {
                            Assert.That(item.Received, Is.EqualTo(item.Expected), $"{testFile}: {item.Title}");
                        }
                    }
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "axe-core implicit-role")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AxeCoreImplicitRole()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            AxeTestCase[] testCases = await RequireAxeTestCasesAsync("axe-core/implicit-role.js").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                foreach (AxeTestCase testCase in testCases)
                {
                    await Page.SetContentAsync($@"
        <body>
          {testCase.Html}
        </body>
      ").ConfigureAwait(false);
                    string received = await Page.EvalOnSelectorAsync<string>(
                        "body",
                        "(_, selector) => { " + RoleSelectorEngine.RoleUtilsSource + @"
  const element = document.querySelector(selector);
  if (!element)
    throw new Error(`Unable to resolve ""${selector}""`);
  return roleUtils.getAriaRole(element);
}",
                        (string)testCase.Target).ConfigureAwait(false);
                    Assert.That(received, Is.EqualTo(testCase.Role), $"checking {testCase.Html}");
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "axe-core accessible-text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AxeCoreAccessibleText()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            AxeTestCase[] testCases = await RequireAxeTestCasesAsync("axe-core/accessible-text.js").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                foreach (AxeTestCase testCase in testCases)
                {
                    await Page.SetContentAsync($@"
        <body>
          {testCase.Html}
        </body>
        <script>
          for (const template of document.querySelectorAll(""template[shadow]"")) {{
            const shadowRoot = template.parentElement.attachShadow({{ mode: 'open' }});
            shadowRoot.appendChild(template.content);
            template.remove();
          }}
        </script>
      ").ConfigureAwait(false);
                    string[] targets = ToArray(testCase.Target);
                    string[] expected = ToArray(testCase.AccessibleText);
                    List<string> received = new List<string>();
                    foreach (string selector in targets)
                    {
                        IElementHandle element = await Page.QuerySelectorAsync("css=" + selector).ConfigureAwait(false)
                            ?? throw new InvalidOperationException($"Unable to resolve \"{selector}\"");
                        received.Add(await element.EvaluateAsync<string>(_nameScript).ConfigureAwait(false));
                    }

                    Assert.That(received, Is.EqualTo(expected), $"checking {testCase.Html}");
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "accessible name with slots")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AccessibleNameWithSlots()
        {
            await Assert.MultipleAsync(async () =>
            {
                // Text "foo" is assigned to the slot, should not be used twice.
                await Page.SetContentAsync(@"
    <button><div>foo</div></button>
    <script>
      (() => {
        const container = document.querySelector('div');
        const shadow = container.attachShadow({ mode: 'open' });
        const slot = document.createElement('slot');
        shadow.appendChild(slot);
      })();
    </script>
  ").ConfigureAwait(false);
                Assert.That(await GetNameAndRoleAsync("button").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "foo")));

                // Text "foo" is assigned to the slot, should be used instead of slot content.
                await Page.SetContentAsync(@"
    <div>foo</div>
    <script>
      (() => {
        const container = document.querySelector('div');
        const shadow = container.attachShadow({ mode: 'open' });
        const button = document.createElement('button');
        shadow.appendChild(button);
        const slot = document.createElement('slot');
        button.appendChild(slot);
        const span = document.createElement('span');
        span.textContent = 'pre';
        slot.appendChild(span);
      })();
    </script>
  ").ConfigureAwait(false);
                Assert.That(await GetNameAndRoleAsync("button").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "foo")));

                // Nothing is assigned to the slot, should use slot content.
                await Page.SetContentAsync(@"
    <div></div>
    <script>
      (() => {
        const container = document.querySelector('div');
        const shadow = container.attachShadow({ mode: 'open' });
        const button = document.createElement('button');
        shadow.appendChild(button);
        const slot = document.createElement('slot');
        button.appendChild(slot);
        const span = document.createElement('span');
        span.textContent = 'pre';
        slot.appendChild(span);
      })();
    </script>
  ").ConfigureAwait(false);
                Assert.That(await GetNameAndRoleAsync("button").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "pre")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "accessible name nested treeitem")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task AccessibleNameNestedTreeitem()
        {
            await Page.SetContentAsync(@"
    <div role=treeitem id=target>
      <span>Top-level</span>
      <div role=group>
        <div role=treeitem><span>Nested 1</span></div>
        <div role=treeitem><span>Nested 2</span></div>
      </div>
    </div>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("#target").ConfigureAwait(false), Is.EqualTo(new NameAndRole("treeitem", "Top-level")));
        }

        [PlaywrightTest("role-utils.spec.ts", "svg title")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SvgTitle()
        {
            await Page.SetContentAsync(@"
    <div>
      <svg width=""162"" height=""30"" viewBox=""0 0 162 30"" fill=""none"" xmlns=""http://www.w3.org/2000/svg"">
        <title>Submit</title>
        <g>
          <title>Hello</title>
        </g>
        <a href=""example.com"" xlink:title=""a link""><circle cx=""50"" cy=""40"" r=""35"" /></a>
      </svg>
    </div>
  ").ConfigureAwait(false);

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("svg").ConfigureAwait(false), Is.EqualTo(new NameAndRole("img", "Submit")));
                Assert.That(await GetNameAndRoleAsync("g").ConfigureAwait(false), Is.EqualTo(new NameAndRole(null, "Hello")));
                Assert.That(await GetNameAndRoleAsync("a").ConfigureAwait(false), Is.EqualTo(new NameAndRole("link", "a link")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "native controls")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NativeControls()
        {
            await Page.SetContentAsync(@"
    <label for=""text1"">TEXT1</label><input id=""text1"" type=text>
    <input id=""text2"" type=text title=""TEXT2"">
    <input id=""text3"" type=text placeholder=""TEXT3"">
    <input id=""number1"" type=number placeholder=""NUMBER1"">

    <label for=""image1"">IMAGE1</label><input id=""image1"" type=image>
    <input id=""image2"" type=image alt=""IMAGE2"">
    <label for=""image3"">IMAGE3</label><input id=""image3"" type=image alt=""MORE3"">

    <label for=""button1"">BUTTON1</label><button id=""button1"" role=""combobox"">button</button>
    <button id=""button2"" role=""combobox"">BUTTON2</button>
    <button id=""button3"">BUTTON3</button>
    <button id=""button4"" title=""BUTTON4""></button>

    <input id=""file1"" type=file>
    <label for=""file2"">FILE2</label><input id=""file2"" type=file>
  ").ConfigureAwait(false);

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#text1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXT1")));
                Assert.That(await GetNameAndRoleAsync("#text2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXT2")));
                Assert.That(await GetNameAndRoleAsync("#text3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXT3")));
                Assert.That(await GetNameAndRoleAsync("#number1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("spinbutton", "NUMBER1")));
                Assert.That(await GetNameAndRoleAsync("#image1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "IMAGE1")));
                Assert.That(await GetNameAndRoleAsync("#image2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "IMAGE2")));
                Assert.That(await GetNameAndRoleAsync("#image3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "IMAGE3")));
                Assert.That(await GetNameAndRoleAsync("#button1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("combobox", "BUTTON1")));
                Assert.That(await GetNameAndRoleAsync("#button2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("combobox", string.Empty)));
                Assert.That(await GetNameAndRoleAsync("#button3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "BUTTON3")));
                Assert.That(await GetNameAndRoleAsync("#button4").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "BUTTON4")));
                Assert.That(await GetNameAndRoleAsync("#file1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Choose File")));
                Assert.That(await GetNameAndRoleAsync("#file2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "FILE2")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "input type=search maps to searchbox unless list points at a datalist")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InputTypeSearchMapsToSearchboxUnlessListPointsAtADatalist()
        {
            await Page.SetContentAsync(@"
    <input id=""search1"" type=search>
    <input id=""search2"" type=search list=nope>
    <input id=""search3"" type=search list=dv><div id=dv></div>
    <input id=""search4"" type=search list=dl><datalist id=dl></datalist>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#search1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("searchbox", string.Empty)));
                Assert.That(await GetNameAndRoleAsync("#search2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("searchbox", string.Empty)));
                Assert.That(await GetNameAndRoleAsync("#search3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("searchbox", string.Empty)));
                Assert.That(await GetNameAndRoleAsync("#search4").ConfigureAwait(false), Is.EqualTo(new NameAndRole("combobox", string.Empty)));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "meter and progress get their name from an associated label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MeterAndProgressGetTheirNameFromAnAssociatedLabel()
        {
            await Page.SetContentAsync(@"
    <label for=""meter1"">Battery</label><meter id=""meter1"" value=0.5></meter>
    <label for=""progress1"">Loading</label><progress id=""progress1"" value=0.3></progress>
    <label>Charge <meter id=""meter2"" value=0.5></meter></label>
    <label for=""meter3"">Ignored</label><meter id=""meter3"" aria-label=""Overridden"" value=0.5></meter>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#meter1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("meter", "Battery")));
                Assert.That(await GetNameAndRoleAsync("#progress1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("progressbar", "Loading")));
                Assert.That(await GetNameAndRoleAsync("#meter2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("meter", "Charge")));
                Assert.That(await GetNameAndRoleAsync("#meter3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("meter", "Overridden")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "native controls labelled-by")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NativeControlsLabelledBy()
        {
            await Page.SetContentAsync(@"
    <label id=""for-text1"">TEXT1</label><input aria-labelledby=""for-text1"" id=""text1"" type=text>
    <label id=""for-text2"">TEXT2</label><input aria-labelledby=""for-text2 text2"" id=""text2"" type=text>
    <label id=""for-text3"" for=""text3"">TEXT3</label><input aria-labelledby=""for-text3 text3"" id=""text3"" type=text>

    <label id=""for-submit1"" for=""submit1"">SUBMIT1</label><input aria-labelledby=""for-submit1 submit1"" id=""submit1"" type=submit>
    <label id=""for-image1"" for=""image1"">IMAGE1</label><input aria-labelledby=""for-image1 image1"" id=""image1"" type=image alt=""MORE1"">
    <label id=""for-image2"" for=""image2"">IMAGE2</label><img aria-labelledby=""for-image2 image2"" id=""image2"" alt=""MORE2"" src=""data:image/svg,<g></g>"">

    <label id=""for-button1"">BUTTON1</label><button aria-labelledby=""for-button1"" id=""button1"">MORE1</button>
    <label id=""for-button2"">BUTTON2</label><button aria-labelledby=""for-button2 button2"" id=""button2"">MORE2</button>
    <label id=""for-button3"" for=""button3"">BUTTON3</label><button aria-labelledby=""for-button3 button3"" id=""button3"">MORE3</button>
    <label id=""for-button4"" for=""button4"">BUTTON4</label><button aria-labelledby=""for-button4"" id=""button4"">MORE4</button>

    <label id=""for-textarea1"" for=""textarea1"">TEXTAREA1</label><textarea aria-labelledby=""for-textarea1 textarea1"" id=""textarea1"" placeholder=""MORE1"">MORE2</textarea>
  ").ConfigureAwait(false);

            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#text1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXT1")));
                Assert.That(await GetNameAndRoleAsync("#text2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXT2")));
                Assert.That(await GetNameAndRoleAsync("#text3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXT3")));
                Assert.That(await GetNameAndRoleAsync("#submit1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "SUBMIT1 Submit")));
                Assert.That(await GetNameAndRoleAsync("#image1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "IMAGE1 MORE1")));
                Assert.That(await GetNameAndRoleAsync("#image2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("img", "IMAGE2 MORE2")));
                Assert.That(await GetNameAndRoleAsync("#button1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "BUTTON1")));
                Assert.That(await GetNameAndRoleAsync("#button2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "BUTTON2 MORE2")));
                Assert.That(await GetNameAndRoleAsync("#button3").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "BUTTON3 MORE3")));
                Assert.That(await GetNameAndRoleAsync("#button4").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "BUTTON4")));
                Assert.That(await GetNameAndRoleAsync("#textarea1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "TEXTAREA1 MORE2")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "display:contents should be visible when contents are visible")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DisplayContentsShouldBeVisibleWhenContentsAreVisible()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <button style='display: contents;'>yo</button>
  ").ConfigureAwait(false);
            await Assertions.Expect(page.GetByRole("button")).ToHaveCountAsync(1).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "should remove soft hyphens and zero-width spaces")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveSoftHyphensAndZeroWidthSpaces()
        {
            await Page.SetContentAsync("\n    <button>1­2​3</button>\n  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("button").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "123")));
        }

        [PlaywrightTest("role-utils.spec.ts", "label/labelled-by aria-hidden with descendants")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LabelLabelledByAriaHiddenWithDescendants()
        {
            // https://github.com/microsoft/playwright/issues/29796
            await Page.SetContentAsync(@"
    <body>
      <div id=""case1"">
        <button aria-labelledby=""label1"" type=""button""></button>
        <tool-tip id=""label1"" for=""button-preview"" popover=""manual"" aria-hidden=""true"" role=""tooltip"">Label1</tool-tip>
      </div>
      <div id=""case2"">
        <label for=""button2"" aria-hidden=""true""><div id=""label2"">Label2</div></label>
        <button id=""button2"" type=""button""></button>
      </div>
    </body>
  ").ConfigureAwait(false);
            await Page.EvalOnSelectorAllAsync("#label1, #label2", @"els => {
    els.forEach(el => el.attachShadow({ mode: 'open' }).appendChild(document.createElement('slot')));
  }").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#case1 button").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Label1")));
                Assert.That(await GetNameAndRoleAsync("#case2 button").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Label2")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "own aria-label concatenated with aria-labelledby")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task OwnAriaLabelConcatenatedWithAriaLabelledby()
        {
            // This is taken from https://w3c.github.io/accname/#example-5-0
            await Page.SetContentAsync(@"
    <h1>Files</h1>
    <ul>
      <li>
        <a id=""file_row1"" href=""./files/Documentation.pdf"">Documentation.pdf</a>
        <span role=""button"" tabindex=""0"" id=""del_row1"" aria-label=""Delete"" aria-labelledby=""del_row1 file_row1""></span>
      </li>
      <li>
        <a id=""file_row2"" href=""./files/HolidayLetter.pdf"">HolidayLetter.pdf</a>
        <span role=""button"" tabindex=""0"" id=""del_row2"" aria-label=""Delete"" aria-labelledby=""del_row2 file_row2""></span>
      </li>
    </ul>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#del_row1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Delete Documentation.pdf")));
                Assert.That(await GetNameAndRoleAsync("#del_row2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Delete HolidayLetter.pdf")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "control embedded in a label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ControlEmbeddedInALabel()
        {
            // https://github.com/microsoft/playwright/issues/28848
            await Page.SetContentAsync(@"
    <label for=""flash"">
      <input type=""checkbox"" id=""flash"">
      Flash the screen <span tabindex=""0"" role=""textbox"" aria-label=""number of times"" contenteditable>5</span> times.
    </label>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("input").ConfigureAwait(false), Is.EqualTo(new NameAndRole("checkbox", "Flash the screen 5 times.")));
                Assert.That(await GetNameAndRoleAsync("span").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "number of times")));
                Assert.That(await GetNameAndRoleAsync("label").ConfigureAwait(false), Is.EqualTo(new NameAndRole(null, string.Empty)));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "control embedded in a target element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ControlEmbeddedInATargetElement()
        {
            // https://github.com/microsoft/playwright/issues/28848
            await Page.SetContentAsync(@"
    <h1>
      <input type=""text"" value=""Foo bar"">
    </h1>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("h1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("heading", "Foo bar")));
        }

        [PlaywrightTest("role-utils.spec.ts", "searchbox embedded control should contribute its value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SearchboxEmbeddedControlShouldContributeItsValue()
        {
            // https://github.com/microsoft/playwright/issues/42341
            await Page.SetContentAsync(@"
    <button id=""b1"" aria-labelledby=""l1""></button><div id=""l1"" hidden><input type=""text"" value=""Query""></div>
    <button id=""b2"" aria-labelledby=""l2""></button><div id=""l2"" hidden><input type=""search"" value=""Query""></div>
    <label for=""c1"">Flash the screen <input type=""search"" value=""5""> times.</label>
    <input type=""checkbox"" id=""c1"">
    <h1><input type=""search"" value=""Foo bar""></h1>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#b1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Query")));
                Assert.That(await GetNameAndRoleAsync("#b2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "Query")));
                Assert.That(await GetNameAndRoleAsync("#c1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("checkbox", "Flash the screen 5 times.")));
                Assert.That(await GetNameAndRoleAsync("h1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("heading", "Foo bar")));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "svg role=presentation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SvgRolePresentation()
        {
            // https://github.com/microsoft/playwright/issues/26809
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync(@"
		<img src=""pptr.png"" alt=""Code is Poetry."" />
		<svg viewBox=""0 0 100 100"" width=""16"" height=""16"" xmlns=""http://www.w3.org/2000/svg"" role=""presentation"" focusable=""false""><circle cx=""50"" cy=""50"" r=""50""></circle></svg>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("img").ConfigureAwait(false), Is.EqualTo(new NameAndRole("img", "Code is Poetry.")));
                Assert.That(await GetNameAndRoleAsync("svg").ConfigureAwait(false), Is.EqualTo(new NameAndRole("presentation", string.Empty)));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("role-utils.spec.ts", "should work with form and tricky input names")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithFormAndTrickyInputNames()
        {
            // https://github.com/microsoft/playwright/issues/30616
            await Page.SetContentAsync(@"
		<form aria-label=""my form"">
      <input name=""tagName"" value=""hello"" title=""tagName input"">
      <input name=""localName"" value=""hello"" title=""localName input"">
    </form>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("form").ConfigureAwait(false), Is.EqualTo(new NameAndRole("form", "my form")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should ignore stylesheet from hidden aria-labelledby subtree")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreStylesheetFromHiddenAriaLabelledbySubtree()
        {
            await Page.SetContentAsync(@"
    <div id=mylabel style=""display:none"">
      <template shadowrootmode=open>
        <style>span { color: red; }</style>
        <span>hello</span>
      </template>
    </div>
    <input aria-labelledby=mylabel type=text>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("input").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "hello")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should not include hidden pseudo into accessible name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotIncludeHiddenPseudoIntoAccessibleName()
        {
            await Page.SetContentAsync(@"
    <style>
      span:before {
        content: 'world';
        display: none;
      }
      div:after {
        content: 'bye';
        visibility: hidden;
      }
    </style>
    <a href=""http://example.com"">
      <span>hello</span>
      <div>hello</div>
    </a>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("a").ConfigureAwait(false), Is.EqualTo(new NameAndRole("link", "hello hello")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should resolve pseudo content from attr")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolvePseudoContentFromAttr()
        {
            await Page.SetContentAsync(@"
    <style>
    .stars:before {
      display: block;
      content: attr(data-hello);
    }
    </style>
    <a href=""http://example.com"">
      <div class=""stars"" data-hello=""hello"">world</div>
    </a>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("a").ConfigureAwait(false), Is.EqualTo(new NameAndRole("link", "hello world")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should resolve pseudo content alternative text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolvePseudoContentAlternativeText()
        {
            await Page.SetContentAsync(@"
    <style>
      .with-content:before {
        content: url(""data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg'></svg>"") / ""alternative text"";
      }
    </style>
    <div role=""button"" class=""with-content""> inner text</div>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("div").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "alternative text inner text")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should resolve css content property for an element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldResolveCssContentPropertyForAnElement()
        {
            await Page.SetContentAsync(@"
    <style>
      .with-content-1 {
        content: url(""data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg'></svg>"") / ""alternative text"";
      }
      .with-content-2 {
        content: url(""data:image/svg+xml,<svg xmlns='http://www.w3.org/2000/svg'></svg>"");
      }
    </style>
    <div id=""button1"" role=""button"" class=""with-content-1"">inner text</div>
    <div id=""button2"" role=""button"" class=""with-content-2"">inner text</div>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("#button1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "alternative text")));
            Assert.That(await GetNameAndRoleAsync("#button2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("button", "inner text")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should ignore invalid aria-labelledby")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIgnoreInvalidAriaLabelledby()
        {
            await Page.SetContentAsync(@"
    <label>
      <span>Text here</span>
      <input type=text aria-labelledby=""does-not-exist"">
    </label>
  ").ConfigureAwait(false);
            Assert.That(await GetNameAndRoleAsync("input").ConfigureAwait(false), Is.EqualTo(new NameAndRole("textbox", "Text here")));
        }

        [PlaywrightTest("role-utils.spec.ts", "should support search element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportSearchElement()
        {
            await Page.SetContentAsync(@"
    <search id=search1 aria-label=""example"">
      Hello
    </search>
    <search id=search2>
      World
    </search>
  ").ConfigureAwait(false);
            await Assert.MultipleAsync(async () =>
            {
                Assert.That(await GetNameAndRoleAsync("#search1").ConfigureAwait(false), Is.EqualTo(new NameAndRole("search", "example")));
                Assert.That(await GetNameAndRoleAsync("#search2").ConfigureAwait(false), Is.EqualTo(new NameAndRole("search", string.Empty)));
                await Assertions.Expect(Page.GetByRole(AriaRole.Search, new() { Name = "example" })).ToBeVisibleAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static IEnumerable<string> HtmlFileNames(string directory)
            => Directory.GetFiles(directory, "*.html")
                .Select(Path.GetFileName)
                .Order(StringComparer.Ordinal);

        private static string[] ToArray(object value)
            => value is object[] array ? array.Cast<string>().ToArray() : new[] { (string)value };

        private async Task WptAccnameAsync(int range)
        {
            string[] skipped =
            {
                // This test expects ::before + title + ::after, which is neither 2F nor 2I.
                "name_test_case_659-manual.html",
                // This test expects ::before + title + ::after, which is neither 2F nor 2I.
                "name_test_case_660-manual.html",
                // These two tests expect <input type=file title=...> to respect the title, but browsers do not.
                "name_test_case_751-manual.html",
                "name_file-title-manual.html",
                // Spec says role=combobox should use selected options, not a title attribute.
                "description_1.0_combobox-focusable-manual.html",
            };

            await Page.AddInitScriptAsync(@"() => {
      const self = window;
      self.setup = () => {};
      self.ATTAcomm = class {
        constructor(data) {
          self.steps = [];
          for (const step of data.steps) {
            if (!step.test.ATK)
              continue;
            for (const atk of step.test.ATK) {
              if (atk[0] !== 'property' || (atk[1] !== 'name' && atk[1] !== 'description') || atk[2] !== 'is' || typeof atk[3] !== 'string')
                continue;
              self.steps.push({ selector: '#' + step.element, property: atk[1], value: atk[3] });
            }
          }
        }
      };
    }").ConfigureAwait(false);

            string script = "() => { " + RoleSelectorEngine.RoleUtilsSource + @"
  const result = [];
  for (const step of window.steps) {
    const element = document.querySelector(step.selector);
    if (!element)
      throw new Error(`Unable to resolve ""${step.selector}""`);
    const received = step.property === 'name' ? roleUtils.getElementAccessibleNameText(element) : roleUtils.getElementAccessibleDescription(element).text;
    result.push({ selector: step.selector, expected: step.value, received });
  }
  return result;
}";

            await Assert.MultipleAsync(async () =>
            {
                foreach (string testFile in HtmlFileNames(TestUtils.GetWebServerFile("wpt/accname/manual")))
                {
                    if (skipped.Contains(testFile))
                    {
                        continue;
                    }

                    bool included = (range == 0 || string.CompareOrdinal(testFile, _wptRanges[range - 1]) >= 0)
                        && (range == _wptRanges.Length || string.CompareOrdinal(testFile, _wptRanges[range]) < 0);
                    if (!included)
                    {
                        continue;
                    }

                    await Page.GoToAsync(TestConstants.ServerUrl + "/wpt/accname/manual/" + testFile).ConfigureAwait(false);
                    StepResult[] result = await Page.EvaluateAsync<StepResult[]>(script).ConfigureAwait(false);
                    foreach (StepResult step in result)
                    {
                        Assert.That(step.Received, Is.EqualTo(step.Expected), $"checking \"{step.Selector}\" in {testFile}");
                    }
                }
            }).ConfigureAwait(false);
        }

        private async Task<AxeTestCase[]> RequireAxeTestCasesAsync(string asset)
        {
            // Upstream `require()`s the asset; evaluate the CommonJS module in the page instead.
            string source = await File.ReadAllTextAsync(TestUtils.GetWebServerFile(asset)).ConfigureAwait(false);
            return await Page.EvaluateAsync<AxeTestCase[]>("() => { const module = { exports: {} }; " + source + "\n return module.exports; }").ConfigureAwait(false);
        }

        private async Task<NameAndRole> GetNameAndRoleAsync(string selector)
        {
            // Page.EvaluateAsync maps camelCase results onto the DTO; ElementHandle evaluation
            // on Chromium does not, so pass the $eval element as an argument instead.
            await using IElementHandle element = await Page.QuerySelectorAsync(selector).ConfigureAwait(false)
                ?? throw new InvalidOperationException($"Unable to resolve \"{selector}\"");
            NameAndRoleResult result = await Page.EvaluateAsync<NameAndRoleResult>(_nameAndRoleScript, element).ConfigureAwait(false);
            return new NameAndRole(result.Role, result.Name);
        }

        private sealed record NameAndRole(string Role, string Name);

        private sealed class NameAndRoleResult
        {
            public string Role { get; set; }

            public string Name { get; set; }
        }

        private sealed class StepResult
        {
            public string Selector { get; set; }

            public string Expected { get; set; }

            public string Received { get; set; }
        }

        private sealed class LabelResult
        {
            public string Title { get; set; }

            public string Expected { get; set; }

            public string Received { get; set; }
        }

        private sealed class AxeTestCase
        {
            public string Html { get; set; }

            public object Target { get; set; }

            public string Role { get; set; }

            public object AccessibleText { get; set; }
        }
    }
}
