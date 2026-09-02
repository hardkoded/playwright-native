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
    /// Official <c>expect-with-snapshot.spec.ts</c> parity for
    /// <c>matcherResult.ariaSnapshot</c> on expect failures.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectWithSnapshotParityTests : PageTestEx
    {
        private static ExpectException CatchExpect(AsyncTestDelegate code)
        {
            Exception error = Assert.CatchAsync(code);
            Assert.That(error, Is.InstanceOf<ExpectException>());
            return (ExpectException)error;
        }

        private IBrowser _browser;
        private IBrowserContext _context;
        private IPage _page;

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
        }

        private IPage Page => _page;

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveText failure includes full element subtree")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextFailureIncludesFullElementSubtree()
        {
            await Page.SetContentAsync("<section id=node><h1>Title</h1><p>Body</p></section>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#node")).ToHaveTextAsync("nope", new() { Timeout = 1000 }));
            string snapshot = error.MatcherResult.AriaSnapshot;
            Assert.That(snapshot, Does.Contain("heading \"Title\""));
            Assert.That(snapshot, Does.Contain("paragraph"));
            Assert.That(snapshot, Does.Contain("Body"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toContainText failure includes full element subtree")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToContainTextFailureIncludesFullElementSubtree()
        {
            await Page.SetContentAsync("<section id=node><h1>Title</h1><p>Body</p></section>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#node")).ToContainTextAsync("nope", new() { Timeout = 1000 }));
            string snapshot = error.MatcherResult.AriaSnapshot;
            Assert.That(snapshot, Does.Contain("heading \"Title\""));
            Assert.That(snapshot, Does.Contain("Body"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toBeChecked failure prints just the input")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedFailurePrintsJustTheInput()
        {
            await Page.SetContentAsync("<label><input id=cb type=checkbox> a checkbox</label>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#cb")).ToBeCheckedAsync(new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("checkbox"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveAttribute failure clips descendant subtree")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveAttributeFailureClipsDescendantSubtree()
        {
            await Page.SetContentAsync("<ul id=lst><li><h2>HeadingMarker</h2><p>BodyMarker</p></li></ul>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#lst")).ToHaveAttributeAsync("data-x", "yes", new() { Timeout = 1000 }));
            string snapshot = error.MatcherResult.AriaSnapshot;
            Assert.That(snapshot, Does.Contain("list"));
            Assert.That(snapshot, Does.Contain("listitem"));
            Assert.That(snapshot, Does.Not.Contain("HeadingMarker"));
            Assert.That(snapshot, Does.Not.Contain("BodyMarker"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveRole failure prints just the element line")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveRoleFailurePrintsJustTheElementLine()
        {
            await Page.SetContentAsync("<button id=btn>Hi<span>nested</span></button>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#btn")).ToHaveRoleAsync(AriaRole.Link, new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("button"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveValue failure prints the input element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValueFailurePrintsTheInputElement()
        {
            await Page.SetContentAsync("<input id=inp value=\"actual\">").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#inp")).ToHaveValueAsync("expected", new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("textbox"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveCSS failure prints the element line")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCSSFailurePrintsTheElementLine()
        {
            await Page.SetContentAsync("<button id=btn style=\"color: red\">Press</button>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#btn")).ToHaveCSSAsync("color", "rgb(0, 0, 0)", new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("button"));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toBeVisible on hidden element prints full page snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleOnHiddenElementPrintsFullPageSnapshot()
        {
            await Page.SetContentAsync(@"
      <div id=hidden style=""display: none""><span>secret</span></div>
      <main><h1>Page Heading</h1></main>
    ").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#hidden")).ToBeVisibleAsync(new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("heading \"Page Heading\""));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toBeVisible on missing element prints full page snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeVisibleOnMissingElementPrintsFullPageSnapshot()
        {
            await Page.SetContentAsync("<header><h1>Hello</h1></header>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#nope")).ToBeVisibleAsync(new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("heading \"Hello\""));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveText on missing element prints full page snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextOnMissingElementPrintsFullPageSnapshot()
        {
            await Page.SetContentAsync("<main><h1>Hello</h1></main>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#missing")).ToHaveTextAsync("x", new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("heading \"Hello\""));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveTitle failure prints full page snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTitleFailurePrintsFullPageSnapshot()
        {
            await Page.SetContentAsync("<title>Right</title><main><h1>Body Heading</h1></main>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page).ToHaveTitleAsync("Wrong", new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Does.Contain("heading \"Body Heading\""));
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveCount failure has no aria snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveCountFailureHasNoAriaSnapshot()
        {
            await Page.SetContentAsync("<ul><li>a</li><li>b</li></ul>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("li")).ToHaveCountAsync(5, new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Is.Null);
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toHaveText with array has no aria snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveTextWithArrayHasNoAriaSnapshot()
        {
            await Page.SetContentAsync("<ul><li>x</li><li>y</li></ul>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("li")).ToHaveTextAsync(new[] { "a", "b", "c" }, new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Is.Null);
        }

        [PlaywrightTest("expect-with-snapshot.spec.ts", "toMatchAriaSnapshot failure has no extra aria snapshot")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToMatchAriaSnapshotFailureHasNoExtraAriaSnapshot()
        {
            await Page.SetContentAsync("<button id=btn>Y</button>").ConfigureAwait(false);
            ExpectException error = CatchExpect(
                () => Assertions.Expect(Page.Locator("#btn")).ToMatchAriaSnapshotAsync("- button \"X\"", new() { Timeout = 1000 }));
            Assert.That(error.MatcherResult.AriaSnapshot, Is.Null);
        }
    }
}
