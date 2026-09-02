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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect-matcher-result.spec.ts</c> parity. Skipped (playbook
    /// screenshot pixel-diff): toHaveScreenshot should populate matcherResult.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectMatcherResultParityTests : PageTestEx
    {
        private static string MessageOf(Exception error)
        {
            string message = error == null ? string.Empty : error.ToString() ?? string.Empty;
            return message.Replace("\r\n", "\n", StringComparison.Ordinal);
        }

        private static string Lines(string value)
        {
            return (value ?? string.Empty).Replace("\r\n", "\n", StringComparison.Ordinal);
        }

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

        [PlaywrightTest("expect-matcher-result.spec.ts", "toMatchText-based assertions should have matcher result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToMatchTextBasedAssertionsShouldHaveMatcherResult()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            ILocator locator = Page.Locator("#node");

            {
                Regex expected = new Regex("Text2");
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(locator).ToHaveTextAsync(expected, new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("Text content"));
                Assert.That(result.Expected, Is.SameAs(expected));
                Assert.That(result.Message, Does.Contain("expect(locator).toHaveText(expected) failed"));
                Assert.That(result.Name, Is.EqualTo("toHaveText"));
                Assert.That(result.Pass, Is.False);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).toHaveText(expected) failed

Locator: locator('#node')
Expected pattern: /Text2/
Received string:  ""Text content""
Timeout: 1ms

Call log")));
            }

            {
                Regex expected = new Regex("Text");
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(locator).Not.ToHaveTextAsync(expected, new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("Text content"));
                Assert.That(result.Expected, Is.SameAs(expected));
                Assert.That(result.Message, Does.Contain("expect(locator).not.toHaveText(expected) failed"));
                Assert.That(result.Name, Is.EqualTo("toHaveText"));
                Assert.That(result.Pass, Is.True);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).not.toHaveText(expected) failed

Locator: locator('#node')
Expected pattern: not /Text/
Received string: ""Text content""
Timeout: 1ms

Call log")));
            }
        }

        [PlaywrightTest("expect-matcher-result.spec.ts", "toBeTruthy-based assertions should have matcher result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeTruthyBasedAssertionsShouldHaveMatcherResult()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#node2")).ToBeVisibleAsync(new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Expected, Is.EqualTo("visible"));
                Assert.That(result.Message, Does.Contain("expect(locator).toBeVisible() failed"));
                Assert.That(result.Name, Is.EqualTo("toBeVisible"));
                Assert.That(result.Pass, Is.False);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).toBeVisible() failed

Locator: locator('#node2')
Expected: visible
Timeout: 1ms
Error: element(s) not found

Call log:
")));
            }

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#node")).Not.ToBeVisibleAsync(new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("visible"));
                Assert.That(result.Expected, Is.EqualTo("visible"));
                Assert.That(result.Message, Does.Contain("expect(locator).not.toBeVisible() failed"));
                Assert.That(result.Name, Is.EqualTo("toBeVisible"));
                Assert.That(result.Pass, Is.True);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).not.toBeVisible() failed

Locator:  locator('#node')
Expected: not visible
Received: visible
Timeout:  1ms

Call log:
")));
            }
        }

        [PlaywrightTest("expect-matcher-result.spec.ts", "toEqual-based assertions should have matcher result")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToEqualBasedAssertionsShouldHaveMatcherResult()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#node2")).ToHaveCountAsync(1, new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo(0));
                Assert.That(result.Expected, Is.EqualTo(1));
                Assert.That(result.Message, Does.Contain("expect(locator).toHaveCount(expected) failed"));
                Assert.That(result.Name, Is.EqualTo("toHaveCount"));
                Assert.That(result.Pass, Is.False);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).toHaveCount(expected) failed

Locator:  locator('#node2')
Expected: 1
Received: 0
Timeout:  1ms

