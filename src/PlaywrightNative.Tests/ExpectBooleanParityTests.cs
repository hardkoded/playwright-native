/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect-boolean.spec.ts</c> parity for toBeChecked,
    /// toBeEditable, toBeEnabled, toBeEmpty, toBeVisible, toBeHidden,
    /// toBeFocused, toBeAttached, and toBeOK. Android / Electron
    /// <c>test.skip</c> is not applied.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectBooleanParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
        private static string Prefix = TestConstants.ServerUrl.TrimEnd('/');
        private static string EmptyPage = TestConstants.EmptyPage;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        private static string MessageOf(Exception error)
        {
            string message = error == null ? string.Empty : error.Message ?? string.Empty;
            return message.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private static string Lines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            if (TestServerSetup.Server != null)
            {
                Prefix = TestConstants.ServerUrl.TrimEnd('/');
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19805;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    string origin = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    Prefix = origin;
                    EmptyPage = origin + "/empty.html";
                    return;
                }
                catch (Exception)
                {
                }
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

        [SetUp]
        public async Task SetUpAsync()
        {
            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            _context = await _browser.NewContextAsync().ConfigureAwait(false);
            _page = await _context.NewPageAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            try
            {
                if (_context != null)
                {
                    await _context.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                if (_browser != null)
                {
                    await _browser.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (ObjectDisposedException)
            {
            }

            Server?.Reset();
        }

        private IPage Page => _page;

        [PlaywrightTest("expect-boolean.spec.ts", "default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedDefault()
        {
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeCheckedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with checked:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithCheckedTrue()
        {
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeCheckedAsync(new() { Checked = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with checked:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithCheckedFalse()
        {
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeCheckedAsync(new() { Checked = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with indeterminate:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithIndeterminateTrue()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            await Page.Locator("input").EvaluateAsync<object>("e => e.indeterminate = true").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeCheckedAsync(new() { Indeterminate = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with indeterminate:true and checked")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithIndeterminateTrueAndChecked()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            await Page.Locator("input").EvaluateAsync<object>("e => e.indeterminate = true").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeCheckedAsync(new() { Indeterminate = true, Checked = false }));
            Assert.That(MessageOf(error), Does.Contain("Can't assert indeterminate and checked at the same time"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFail()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeCheckedAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toBeChecked() failed

Locator:  locator('input')
Expected: checked
Received: unchecked
Timeout:  1000ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toBeChecked\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithNot()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeCheckedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not and checked:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithNotAndCheckedFalse()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeCheckedAsync(new() { Checked = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFailWithNot()
        {
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToBeCheckedAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).not.toBeChecked() failed

Locator:  locator('input')
Expected: not checked
Received: checked
Timeout:  1000ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"not toBeChecked\" with timeout 1000ms"));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"locator resolved to <input checked type=""checkbox""/>")));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with checked:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFailWithCheckedFalse()
        {
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeCheckedAsync(new() { Checked = false, Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toBeChecked({ checked: false }) failed

Locator:  locator('input')
Expected: unchecked
Received: checked
Timeout:  1000ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toBeChecked\" with timeout 1000ms"));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"locator resolved to <input checked type=""checkbox""/>")));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with indeterminate: true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFailWithIndeterminateTrue()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeCheckedAsync(new() { Indeterminate = true, Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toBeChecked({ indeterminate: true }) failed

Locator:  locator('input')
Expected: indeterminate
Received: unchecked
Timeout:  1000ms")));
            Assert.That(MessageOf(error), Does.Contain("- Expect \"toBeChecked\" with timeout 1000ms"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail missing")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFailMissing()
        {
            await Page.SetContentAsync("<div>no inputs here</div>").ConfigureAwait(false);
            ILocator locator2 = Page.Locator("input2");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator2).Not.ToBeCheckedAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).not.toBeChecked() failed

Locator: locator('input2')
Expected: not checked
Timeout: 1000ms
Error: element(s) not found

Call log:
  - Expect ""not toBeChecked"" with timeout 1000ms
  - waiting for locator('input2')
")));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with role")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithRole()
        {
            string[] roles = new string[] { "checkbox", "menuitemcheckbox", "option", "radio", "switch", "menuitemradio", "treeitem" };
            for (int i = 0; i < roles.Length; i++)
            {
                string role = roles[i];
                await Page.SetContentAsync("<div role=" + role + " aria-checked=true>I am checked</div>").ConfigureAwait(false);
                ILocator locator = Page.Locator("div");
                await Assertions.Expect(locator).ToBeCheckedAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("expect-boolean.spec.ts", "friendly log")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFriendlyLog()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            Exception error1 = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("input")).ToBeCheckedAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error1), Does.Contain("unexpected value \"unchecked\""));
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            Exception error2 = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("input")).ToBeCheckedAsync(new() { Checked = false, Timeout = 1000 }));
            Assert.That(MessageOf(error2), Does.Contain("unexpected value \"checked\""));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<input type=checkbox checked></input>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("input")).ToBeCheckedAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout .not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithImpossibleTimeoutNot()
        {
            await Page.SetContentAsync("<input type=checkbox></input>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("input")).Not.ToBeCheckedAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEditableDefault()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeEditableAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEditableWithNot()
        {
            await Page.SetContentAsync("<input readonly></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeEditableAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with editable:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEditableWithEditableTrue()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeEditableAsync(new() { Editable = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with editable:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEditableWithEditableFalse()
        {
            await Page.SetContentAsync("<input readonly></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeEditableAsync(new() { Editable = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not and editable:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEditableWithNotAndEditableFalse()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeEditableAsync(new() { Editable = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "throws")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEditableThrows()
        {
            await Page.SetContentAsync("<button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeEditableAsync());
            Assert.That(MessageOf(error), Does.Contain("Element is not an <input>, <textarea>, <select> or [contenteditable] and does not have a role allowing [aria-readonly]"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledDefault()
        {
            await Page.SetContentAsync("<button>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeEnabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with enabled:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledWithEnabledTrue()
        {
            await Page.SetContentAsync("<button>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeEnabledAsync(new() { Enabled = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with enabled:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledWithEnabledFalse()
        {
            await Page.SetContentAsync("<button disabled>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeEnabledAsync(new() { Enabled = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "failed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledFailed()
        {
            await Page.SetContentAsync("<button disabled>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeEnabledAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to <button disabled>Text</button>"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledEventually()
        {
            await Page.SetContentAsync("<button disabled>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await locator.EvaluateAsync<object>("e => e.removeAttribute('disabled')").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).ToBeEnabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledEventuallyWithNot()
        {
            await Page.SetContentAsync("<button>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            _ = Task.Run(async () =>
            {
                await Task.Delay(500).ConfigureAwait(false);
                try
                {
                    await locator.EvaluateAsync<object>("e => e.setAttribute('disabled', '')").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).Not.ToBeEnabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not and enabled:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledWithNotAndEnabledFalse()
        {
            await Page.SetContentAsync("<button>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).Not.ToBeEnabledAsync(new() { Enabled = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeDisabled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEnabledToBeDisabled()
        {
            await Page.SetContentAsync("<button disabled>Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeDisabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeEmpty input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEmptyInput()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeEmptyAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "not.toBeEmpty")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NotToBeEmpty()
        {
            await Page.SetContentAsync("<input value=text></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeEmptyAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeEmpty div")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeEmptyDiv()
        {
            await Page.SetContentAsync("<div style=\"width: 50; height: 50px\"></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).ToBeEmptyAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeDisabled with value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeDisabledWithValue()
        {
            await Page.SetContentAsync("<button disabled=\"yes\">Text</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeDisabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeChecked with value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedWithValue()
        {
            await Page.SetContentAsync("<input type=checkbox checked=\"yes\"></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeCheckedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeHidden with value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenWithValue()
        {
            await Page.SetContentAsync("<input type=checkbox hidden=\"of course\"></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "not.toBeDisabled div")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NotToBeDisabledDiv()
        {
            await Page.SetContentAsync("<div disabled=\"yes\"></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("div");
            await Assertions.Expect(locator).Not.ToBeDisabledAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleDefault()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithNot()
        {
            await Page.SetContentAsync("<button style=\"display: none\">hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).Not.ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with visible:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithVisibleTrue()
        {
            await Page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeVisibleAsync(new() { Visible = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with visible:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithVisibleFalse()
        {
            await Page.SetContentAsync("<button hidden>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeVisibleAsync(new() { Visible = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not and visible:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithNotAndVisibleFalse()
        {
            await Page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).Not.ToBeVisibleAsync(new() { Visible = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleEventually()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Page.EvalOnSelectorAsync<object>("div", "div => div.innerHTML = '<span>Hello</span>'").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleEventuallyWithNot()
        {
            await Page.SetContentAsync("<div><span>Hello</span></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Page.EvalOnSelectorAsync<object>("span", "span => span.textContent = ''").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).Not.ToBeVisibleAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleFail()
        {
            await Page.SetContentAsync("<button style=\"display: none\"></button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeVisibleAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to <button></button>"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleFailWithNot()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToBeVisibleAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to <input/>"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToBeVisibleAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout .not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithImpossibleTimeoutNot()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("no-such-thing")).Not.ToBeVisibleAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with frameLocator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithFrameLocator()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.FrameLocator("iframe").Locator("input");
            bool done = false;
            Task promise = Assertions.Expect(locator).ToBeVisibleAsync().ContinueWith(t => { done = true; return t; }, TaskScheduler.Default).Unwrap();
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await Page.SetContentAsync("<iframe srcdoc=\"<input>\"></iframe>").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with frameLocator 2")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleWithFrameLocator2()
        {
            await Page.SetContentAsync("<iframe></iframe>").ConfigureAwait(false);
            ILocator locator = Page.FrameLocator("iframe").Locator("input");
            bool done = false;
            Task promise = Assertions.Expect(locator).ToBeVisibleAsync().ContinueWith(t => { done = true; return t; }, TaskScheduler.Default).Unwrap();
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await Page.SetContentAsync("<iframe srcdoc=\"<input>\"></iframe>").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "over navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleOverNavigation()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            bool done = false;
            Task promise = Assertions.Expect(Page.Locator("input")).ToBeVisibleAsync().ContinueWith(t => { done = true; return t; }, TaskScheduler.Default).Unwrap();
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await Page.GoToAsync(Prefix + "/input/checkbox.html").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenDefault()
        {
            await Page.SetContentAsync("<button style=\"display: none\"></button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "when nothing matches")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenWhenNothingMatches()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenWithNot()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenEventuallyWithNot()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Page.EvalOnSelectorAsync<object>("div", "div => div.innerHTML = '<span>Hello</span>'").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).Not.ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenEventually()
        {
            await Page.SetContentAsync("<div><span>Hello</span></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Page.EvalOnSelectorAsync<object>("span", "span => span.textContent = ''").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).ToBeHiddenAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenFail()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeHiddenAsync(new() { Timeout = 3000 }));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to <input/>"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenFailWithNot()
        {
            await Page.SetContentAsync("<button style=\"display: none\"></button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToBeHiddenAsync(new() { Timeout = 3000 }));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to <button></button>"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with not when nothing matching")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenFailWithNotWhenNothingMatching()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToBeHiddenAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).not.toBeHidden() failed

Locator: locator('button')
Expected: not hidden
Timeout: 1000ms
Error: element(s) not found

Call log:
  - Expect ""not toBeHidden"" with timeout 1000ms
")));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout .not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenWithImpossibleTimeoutNot()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).Not.ToBeHiddenAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeHiddenWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("no-such-thing")).ToBeHiddenAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeFocused")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeFocused()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await locator.FocusAsync().ConfigureAwait(false);
            await Assertions.Expect(locator).ToBeFocusedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeFocused with shadow elements")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeFocusedWithShadowElements()
        {
            await Page.SetContentAsync(@"
    <div id=""app"">
    </div>
    <script>
      const root = document.querySelector('div');
      const shadowRoot = root.attachShadow({ mode: 'open' });
      const input = document.createElement('input');
      input.id = ""my-input""
      shadowRoot.appendChild(input);
    </script>
  ").ConfigureAwait(false);
            await Page.Locator("input").FocusAsync().ConfigureAwait(false);
            string id = await Page.EvaluateAsync<string>("() => document.activeElement.shadowRoot.activeElement.id").ConfigureAwait(false);
            Assert.That(id, Is.EqualTo("my-input"));
            await Assertions.Expect(Page.Locator("#app")).ToBeFocusedAsync().ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("input")).ToBeFocusedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "should print unknown engine error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintUnknownEngineError()
        {
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("row=\"row\"")).ToBeVisibleAsync());
            Assert.That(MessageOf(error), Does.Contain(Lines(@"Unknown engine ""row"" while parsing selector row=""row""")));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "should print selector syntax error")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintSelectorSyntaxError()
        {
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("row]")).ToBeVisibleAsync());
            Assert.That(MessageOf(error), Does.Contain(Lines(@"Unexpected token ""]"" while parsing css selector ""row]""")));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeOK")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeOK()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            IAPIResponse res = await Page.APIRequest.GetAsync(EmptyPage).ConfigureAwait(false);
            await Assertions.Expect(res).ToBeOKAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "not.toBeOK")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task NotToBeOK()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            IAPIResponse res = await Page.APIRequest.GetAsync(Prefix + "/unknown").ConfigureAwait(false);
            await Assertions.Expect(res).Not.ToBeOKAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "text content type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeOKTextContentType()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.SetRoute("/text-content-type", http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "text/plain";
                return http.Response.WriteAsync("Text error");
            });
            IAPIResponse res = await Page.APIRequest.GetAsync(Prefix + "/text-content-type").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(res).ToBeOKAsync());
            Assert.That(MessageOf(error), Does.Contain("expect(response).toBeOK() failed"));
            Assert.That(MessageOf(error), Does.Contain("→ GET " + Prefix + "/text-content-type"));
            Assert.That(MessageOf(error), Does.Contain("← 404 Not Found"));
            Assert.That(MessageOf(error), Does.Contain("Response text:\nText error"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "no content type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeOKNoContentType()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.SetRoute("/no-content-type", http =>
            {
                http.Response.StatusCode = 404;
                return http.Response.WriteAsync("No content type error");
            });
            IAPIResponse res = await Page.APIRequest.GetAsync(Prefix + "/no-content-type").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(res).ToBeOKAsync());
            Assert.That(MessageOf(error), Does.Contain("→ GET " + Prefix + "/no-content-type"));
            Assert.That(MessageOf(error), Does.Contain("← 404 Not Found"));
            Assert.That(MessageOf(error), Does.Not.Contain("No content type error"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "image content type")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeOKImageContentType()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Server.SetRoute("/image-content-type", http =>
            {
                http.Response.StatusCode = 404;
                http.Response.ContentType = "image/bmp";
                return http.Response.WriteAsync("Image content type error");
            });
            IAPIResponse res = await Page.APIRequest.GetAsync(Prefix + "/image-content-type").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(res).ToBeOKAsync());
            Assert.That(MessageOf(error), Does.Contain("→ GET " + Prefix + "/image-content-type"));
            Assert.That(MessageOf(error), Does.Contain("← 404 Not Found"));
            Assert.That(MessageOf(error), Does.Not.Contain("Image content type error"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "default")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedDefault()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeAttachedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with hidden element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithHiddenElement()
        {
            await Page.SetContentAsync("<button style=\"display:none\">hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeAttachedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithNot()
        {
            await Page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).Not.ToBeAttachedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with attached:true")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithAttachedTrue()
        {
            await Page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).ToBeAttachedAsync(new() { Attached = true }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with attached:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithAttachedFalse()
        {
            await Page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            await Assertions.Expect(locator).ToBeAttachedAsync(new() { Attached = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with not and attached:false")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithNotAndAttachedFalse()
        {
            await Page.SetContentAsync("<button>hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("button");
            await Assertions.Expect(locator).Not.ToBeAttachedAsync(new() { Attached = false }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedEventually()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Page.EvalOnSelectorAsync<object>("div", "div => div.innerHTML = '<span>Hello</span>'").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).ToBeAttachedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "eventually with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedEventuallyWithNot()
        {
            await Page.SetContentAsync("<div><span>Hello</span></div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("span");
            _ = Task.Run(async () =>
            {
                try
                {
                    await Page.EvalOnSelectorAsync<object>("div", "div => div.textContent = ''").ConfigureAwait(false);
                }
                catch (Exception)
                {
                }
            });
            await Assertions.Expect(locator).Not.ToBeAttachedAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedFail()
        {
            await Page.SetContentAsync("<button>Hello</button>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToBeAttachedAsync(new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Not.Contain("locator resolved to"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "fail with not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedFailWithNot()
        {
            await Page.SetContentAsync("<input></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).Not.ToBeAttachedAsync(new() { Timeout = 3000 }));
            Assert.That(MessageOf(error), Does.Contain("locator resolved to <input/>"));
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#node")).ToBeAttachedAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with impossible timeout .not")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithImpossibleTimeoutNot()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("no-such-thing")).Not.ToBeAttachedAsync(new() { Timeout = 1 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "with frameLocator")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedWithFrameLocator()
        {
            await Page.SetContentAsync("<div></div>").ConfigureAwait(false);
            ILocator locator = Page.FrameLocator("iframe").Locator("input");
            bool done = false;
            Task promise = Assertions.Expect(locator).ToBeAttachedAsync().ContinueWith(t => { done = true; return t; }, TaskScheduler.Default).Unwrap();
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await Page.SetContentAsync("<iframe srcdoc=\"<input>\"></iframe>").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "over navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeAttachedOverNavigation()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            bool done = false;
            Task promise = Assertions.Expect(Page.Locator("input")).ToBeAttachedAsync().ContinueWith(t => { done = true; return t; }, TaskScheduler.Default).Unwrap();
            await Page.WaitForTimeoutAsync(1000).ConfigureAwait(false);
            Assert.That(done, Is.False);
            await Page.GoToAsync(Prefix + "/input/checkbox.html").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
            Assert.That(done, Is.True);
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeOK fail with invalid argument")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ToBeOKFailWithInvalidArgument()
        {
            IAPIResponse response = null;
            Exception error = Assert.Catch(() => Assertions.Expect(response));
            Assert.That(error, Is.InstanceOf<ArgumentNullException>());
        }

        [PlaywrightTest("expect-boolean.spec.ts", "toBeOK fail with promise")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeOKFailWithPromise()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            Task<IAPIResponse> res = Page.APIRequest.GetAsync(EmptyPage);
            Exception error = Assert.CatchAsync(() => Assertions.Expect((object)res).ToBeOKAsync());
            Assert.That(error.Message, Does.Contain("toBeOK can be only used with APIResponse object"));
            await res.ConfigureAwait(false);
        }
    }
}
