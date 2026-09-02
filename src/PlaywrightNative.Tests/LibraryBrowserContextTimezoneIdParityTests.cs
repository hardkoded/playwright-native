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
using System.Globalization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/browsercontext-timezone-id.spec.ts</c> parity.
    /// Do not edit leftover <c>ContextEnvironmentTests</c> or
    /// <c>LaunchPersistentTimezoneTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryBrowserContextTimezoneIdParityTests : PageTestEx
    {
        private const string DateToString = "() => new Date(1479579154987).toString()";

        private static SimpleServer _ownedServer;
        private static string EmptyPage = TestConstants.EmptyPage;

        private IBrowser _browser;

        private static SimpleServer Server => _ownedServer ?? TestServerSetup.Server;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            int basePort = 19854;
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

            if (TestServerSetup.Server != null)
            {
                EmptyPage = TestConstants.EmptyPage;
                return;
            }

            Assert.Ignore("Test server is unavailable.");
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedServer != null)
            {
                await _ownedServer.StopAsync().ConfigureAwait(false);
                _ownedServer = null;
            }

            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
            }
        }

        [SetUp]
        public async Task SetUpAsync()
        {
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }

            _browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
        }

        [TearDown]
        public async Task TearDownAsync()
        {
            _ownedServer?.Reset();
            TestServerSetup.Server?.Reset();
            if (_browser != null)
            {
                await DisposeQuietlyAsync(_browser).ConfigureAwait(false);
                _browser = null;
            }
        }

        [PlaywrightTest("browsercontext-timezone-id.spec.ts", "should work @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await AssertTimezoneContainsAsync("America/Jamaica", "Sat Nov 19 2016 13:12:34 GMT-0500").ConfigureAwait(false);
            await AssertTimezoneContainsAsync("Pacific/Honolulu", "Sat Nov 19 2016 08:12:34 GMT-1000").ConfigureAwait(false);
            await AssertTimezoneContainsAsync("America/Buenos_Aires", "Sat Nov 19 2016 15:12:34 GMT-0300").ConfigureAwait(false);
            await AssertTimezoneContainsAsync("Europe/Berlin", "Sat Nov 19 2016 19:12:34 GMT+0100").ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-timezone-id.spec.ts", "should throw for invalid timezone IDs when creating pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForInvalidTimezoneIDsWhenCreatingPages()
        {
            foreach (string timezoneId in new[] { "Foo/Bar", "Baz/Qux" })
            {
                IBrowserContext context = null;
                PlaywrightNativeException error = null;
                try
                {
                    context = await _browser.NewContextAsync(new() { TimezoneId = timezoneId }).ConfigureAwait(false);
                    await context.NewPageAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException ex)
                {
                    error = ex;
                }

                Assert.That(error, Is.Not.Null);
                Assert.That(error.Message, Does.Contain("Invalid timezone ID: " + timezoneId));
                if (context != null)
                {
                    await context.CloseAsync().ConfigureAwait(false);
                }
            }
        }

        [PlaywrightTest("browsercontext-timezone-id.spec.ts", "should work for multiple pages sharing same process")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkForMultiplePagesSharingSameProcess()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { TimezoneId = "Europe/Moscow" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IPage> popupTask = page.WaitForPopupAsync();
            await page.EvaluateAsync("(url) => { window.open(url); }", EmptyPage).ConfigureAwait(false);
            IPage popup = await popupTask.ConfigureAwait(false);
            Task<IPage> nestedTask = popup.WaitForPopupAsync();
            await popup.EvaluateAsync("(url) => { window.open(url); }", EmptyPage).ConfigureAwait(false);
            await nestedTask.ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-timezone-id.spec.ts", "should not change default timezone in another context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotChangeDefaultTimezoneInAnotherContext()
        {
            async Task<string> GetContextTimezoneAsync(IBrowserContext context)
            {
                IPage page = await context.NewPageAsync().ConfigureAwait(false);
                return await page.EvaluateAsync<string>("() => Intl.DateTimeFormat().resolvedOptions().timeZone").ConfigureAwait(false);
            }

            IBrowserContext first = await _browser.NewContextAsync().ConfigureAwait(false);
            string defaultTimezone = await GetContextTimezoneAsync(first).ConfigureAwait(false);
            await first.CloseAsync().ConfigureAwait(false);

            string timezoneOverride = defaultTimezone == "Europe/Moscow" ? "America/Los_Angeles" : "Europe/Moscow";
            IBrowserContext overrideContext = await _browser.NewContextAsync(new() { TimezoneId = timezoneOverride }).ConfigureAwait(false);
            Assert.That(await GetContextTimezoneAsync(overrideContext).ConfigureAwait(false), Is.EqualTo(timezoneOverride));
            await overrideContext.CloseAsync().ConfigureAwait(false);

            IBrowserContext again = await _browser.NewContextAsync().ConfigureAwait(false);
            Assert.That(await GetContextTimezoneAsync(again).ConfigureAwait(false), Is.EqualTo(defaultTimezone));
            await again.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-timezone-id.spec.ts", "should affect Intl.DateTimeFormat().resolvedOptions().timeZone")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAffectIntlDateTimeFormatResolvedOptionsTimeZone()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { TimezoneId = "America/Jamaica" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Assert.That(
                await page.EvaluateAsync<string>("() => (new Intl.DateTimeFormat()).resolvedOptions().timeZone").ConfigureAwait(false),
                Is.EqualTo("America/Jamaica"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("browsercontext-timezone-id.spec.ts", "should propagate timezone to workers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldPropagateTimezoneToWorkers()
        {
            EnsureServer();
            IBrowserContext context = await _browser.NewContextAsync(new() { TimezoneId = "America/Jamaica" }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(EmptyPage).ConfigureAwait(false);
            Task<IConsoleMessage> messageTask = page.WaitForEventAsync(PageEvent.Console);
            await page.EvaluateAsync(
                "() => new Worker(URL.createObjectURL(new Blob(['console.log(Intl.DateTimeFormat().resolvedOptions().timeZone)'], { type: 'application/javascript' })))").ConfigureAwait(false);
            IConsoleMessage message = await messageTask.ConfigureAwait(false);
            Assert.That(message.Text, Is.EqualTo("America/Jamaica"));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private async Task AssertTimezoneContainsAsync(string timezoneId, string expected)
        {
            IBrowserContext context = await _browser.NewContextAsync(new() { Locale = "en-US", TimezoneId = timezoneId }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            string actual = await page.EvaluateAsync<string>(DateToString).ConfigureAwait(false);
            Assert.That(actual, Does.Contain(expected));
            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void EnsureServer()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
            }
        }

        private static async Task DisposeQuietlyAsync(IAsyncDisposable disposable)
        {
            try
            {
                await disposable.DisposeAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
            }
        }
    }
}