Call log")));
            }

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#node")).Not.ToHaveCountAsync(1, new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo(1));
                Assert.That(result.Expected, Is.EqualTo(1));
                Assert.That(result.Message, Does.Contain("expect(locator).not.toHaveCount(expected) failed"));
                Assert.That(result.Name, Is.EqualTo("toHaveCount"));
                Assert.That(result.Pass, Is.True);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).not.toHaveCount(expected) failed

Locator:  locator('#node')
Expected: not 1
Received: 1
Timeout:  1ms

Call log")));
            }
        }

        [PlaywrightTest("expect-matcher-result.spec.ts", "toBeChecked({ checked }) should have expected")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ToBeCheckedCheckedShouldHaveExpected()
        {
            await Page.SetContentAsync(@"
    <input id=checked type=checkbox checked></input>
    <input id=unchecked type=checkbox></input>
  ").ConfigureAwait(false);

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#unchecked")).ToBeCheckedAsync(new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("unchecked"));
                Assert.That(result.Expected, Is.EqualTo("checked"));
                Assert.That(result.Message, Does.Contain("expect(locator).toBeChecked() failed"));
                Assert.That(result.Name, Is.EqualTo("toBeChecked"));
                Assert.That(result.Pass, Is.False);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).toBeChecked() failed

Locator:  locator('#unchecked')
Expected: checked
Received: unchecked
Timeout:  1ms

Call log")));
            }

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#checked")).Not.ToBeCheckedAsync(new() { Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("checked"));
                Assert.That(result.Expected, Is.EqualTo("checked"));
                Assert.That(result.Message, Does.Contain("expect(locator).not.toBeChecked() failed"));
                Assert.That(result.Name, Is.EqualTo("toBeChecked"));
                Assert.That(result.Pass, Is.True);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).not.toBeChecked() failed

Locator:  locator('#checked')
Expected: not checked
Received: checked
Timeout:  1ms

Call log")));
            }

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#checked")).ToBeCheckedAsync(new() { Checked = false, Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("checked"));
                Assert.That(result.Expected, Is.EqualTo("unchecked"));
                Assert.That(result.Message, Does.Contain("expect(locator).toBeChecked({ checked: false }) failed"));
                Assert.That(result.Name, Is.EqualTo("toBeChecked"));
                Assert.That(result.Pass, Is.False);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).toBeChecked({ checked: false }) failed

Locator:  locator('#checked')
Expected: unchecked
Received: checked
Timeout:  1ms

Call log")));
            }

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#unchecked")).Not.ToBeCheckedAsync(new() { Checked = false, Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("unchecked"));
                Assert.That(result.Expected, Is.EqualTo("unchecked"));
                Assert.That(result.Message, Does.Contain("expect(locator).not.toBeChecked({ checked: false }) failed"));
                Assert.That(result.Name, Is.EqualTo("toBeChecked"));
                Assert.That(result.Pass, Is.True);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).not.toBeChecked({ checked: false }) failed

Locator:  locator('#unchecked')
Expected: not unchecked
Received: unchecked
Timeout:  1ms

Call log")));
            }

            {
                ExpectException error = CatchExpect(
                    () => Assertions.Expect(Page.Locator("#unchecked")).ToBeCheckedAsync(new() { Indeterminate = true, Timeout = 1 }));
                ExpectMatcherResult result = error.MatcherResult;
                Assert.That(result.Actual, Is.EqualTo("unchecked"));
                Assert.That(result.Expected, Is.EqualTo("indeterminate"));
                Assert.That(result.Message, Does.Contain("expect(locator).toBeChecked({ indeterminate: true }) failed"));
                Assert.That(result.Name, Is.EqualTo("toBeChecked"));
                Assert.That(result.Pass, Is.False);
                Assert.That(result.Log, Is.Not.Null);
                Assert.That(result.Timeout, Is.EqualTo(1));
                Assert.That(result.AriaSnapshot, Is.Not.Null);
                Assert.That(MessageOf(error), Does.Contain(Lines(@"Error: expect(locator).toBeChecked({ indeterminate: true }) failed

Locator:  locator('#unchecked')
Expected: indeterminate
Received: unchecked
Timeout:  1ms

Call log")));
            }
        }
    }
}
