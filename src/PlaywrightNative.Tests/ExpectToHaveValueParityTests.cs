/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect-to-have-value.spec.ts</c> parity for toHaveValue
    /// and toHaveValues.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectToHaveValueParityTests : PageTestEx
    {
        private static string MessageOf(Exception error)
        {
            string message = error == null ? string.Empty : error.Message ?? string.Empty;
            return message.Replace("\r\n", "\n", StringComparison.Ordinal);
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

        [PlaywrightTest("expect-to-have-value.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await Page.SetContentAsync("<input id=node></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await locator.FillAsync("Text content").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveValueAsync("Text content").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "should work with label")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithLabel()
        {
            await Page.SetContentAsync("<label><input></input></label>").ConfigureAwait(false);
            await Page.Locator("label input").FillAsync("Text content").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("label")).ToHaveValueAsync("Text content").ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "should work with regex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithRegex()
        {
            await Page.SetContentAsync("<input id=node></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await locator.FillAsync("Text content").ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveValueAsync(new Regex("Text")).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "should support failure")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportFailure()
        {
            await Page.SetContentAsync("<input id=node></input>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");
            await locator.FillAsync("Text content").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveValueAsync(new Regex("Text2"), new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain("\"Text content\""));
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "works with text")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesWorksWithText()
        {
            await Page.SetContentAsync(@"
      <select multiple>
        <option value=""R"">Red</option>
        <option value=""G"">Green</option>
        <option value=""B"">Blue</option>
      </select>
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("select");
            await locator.SelectOptionAsync(new[] { "R", "G" }).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveValuesAsync(new[] { "R", "G" }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "follows labels")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesFollowsLabels()
        {
            await Page.SetContentAsync(@"
      <label for=""colors"">Pick a Color</label>
      <select id=""colors"" multiple>
        <option value=""R"">Red</option>
        <option value=""G"">Green</option>
        <option value=""B"">Blue</option>
      </select>
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("text=Pick a Color");
            await locator.SelectOptionAsync(new[] { "R", "G" }).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveValuesAsync(new[] { "R", "G" }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "exact match with text failure")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesExactMatchWithTextFailure()
        {
            await Page.SetContentAsync(@"
      <select multiple>
        <option value=""RR"">Red</option>
        <option value=""GG"">Green</option>
      </select>
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("select");
            await locator.SelectOptionAsync(new[] { "RR", "GG" }).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveValuesAsync(new[] { "R", "G" }, new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain("-   \"R\""));
            Assert.That(MessageOf(error), Does.Contain("+   \"RR\""));
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "works with regex")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesWorksWithRegex()
        {
            await Page.SetContentAsync(@"
      <select multiple>
        <option value=""R"">Red</option>
        <option value=""G"">Green</option>
        <option value=""B"">Blue</option>
      </select>
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("select");
            await locator.SelectOptionAsync(new[] { "R", "G" }).ConfigureAwait(false);
            await Assertions.Expect(locator).ToHaveValuesAsync(new[] { new Regex("R"), new Regex("G") }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "fails when items not selected")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesFailsWhenItemsNotSelected()
        {
            await Page.SetContentAsync(@"
      <select multiple>
        <option value=""R"">Red</option>
        <option value=""G"">Green</option>
        <option value=""B"">Blue</option>
      </select>
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("select");
            await locator.SelectOptionAsync(new[] { "B" }).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveValuesAsync(new[] { new Regex("R"), new Regex("G") }, new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain("+   \"B\""));
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "fails when multiple not specified")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesFailsWhenMultipleNotSpecified()
        {
            await Page.SetContentAsync(@"
      <select>
        <option value=""R"">Red</option>
        <option value=""G"">Green</option>
        <option value=""B"">Blue</option>
      </select>
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("select");
            await locator.SelectOptionAsync(new[] { "B" }).ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveValuesAsync(new[] { new Regex("R"), new Regex("G") }, new() { Timeout = 1000 }));
            Assert.That(error.Message, Does.Contain("Not a select element with a multiple attribute"));
        }

        [PlaywrightTest("expect-to-have-value.spec.ts", "fails when not a select element")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToHaveValuesFailsWhenNotASelectElement()
        {
            await Page.SetContentAsync(@"
      <input value=""foo"" />
    ").ConfigureAwait(false);
            ILocator locator = Page.Locator("input");
            Exception error = Assert.CatchAsync(() => Assertions.Expect(locator).ToHaveValuesAsync(new[] { new Regex("R"), new Regex("G") }, new() { Timeout = 1000 }));
            Assert.That(error.Message, Does.Contain("Not a select element with a multiple attribute"));
        }
    }
}
