/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.WaitForConsoleMessageAsync"/> and
    /// <see cref="IBrowserContext.WaitForPageAsync"/>.
    /// </summary>
    [TestFixture]
    public class WaitForConsolePageTests : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "WaitForConsoleMessage resolves on console.log")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForConsoleMessageShouldResolveOnLog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>("console.log('wave116')").ConfigureAwait(false);
            IConsoleMessage received = await waitTask.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Text, Does.Contain("wave116"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "Console message Args are populated")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleMessageArgsShouldBePopulated()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>("console.log('hello-args', 42)").ConfigureAwait(false);
            IConsoleMessage received = await waitTask.ConfigureAwait(false);

            Assert.That(received.Args, Is.Not.Null);
            Assert.That(received.Args, Is.Not.Empty);
            IJSHandle first = received.Args.First();
            string value = await first.JsonValueAsync<string>().ConfigureAwait(false);
            Assert.That(value, Does.Contain("hello-args"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "Console message Timestamp is populated")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleMessageTimestampShouldBePopulated()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>("console.log('ts')").ConfigureAwait(false);
            IConsoleMessage received = await waitTask.ConfigureAwait(false);

            Assert.That(received.Timestamp, Is.GreaterThan(0));
        }

        [PlaywrightTest("page-event-console.spec.ts", "RunAndWaitForConsoleMessageAsync waits for console.log")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForConsoleMessageAsyncShouldReturnTheMessage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IConsoleMessage received = await page.RunAndWaitForConsoleMessageAsync(
                () => page.EvaluateAsync<object>("console.log('run-wait-console')")).ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Text, Does.Contain("run-wait-console"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "BrowserContext Console forwards page logs")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserContextConsoleShouldForwardPageLogs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            TaskCompletionSource<IConsoleMessage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            context.Console += (_, message) => tcs.TrySetResult(message);
            await page.EvaluateAsync<object>("console.log('context-console')").ConfigureAwait(false);
            IConsoleMessage received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Text, Does.Contain("context-console"));
            Assert.That(received.Page, Is.SameAs(page));
        }

        [PlaywrightTest("page-event-console.spec.ts", "WaitForConsoleMessageAsync on context")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserContextWaitForConsoleMessageShouldResolveOnLog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> waitTask = context.WaitForConsoleMessageAsync();
            await page.EvaluateAsync<object>("console.log('context-wait-console')").ConfigureAwait(false);
            IConsoleMessage received = await waitTask.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Text, Does.Contain("context-wait-console"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "RunAndWaitForConsoleMessageAsync on context")]
        [Test]
        [Timeout(30_000)]
        public async Task BrowserContextRunAndWaitForConsoleMessageShouldReturnTheMessage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            IConsoleMessage received = await context.RunAndWaitForConsoleMessageAsync(
                () => page.EvaluateAsync<object>("console.log('context-run-wait')")).ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Text, Does.Contain("context-run-wait"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "WaitForConsoleMessage honors predicate")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForConsoleMessageShouldHonorPredicate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IConsoleMessage> waitTask = page.WaitForConsoleMessageAsync(
                m => m.Text != null && m.Text.Contains("keep", StringComparison.Ordinal));
            await page.EvaluateAsync<object>("console.log('skip')").ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('keep-me')").ConfigureAwait(false);
            IConsoleMessage received = await waitTask.ConfigureAwait(false);

            Assert.That(received.Text, Does.Contain("keep-me"));
        }

        [PlaywrightTest("page-event-console.spec.ts", "WaitForPage resolves on NewPageAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForPageShouldResolveOnNewPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            Task<IPage> waitTask = context.WaitForPageAsync();
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            IPage fromWait = await waitTask.ConfigureAwait(false);

            Assert.That(fromWait, Is.SameAs(page));
        }

        [PlaywrightTest("page-event-console.spec.ts", "RunAndWaitForPageAsync waits for NewPageAsync")]
        [Test]
        [Timeout(30_000)]
        public async Task RunAndWaitForPageAsyncShouldReturnThePage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);

            IPage page = await context.RunAndWaitForPageAsync(
                () => context.NewPageAsync()).ConfigureAwait(false);

            Assert.That(page, Is.Not.Null);
            Assert.That(context.Pages, Does.Contain(page));
        }

        [PlaywrightTest("page-event-console.spec.ts", "WaitForPage resolves on window.open")]
        [Test]
        [Timeout(30_000)]
        public async Task WaitForPageShouldResolveOnWindowOpen()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IPage> waitTask = context.WaitForPageAsync();
            await page.EvaluateAsync<bool>("window.open('about:blank'), true").ConfigureAwait(false);
            IPage popup = await waitTask.ConfigureAwait(false);

            Assert.That(popup, Is.Not.Null);
            Assert.That(popup, Is.Not.SameAs(page));
            Assert.That(context.Pages, Does.Contain(popup));
        }

        [PlaywrightTest("page-event-console.spec.ts", "ConsoleMessagesAsync returns recorded logs")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleMessagesAsyncShouldReturnRecordedLogs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            await page.EvaluateAsync<object>("console.log('wave340-a')").ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('wave340-b')").ConfigureAwait(false);

            IReadOnlyList<IConsoleMessage> messages = await page.ConsoleMessagesAsync().ConfigureAwait(false);
            Assert.That(messages, Is.Not.Null);
            Assert.That(messages.Select(item => item.Text), Does.Contain("wave340-a"));
            Assert.That(messages.Select(item => item.Text), Does.Contain("wave340-b"));
        }
    }
}
