/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>expect-timeout.spec.ts</c> parity.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class ExpectTimeoutParityTests : PageTestEx
    {
        private static SimpleServer _ownedServer;
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
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19815;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer server = SimpleServer.Create(port, contentRoot);
                    await server.StartAsync().ConfigureAwait(false);
                    _ownedServer = server;
                    EmptyPage = "http://localhost:" + port.ToString(CultureInfo.InvariantCulture) + "/empty.html";
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
        }

        private IPage Page => _page;

        [PlaywrightTest("expect-timeout.spec.ts", "should print element not found")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintElementNotFound()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("no-such-thing")).ToHaveTextAsync("hey", new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator: locator('no-such-thing')
Expected: ""hey""
Timeout: 1000ms
Error: element(s) not found

Call log:
")));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should print timed out error message when value does not match")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintTimedOutErrorMessageWhenValueDoesNotMatch()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("hey", new() { Timeout = 1000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator:  locator('div')
Expected: ""hey""
Received: ""Text content""
Timeout:  1000ms

Call log:
")));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should print timed out error message with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintTimedOutErrorMessageWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("no-such-thing")).ToHaveTextAsync("hey", new() { Timeout = 1 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator: locator('no-such-thing')
Expected: ""hey""
Timeout: 1ms
Error: element(s) not found

Call log:")));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should print timed out error message when value does not match with impossible timeout")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPrintTimedOutErrorMessageWhenValueDoesNotMatchWithImpossibleTimeout()
        {
            await Page.SetContentAsync("<div id=node>Text content</div>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("hey", new() { Timeout = 1 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator:  locator('div')
Expected: ""hey""
Received: ""Text content""
Timeout:  1ms

Call log:
")));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should have timeout error name")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHaveTimeoutErrorName()
        {
            Exception error = Assert.CatchAsync(() => Page.WaitForSelectorAsync("#not-found", new() { Timeout = 1 }));
            Assert.That(error, Is.InstanceOf<TimeoutException>());
            await Task.CompletedTask.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should not throw when navigating during one-shot check")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenNavigatingDuringOneShotCheck()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            await Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Task promise = Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("bye");
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync("<div>bye</div>").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should not throw when navigating during first locator handler check")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotThrowWhenNavigatingDuringFirstLocatorHandlerCheck()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }

            await Page.AddLocatorHandlerAsync(Page.Locator("span"), async (ILocator locator) =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            await Page.SetContentAsync("<div>hello</div>").ConfigureAwait(false);
            Task promise = Assertions.Expect(Page.Locator("div")).ToHaveTextAsync("bye");
            await Page.GoToAsync(EmptyPage).ConfigureAwait(false);
            await Page.SetContentAsync("<div>bye</div>").ConfigureAwait(false);
            await promise.ConfigureAwait(false);
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should timeout during first locator handler check")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldTimeoutDuringFirstLocatorHandlerCheck()
        {
            await Page.AddLocatorHandlerAsync(Page.Locator("div"), async (ILocator locator) =>
            {
                await Task.CompletedTask.ConfigureAwait(false);
            }).ConfigureAwait(false);
            await Page.SetContentAsync("<div>hello</div><span>bye</span>").ConfigureAwait(false);
            Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("span")).ToHaveTextAsync("bye", new() { Timeout = 3000 }));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toHaveText(expected) failed

Locator:  locator('span')
Expected: ""bye""
Received: """"
Timeout:  3000ms

Call log:
")));
            Assert.That(error.Message, Does.Contain("locator handler has finished, waiting for locator('div') to be hidden"));
            Assert.That(error.Message, Does.Contain("locator resolved to visible <div>hello</div>"));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should not miss element that appears between retries before the deadline")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotMissElementThatAppearsBetweenRetriesBeforeTheDeadline()
        {
            await Page.SetContentAsync("<div id=\"target\" style=\"display:none\">content</div>").ConfigureAwait(false);
            await Page.EvaluateAsync<object>(@"() => {
                setTimeout(() => {
                    document.getElementById('target').style.display = 'block';
                }, 1500);
            }").ConfigureAwait(false);
            await Assertions.Expect(Page.Locator("#target")).ToBeVisibleAsync(new() { Timeout = 1800 }).ConfigureAwait(false);
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should fail like a timeout when the signal is aborted mid-assertion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailLikeATimeoutWhenTheSignalIsAbortedMidAssertion()
        {
            await Page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            AbortController controller = new AbortController();
            Task promise = Assertions.Expect(Page.Locator("span")).ToBeVisibleAsync(new() { Timeout = 5000 });
            await Page.WaitForTimeoutAsync(500).ConfigureAwait(false);
            controller.Abort(new Exception("stop it"));
            Exception error = Assert.CatchAsync(() => promise);
            Assert.That(error, Is.Not.InstanceOf<OperationCanceledException>());
            Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
            Assert.That(MessageOf(error), Does.Contain(Lines(@"expect(locator).toBeVisible() failed

Locator: locator('span')
Expected: visible
Error: element(s) not found

Call log:
  - Expect ""toBeVisible"" locator('span') with timeout 5000ms
  - waiting for locator('span')
  - operation was aborted: stop it")));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should fail like a timeout when toHaveText is aborted mid-assertion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailLikeATimeoutWhenToHaveTextIsAbortedMidAssertion()
        {
            await Page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            AbortController controller = new AbortController();
            Task promise = Assertions.Expect(Page.Locator("span")).ToHaveTextAsync("missing", new() { Timeout = 5000 });
            await Page.WaitForTimeoutAsync(300).ConfigureAwait(false);
            controller.Abort(new Exception("stop it"));
            Exception error = Assert.CatchAsync(() => promise);
            Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
            Assert.That(MessageOf(error), Does.Contain("expect(locator).toHaveText(expected) failed"));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should fail like a timeout when toHaveCount is aborted mid-assertion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailLikeATimeoutWhenToHaveCountIsAbortedMidAssertion()
        {
            await Page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            AbortController controller = new AbortController();
            Task promise = Assertions.Expect(Page.Locator("span")).ToHaveCountAsync(3, new() { Timeout = 5000 });
            await Page.WaitForTimeoutAsync(300).ConfigureAwait(false);
            controller.Abort(new Exception("stop it"));
            Exception error = Assert.CatchAsync(() => promise);
            Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
            Assert.That(MessageOf(error), Does.Contain("expect(locator).toHaveCount(expected) failed"));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should fail like a timeout when toMatchAriaSnapshot is aborted mid-assertion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailLikeATimeoutWhenToMatchAriaSnapshotIsAbortedMidAssertion()
        {
            await Page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            AbortController controller = new AbortController();
            Task promise = Assertions.Expect(Page.Locator("body")).ToMatchAriaSnapshotAsync("- list", new() { Timeout = 5000 });
            await Page.WaitForTimeoutAsync(300).ConfigureAwait(false);
            controller.Abort(new Exception("stop it"));
            Exception error = Assert.CatchAsync(() => promise);
            Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
            Assert.That(MessageOf(error), Does.Contain("expect(locator).toMatchAriaSnapshot(expected) failed"));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should fail like a timeout when toHaveURL is aborted mid-assertion")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailLikeATimeoutWhenToHaveURLIsAbortedMidAssertion()
        {
            await Page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            AbortController controller = new AbortController();
            Task promise = Assertions.Expect(Page).ToHaveURLAsync("https://example.com/", new() { Timeout = 5000 });
            await Page.WaitForTimeoutAsync(500).ConfigureAwait(false);
            controller.Abort(new Exception("stop it"));
            Exception error = Assert.CatchAsync(() => promise);
            Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
            Assert.That(MessageOf(error), Does.Contain("expect(page).toHaveURL(expected) failed"));
        }

        [PlaywrightTest("expect-timeout.spec.ts", "should fail the assertion when the signal is already aborted")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFailTheAssertionWhenTheSignalIsAlreadyAborted()
        {
            await Page.SetContentAsync("<div>content</div>").ConfigureAwait(false);
            {
                AbortController controller = new AbortController();
                controller.Abort(new Exception("already aborted"));
                Exception error = Assert.CatchAsync(() => Assertions.Expect(Page.Locator("div")).ToBeVisibleAsync(new() { Timeout = 5000 }));
                Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
                Assert.That(MessageOf(error), Is.EqualTo(Lines(@"expect(locator).toBeVisible() failed

Locator: locator('div')
Expected: visible
Error: The assertion was aborted: already aborted
")));
            }

            {
                if (Server == null)
                {
                    Assert.Ignore("Test server is unavailable.");
                }

                AbortController controller = new AbortController();
                controller.Abort("stop it");
                Exception error = Assert.CatchAsync(() => Assertions.Expect(Page).ToHaveURLAsync(EmptyPage, new() { Timeout = 5000 }));
                Assert.That(error.GetType().Name, Is.Not.EqualTo("AbortError"));
                Assert.That(MessageOf(error), Is.EqualTo(Lines("expect(page).toHaveURL(expected) failed\n\nExpected: " + System.Text.Json.JsonSerializer.Serialize(EmptyPage) + "\nError: The assertion was aborted: stop it\n")));
            }
        }
    }
}
