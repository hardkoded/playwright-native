/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>retarget.spec.ts</c> parity for label / button retargeting
    /// of visibility, enabled, editable, fill, select, and check.
    /// Skipped (Node-only internals: toImpl, inspector, Electron, Android): none.
    /// </summary>
    [TestFixture]
    public class RetargetParityTests : PageTestEx
    {
        [PlaywrightTest("retarget.spec.ts", "element state checks should work as expected for label with zero-sized input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ElementStateChecksShouldWorkAsExpectedForLabelWithZeroSizedInput()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(
                    "<label>\n" +
                    "      Click me\n" +
                    "      <input disabled style=\"width:0;height:0;padding:0;margin:0;border:0;\">\n" +
                    "    </label>").ConfigureAwait(false);
                Assert.That(await page.IsVisibleAsync("text=Click me").ConfigureAwait(false), Is.True);
                Assert.That(await page.IsHiddenAsync("text=Click me").ConfigureAwait(false), Is.False);
                Assert.That(await page.IsEnabledAsync("text=Click me").ConfigureAwait(false), Is.False);
                Assert.That(await page.IsDisabledAsync("text=Click me").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "should wait for enclosing disabled button")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForEnclosingDisabledButton()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button><span>Target</span></button>").ConfigureAwait(false);
                IElementHandle span = await page.QuerySelectorAsync("text=Target").ConfigureAwait(false);
                bool done = false;
                Task promise = span.WaitForElementStateAsync(ElementState.Disabled).ContinueWith(
                    _ => { done = true; },
                    TaskScheduler.Default);
                await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
                Assert.That(done, Is.False);
                await span.EvaluateAsync("span => { span.parentElement.disabled = true; }").ConfigureAwait(false);
                await promise.ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "should wait for enclosing button with a disabled fieldset")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForEnclosingButtonWithADisabledFieldset()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<fieldset disabled=true><button><span>Target</span></button></div>").ConfigureAwait(false);
                IElementHandle span = await page.QuerySelectorAsync("text=Target").ConfigureAwait(false);
                bool done = false;
                Task promise = span.WaitForElementStateAsync(ElementState.Enabled).ContinueWith(
                    _ => { done = true; },
                    TaskScheduler.Default);
                await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
                Assert.That(done, Is.False);
                await span.EvaluateAsync(
                    "span => { span.parentElement.parentElement.disabled = false; }").ConfigureAwait(false);
                await promise.ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "should wait for enclosing enabled button")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitForEnclosingEnabledButton()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<button disabled><span>Target</span></button>").ConfigureAwait(false);
                IElementHandle span = await page.QuerySelectorAsync("text=Target").ConfigureAwait(false);
                bool done = false;
                Task promise = span.WaitForElementStateAsync(ElementState.Enabled).ContinueWith(
                    _ => { done = true; },
                    TaskScheduler.Default);
                await GiveItAChanceToResolveAsync(page).ConfigureAwait(false);
                Assert.That(done, Is.False);
                await span.EvaluateAsync("span => { span.parentElement.disabled = false; }").ConfigureAwait(false);
                await promise.ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "should check the box outside shadow dom label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldCheckTheBoxOutsideShadowDomLabel()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div></div>").ConfigureAwait(false);
                await page.EvalOnSelectorAsync<object>(
                    "div",
                    @"div => {
                        const root = div.attachShadow({ mode: 'open' });
                        const label = document.createElement('label');
                        label.setAttribute('for', 'target');
                        label.textContent = 'Click me';
                        root.appendChild(label);
                        const input = document.createElement('input');
                        input.setAttribute('type', 'checkbox');
                        input.setAttribute('id', 'target');
                        root.appendChild(input);
                    }").ConfigureAwait(false);
                await page.CheckAsync("label").ConfigureAwait(false);
                Assert.That(await page.EvalOnSelectorAsync<bool>("input", "input => input.checked").ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "setInputFiles should work with label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetInputFilesShouldWorkWithLabel()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<label for=target>Choose a file</label><input id=target type=file>").ConfigureAwait(false);
                await page.SetInputFilesAsync("text=Choose a file", TestConstants.FileToUpload).ConfigureAwait(false);
                Assert.That(await page.EvalOnSelectorAsync<int>("input", "input => input.files.length").ConfigureAwait(false), Is.EqualTo(1));
                Assert.That(
                    await page.EvalOnSelectorAsync<string>("input", "input => input.files[0].name").ConfigureAwait(false),
                    Is.EqualTo("file-to-upload.txt"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "enabled/disabled retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task EnabledDisabledRetargeting()
        {
            Case[] cases =
            {
                new Case(DomInLabel("<input id=target>"), enabled: true, locator: "label"),
                new Case(DomLabelFor("<input id=target>"), enabled: true, locator: "label"),
                new Case(DomStandalone("<input id=target>"), enabled: true, locator: "input"),
                new Case(DomInButton("<input id=target>"), enabled: true, locator: "input"),
                new Case(DomInLink("<input id=target>"), enabled: true, locator: "input"),
                new Case(DomInButton("<input id=target>", disabled: true), enabled: true, locator: "input"),
                new Case(DomInLabel("<li role=menuitem id=target aria-disabled=false></li>"), enabled: true, locator: "li"),
                new Case(DomInLabel("<input id=target disabled>"), enabled: false, locator: "label"),
                new Case(DomLabelFor("<input id=target disabled>"), enabled: false, locator: "label"),
                new Case(DomStandalone("<input id=target disabled>"), enabled: false, locator: "input"),
                new Case(DomInButton("<input id=target disabled>"), enabled: false, locator: "input"),
                new Case(DomInLink("<input id=target disabled>"), enabled: false, locator: "input"),
                new Case(DomInButton("<input id=target disabled>", disabled: true), enabled: false, locator: "input"),
                new Case(DomInLabel("<li role=menuitem id=target aria-disabled=true></li>"), enabled: false, locator: "li"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    IElementHandle handle = await page.QuerySelectorAsync(item.Locator).ConfigureAwait(false);
                    Assert.That(await target.IsEnabledAsync().ConfigureAwait(false), Is.EqualTo(item.Enabled), item.Dom);
                    Assert.That(await target.IsDisabledAsync().ConfigureAwait(false), Is.EqualTo(!item.Enabled), item.Dom);
                    if (item.Enabled)
                    {
                        await Assertions.Expect(target).ToBeEnabledAsync().ConfigureAwait(false);
                        await Assertions.Expect(target).Not.ToBeDisabledAsync().ConfigureAwait(false);
                        await handle.WaitForElementStateAsync(ElementState.Enabled).ConfigureAwait(false);
                    }
                    else
                    {
                        await Assertions.Expect(target).Not.ToBeEnabledAsync().ConfigureAwait(false);
                        await Assertions.Expect(target).ToBeDisabledAsync().ConfigureAwait(false);
                        await handle.WaitForElementStateAsync(ElementState.Disabled).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "visible/hidden retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task VisibleHiddenRetargeting()
        {
            Case[] cases =
            {
                new Case(DomInLabel("<span id=target>content</span>"), visible: true, locator: "label"),
                new Case(DomInLabel("<span id=target hidden>content</span>"), visible: true, locator: "label"),
                new Case(DomLabelFor("<span id=target>content</span>"), visible: true, locator: "label"),
                new Case(DomLabelFor("<span id=target hidden>content</span>"), visible: true, locator: "label"),
                new Case(DomStandalone("<span id=target>content</span>"), visible: true, locator: "span"),
                new Case(DomInButton("<span id=target>content</span>"), visible: true, locator: "span"),
                new Case(DomInLink("<span id=target>content</span>"), visible: true, locator: "span"),
                new Case(DomInLabel("<span id=target>content</span>", hidden: true), visible: false, locator: "label"),
                new Case(DomLabelFor("<span id=target>content</span>", hidden: true), visible: false, locator: "label"),
                new Case(DomStandalone("<span id=target hidden>content</span>"), visible: false, locator: "span"),
                new Case(DomInButton("<span id=target hidden>content</span>"), visible: false, locator: "span"),
                new Case(DomInButton("<span id=target>content</span>", hidden: true), visible: false, locator: "span"),
                new Case(DomInLink("<span id=target hidden>content</span>"), visible: false, locator: "span"),
                new Case(DomInLink("<span id=target>content</span>", hidden: true), visible: false, locator: "span"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    IElementHandle handle = await page.QuerySelectorAsync(item.Locator).ConfigureAwait(false);
                    Assert.That(await target.IsVisibleAsync().ConfigureAwait(false), Is.EqualTo(item.Visible), item.Dom);
                    Assert.That(await target.IsHiddenAsync().ConfigureAwait(false), Is.EqualTo(!item.Visible), item.Dom);
                    if (item.Visible)
                    {
                        await Assertions.Expect(target).ToBeVisibleAsync().ConfigureAwait(false);
                        await Assertions.Expect(target).Not.ToBeHiddenAsync().ConfigureAwait(false);
                        await handle.WaitForElementStateAsync(ElementState.Visible).ConfigureAwait(false);
                    }
                    else
                    {
                        await Assertions.Expect(target).Not.ToBeVisibleAsync().ConfigureAwait(false);
                        await Assertions.Expect(target).ToBeHiddenAsync().ConfigureAwait(false);
                        await handle.WaitForElementStateAsync(ElementState.Hidden).ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "editable retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task EditableRetargeting()
        {
            Case[] cases =
            {
                new Case(DomInLabel("<input id=target>"), editable: true, locator: "label"),
                new Case(DomLabelFor("<input id=target>"), editable: true, locator: "label"),
                new Case(DomStandalone("<input id=target>"), editable: true, locator: "input"),
                new Case(DomInButton("<input id=target>"), editable: true, locator: "input"),
                new Case(DomInLink("<input id=target>"), editable: true, locator: "input"),
                new Case(DomInButton("<input id=target>", readOnly: true), editable: true, locator: "input"),
                new Case(DomInLabel("<input id=target readonly>"), editable: false, locator: "label"),
                new Case(DomLabelFor("<input id=target readonly>"), editable: false, locator: "label"),
                new Case(DomStandalone("<input id=target readonly>"), editable: false, locator: "input"),
                new Case(DomInButton("<input id=target readonly>"), editable: false, locator: "input"),
                new Case(DomInLink("<input id=target readonly>"), editable: false, locator: "input"),
                new Case(DomInButton("<input id=target readonly>", readOnly: true), editable: false, locator: "input"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    IElementHandle handle = await page.QuerySelectorAsync(item.Locator).ConfigureAwait(false);
                    Assert.That(await target.IsEditableAsync().ConfigureAwait(false), Is.EqualTo(item.Editable), item.Dom);
                    if (item.Editable)
                    {
                        await Assertions.Expect(target).ToBeEditableAsync().ConfigureAwait(false);
                        await handle.WaitForElementStateAsync(ElementState.Editable).ConfigureAwait(false);
                    }
                    else
                    {
                        await Assertions.Expect(target).Not.ToBeEditableAsync().ConfigureAwait(false);
                    }
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "input value retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task InputValueRetargeting()
        {
            Case[] cases =
            {
                new Case(DomInLabel("<input id=target>"), locator: "label"),
                new Case(DomLabelFor("<input id=target>"), locator: "label"),
                new Case(DomStandalone("<input id=target>"), locator: "input"),
                new Case(DomInButton("<input id=target>"), locator: "input"),
                new Case(DomInLink("<input id=target>"), locator: "input"),
                new Case(DomInButton("<input id=target>"), locator: "input"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    IElementHandle handle = await page.QuerySelectorAsync(item.Locator).ConfigureAwait(false);
                    Assert.That(await target.InputValueAsync().ConfigureAwait(false), Is.EqualTo(string.Empty), item.Dom);
                    Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo(string.Empty), item.Dom);
                    await Assertions.Expect(target).ToHaveValueAsync(string.Empty).ConfigureAwait(false);
                    await target.FillAsync("foo").ConfigureAwait(false);
                    Assert.That(await target.InputValueAsync().ConfigureAwait(false), Is.EqualTo("foo"), item.Dom);
                    Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo("foo"), item.Dom);
                    await Assertions.Expect(target).ToHaveValueAsync("foo").ConfigureAwait(false);
                    await page.EvalOnSelectorAsync<object>("#target", "input => { input.value = 'bar'; }").ConfigureAwait(false);
                    Assert.That(await target.InputValueAsync().ConfigureAwait(false), Is.EqualTo("bar"), item.Dom);
                    Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo("bar"), item.Dom);
                    await Assertions.Expect(target).ToHaveValueAsync("bar").ConfigureAwait(false);
                    await target.SelectTextAsync().ConfigureAwait(false);
                    Assert.That(
                        await page.EvaluateAsync<string>("() => window.getSelection().toString()").ConfigureAwait(false),
                        Is.EqualTo("bar"),
                        item.Dom);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "selection retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SelectionRetargeting()
        {
            Case[] cases =
            {
                new Case(DomStandalone("<div contenteditable id=target>content</div>"), locator: "div"),
                new Case(DomInButton("<div contenteditable id=target>content</div>"), locator: "div"),
                new Case(DomInLink("<div contenteditable id=target>content</div>"), locator: "div"),
                new Case(DomInButton("<div contenteditable id=target>content</div>"), locator: "div"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    IElementHandle handle = await page.QuerySelectorAsync(item.Locator).ConfigureAwait(false);
                    Assert.That(await target.IsEditableAsync().ConfigureAwait(false), Is.True, item.Dom);
                    Assert.That(await handle.IsEditableAsync().ConfigureAwait(false), Is.True, item.Dom);
                    await Assertions.Expect(page.Locator("#target")).ToHaveTextAsync("content").ConfigureAwait(false);
                    await target.FillAsync("foo").ConfigureAwait(false);
                    await Assertions.Expect(page.Locator("#target")).ToHaveTextAsync("foo").ConfigureAwait(false);
                    await target.SelectTextAsync().ConfigureAwait(false);
                    Assert.That(
                        await page.EvaluateAsync<string>("() => window.getSelection().toString()").ConfigureAwait(false),
                        Is.EqualTo("foo"),
                        item.Dom);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "select options retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SelectOptionsRetargeting()
        {
            const string select =
                "<select id=target multiple><option value=dog selected>Dog</option><option value=cat>Cat</option></select>";
            Case[] cases =
            {
                new Case(DomInLabel(select), locator: "label"),
                new Case(DomLabelFor(select), locator: "label"),
                new Case(DomStandalone(select), locator: "select"),
                new Case(DomInButton(select), locator: "select"),
                new Case(DomInLink(select), locator: "select"),
                new Case(DomInButton(select), locator: "select"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    IElementHandle handle = await page.QuerySelectorAsync(item.Locator).ConfigureAwait(false);
                    Assert.That(await target.InputValueAsync().ConfigureAwait(false), Is.EqualTo("dog"), item.Dom);
                    Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo("dog"), item.Dom);
                    await Assertions.Expect(target).ToHaveValueAsync("dog").ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveValuesAsync(new[] { "dog" }).ConfigureAwait(false);
                    await target.SelectOptionAsync("cat").ConfigureAwait(false);
                    Assert.That(await target.InputValueAsync().ConfigureAwait(false), Is.EqualTo("cat"), item.Dom);
                    Assert.That(await handle.InputValueAsync().ConfigureAwait(false), Is.EqualTo("cat"), item.Dom);
                    await Assertions.Expect(target).ToHaveValueAsync("cat").ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveValuesAsync(new[] { "cat" }).ConfigureAwait(false);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "direct actions retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task DirectActionsRetargeting()
        {
            Case[] cases =
            {
                new Case(DomInLabel("<div>content</div><input id=target value=oh>"), locator: "div"),
                new Case(DomLabelFor("<div>content</div><input id=target value=oh>"), locator: "div"),
                new Case(DomStandalone("<div>content</div>"), locator: "div"),
                new Case(DomInButton("<div>content</div>"), locator: "div"),
                new Case(DomInLink("<div>content</div>"), locator: "div"),
                new Case(DomInButton("<div>content</div>"), locator: "div"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    Assert.That(await target.InnerTextAsync().ConfigureAwait(false), Is.EqualTo("content"), item.Dom);
                    Assert.That(await target.TextContentAsync().ConfigureAwait(false), Is.EqualTo("content"), item.Dom);
                    await Assertions.Expect(target).ToHaveTextAsync("content").ConfigureAwait(false);
                    await Assertions.Expect(target).ToContainTextAsync("content").ConfigureAwait(false);
                    await Assertions.Expect(target).Not.ToBeFocusedAsync().ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveCountAsync(1).ConfigureAwait(false);
                    await page.EvalOnSelectorAsync<object>("div", "div => { div.foo = 'bar'; }").ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveJSPropertyAsync("foo", "bar").ConfigureAwait(false);
                    await page.EvalOnSelectorAsync<object>("div", "div => { div.classList.add('cls'); }").ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveClassAsync("cls").ConfigureAwait(false);
                    await page.EvalOnSelectorAsync<object>("div", "div => { div.id = 'myid'; }").ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveIdAsync("myid").ConfigureAwait(false);
                    await Assertions.Expect(target).ToHaveAttributeAsync("id", "myid").ConfigureAwait(false);
                    Assert.That(await target.GetAttributeAsync("id").ConfigureAwait(false), Is.EqualTo("myid"), item.Dom);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "check retargeting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckRetargeting()
        {
            Case[] cases =
            {
                new Case(DomInLabel("<input type=checkbox id=target>"), locator: "label"),
                new Case(DomLabelFor("<input type=checkbox id=target>"), locator: "label"),
                new Case(DomStandalone("<input type=checkbox id=target>"), locator: "input"),
                new Case(DomInButton("<input type=checkbox id=target>"), locator: "input"),
                new Case(DomInLink("<input type=checkbox id=target>"), locator: "input"),
                new Case(DomInButton("<input type=checkbox id=target>"), locator: "input"),
            };
            await WithPageAsync(async page =>
            {
                foreach (Case item in cases)
                {
                    await page.SetContentAsync(item.Dom).ConfigureAwait(false);
                    ILocator target = page.Locator(item.Locator);
                    Assert.That(await target.IsCheckedAsync().ConfigureAwait(false), Is.False, item.Dom);
                    await Assertions.Expect(target).Not.ToBeCheckedAsync().ConfigureAwait(false);
                    await Assertions.Expect(target).ToBeCheckedAsync(new() { Checked = false }).ConfigureAwait(false);
                    await page.EvalOnSelectorAsync<object>("input", "input => { input.checked = true; }").ConfigureAwait(false);
                    Assert.That(await target.IsCheckedAsync().ConfigureAwait(false), Is.True, item.Dom);
                    await Assertions.Expect(target).ToBeCheckedAsync().ConfigureAwait(false);
                    await Assertions.Expect(target).ToBeCheckedAsync(new() { Checked = true }).ConfigureAwait(false);
                    await target.UncheckAsync().ConfigureAwait(false);
                    Assert.That(await page.EvalOnSelectorAsync<bool>("input", "input => input.checked").ConfigureAwait(false), Is.False, item.Dom);
                    await target.CheckAsync().ConfigureAwait(false);
                    Assert.That(await page.EvalOnSelectorAsync<bool>("input", "input => input.checked").ConfigureAwait(false), Is.True, item.Dom);
                    await target.SetCheckedAsync(false).ConfigureAwait(false);
                    Assert.That(await page.EvalOnSelectorAsync<bool>("input", "input => input.checked").ConfigureAwait(false), Is.False, item.Dom);
                }
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("retarget.spec.ts", "should not retarget anchor into parent label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRetargetAnchorIntoParentLabel()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(
                    "<label disabled>Text<a href='#' onclick='window.__clicked=1'>Target</a></label>").ConfigureAwait(false);
                await page.Locator("a").ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("() => window.__clicked").ConfigureAwait(false), Is.EqualTo(1));
                await page.SetContentAsync(
                    "<input type=\"radio\" id=\"input-id\" checked disabled />" +
                    "<label for=\"input-id\">Text<a href='#' onclick='window.__clicked=2'>Target</a></label>").ConfigureAwait(false);
                await page.Locator("a").ClickAsync().ConfigureAwait(false);
                Assert.That(await page.EvaluateAsync<int>("() => window.__clicked").ConfigureAwait(false), Is.EqualTo(2));
            }).ConfigureAwait(false);
        }

        private static string OptionsToAttributes(bool disabled = false, bool hidden = false, bool readOnly = false)
            => " " + (disabled ? "disabled" : string.Empty) + " " + (hidden ? "hidden" : string.Empty) + " " + (readOnly ? "readonly" : string.Empty) + " ";

        private static string DomInLabel(string dom, bool disabled = false, bool hidden = false, bool readOnly = false)
            => "<label" + OptionsToAttributes(disabled, hidden, readOnly) + ">Text " + dom + "</label>";

        private static string DomLabelFor(string dom, bool disabled = false, bool hidden = false, bool readOnly = false)
            => "<label" + OptionsToAttributes(disabled, hidden, readOnly) + " for=\"target\"><h1>Text</h1></label>" + dom;

        private static string DomStandalone(string dom) => dom;

        private static string DomInButton(string dom, bool disabled = false, bool hidden = false, bool readOnly = false)
            => "<button" + OptionsToAttributes(disabled, hidden, readOnly) + ">Button " + dom + "</button>";

        private static string DomInLink(string dom, bool disabled = false, bool hidden = false, bool readOnly = false)
            => "<button" + OptionsToAttributes(disabled, hidden, readOnly) + ">Button " + dom + "</button>";

        private static async Task GiveItAChanceToResolveAsync(IPage page)
        {
            for (int i = 0; i < 5; i++)
            {
                await page.EvaluateAsync<object>(
                    "(() => new Promise(r => requestAnimationFrame(() => requestAnimationFrame(r))))()").ConfigureAwait(false);
            }
        }

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }

        private sealed class Case
        {
            internal Case(
                string dom,
                string locator,
                bool enabled = false,
                bool visible = false,
                bool editable = false)
            {
                Dom = dom;
                Locator = locator;
                Enabled = enabled;
                Visible = visible;
                Editable = editable;
            }

            internal string Dom { get; }

            internal string Locator { get; }

            internal bool Enabled { get; }

            internal bool Visible { get; }

            internal bool Editable { get; }
        }
    }
}
