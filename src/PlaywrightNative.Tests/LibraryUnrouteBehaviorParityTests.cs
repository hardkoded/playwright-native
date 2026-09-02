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
    /// Official <c>library/unroute-behavior.spec.ts</c> parity. Do not edit
    /// leftover <c>UnrouteBehaviorTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryUnrouteBehaviorParityTests : PageTestEx
    {
        private static readonly Regex AnyUrl = new Regex(".*", RegexOptions.CultureInvariant);

        [PlaywrightTest("unroute-behavior.spec.ts", "context.unroute should not wait for pending handlers to complete")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextUnrouteShouldNotWaitForPendingHandlersToComplete()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await context.RouteAsync(AnyUrl, async route =>
            {
                secondHandlerCalled = true;
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> routeBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<IRoute, Task> handler = async route =>
            {
                routeStarted.TrySetResult(true);
                await routeBarrier.Task.ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            };
            await context.RouteAsync(AnyUrl, handler).ConfigureAwait(false);

            Task navigation = page.GoToAsync(TestConstants.EmptyPage);
            await routeStarted.Task.ConfigureAwait(false);
            await context.UnrouteAsync(AnyUrl, handler).ConfigureAwait(false);
            routeBarrier.TrySetResult(true);
            await navigation.ConfigureAwait(false);
            Assert.That(secondHandlerCalled, Is.True);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context.unrouteAll removes all handlers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextUnrouteAllRemovesAllHandlers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await context.RouteAsync("**/*", route =>
            {
                _ = route.AbortAsync();
            }).ConfigureAwait(false);
            await context.RouteAsync("**/empty.html", route =>
            {
                _ = route.AbortAsync();
            }).ConfigureAwait(false);
            await context.UnrouteAllAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context.unrouteAll should wait for pending handlers to complete")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextUnrouteAllShouldWaitForPendingHandlersToComplete()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await context.RouteAsync(AnyUrl, async route =>
            {
                secondHandlerCalled = true;
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> routeBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await context.RouteAsync(AnyUrl, async route =>
            {
                routeStarted.TrySetResult(true);
                await routeBarrier.Task.ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task navigation = page.GoToAsync(TestConstants.EmptyPage);
            await routeStarted.Task.ConfigureAwait(false);
            bool didUnroute = false;
            Task unroute = context.UnrouteAllAsync(UnrouteBehavior.Wait).ContinueWith(
                _ =>
                {
                    didUnroute = true;
                },
                TaskScheduler.Default);
            await Task.Delay(500).ConfigureAwait(false);
            Assert.That(didUnroute, Is.False);
            routeBarrier.TrySetResult(true);
            await unroute.ConfigureAwait(false);
            Assert.That(didUnroute, Is.True);
            await navigation.ConfigureAwait(false);
            Assert.That(secondHandlerCalled, Is.False);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context.unrouteAll should not wait for pending handlers to complete if behavior is ignoreErrors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextUnrouteAllShouldNotWaitForPendingHandlersToCompleteIfBehaviorIsIgnoreErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await context.RouteAsync(AnyUrl, async route =>
            {
                secondHandlerCalled = true;
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> routeBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await context.RouteAsync(AnyUrl, async route =>
            {
                routeStarted.TrySetResult(true);
                await routeBarrier.Task.ConfigureAwait(false);
                throw new InvalidOperationException("Handler error");
            }).ConfigureAwait(false);

            Task navigation = page.GoToAsync(TestConstants.EmptyPage);
            await routeStarted.Task.ConfigureAwait(false);
            bool didUnroute = false;
            Task unroute = context.UnrouteAllAsync(UnrouteBehavior.IgnoreErrors).ContinueWith(
                _ =>
                {
                    didUnroute = true;
                },
                TaskScheduler.Default);
            await Task.Delay(500).ConfigureAwait(false);
            await unroute.ConfigureAwait(false);
            Assert.That(didUnroute, Is.True);
            routeBarrier.TrySetResult(true);
            try
            {
                await navigation.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            Assert.That(secondHandlerCalled, Is.False);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page.close should not wait for active route handlers on the owning context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageCloseShouldNotWaitForActiveRouteHandlersOnTheOwningContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await context.RouteAsync(AnyUrl, route =>
            {
                routeStarted.TrySetResult(true);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            await page.RouteAsync(AnyUrl, async route =>
            {
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            _ = page.GoToAsync(TestConstants.EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            await routeStarted.Task.ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "context.close should not wait for active route handlers on the owned pages")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ContextCloseShouldNotWaitForActiveRouteHandlersOnTheOwnedPages()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, route =>
            {
                routeStarted.TrySetResult(true);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            await page.RouteAsync(AnyUrl, async route =>
            {
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
            _ = page.GoToAsync(TestConstants.EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            await routeStarted.Task.ConfigureAwait(false);
            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page.unroute should not wait for pending handlers to complete")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageUnrouteShouldNotWaitForPendingHandlersToComplete()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await page.RouteAsync(AnyUrl, async route =>
            {
                secondHandlerCalled = true;
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> routeBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Func<IRoute, Task> handler = async route =>
            {
                routeStarted.TrySetResult(true);
                await routeBarrier.Task.ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            };
            await page.RouteAsync(AnyUrl, handler).ConfigureAwait(false);

            Task navigation = page.GoToAsync(TestConstants.EmptyPage);
            await routeStarted.Task.ConfigureAwait(false);
            await page.UnrouteAsync(AnyUrl, handler).ConfigureAwait(false);
            routeBarrier.TrySetResult(true);
            await navigation.ConfigureAwait(false);
            Assert.That(secondHandlerCalled, Is.True);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page.unrouteAll removes all routes")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageUnrouteAllRemovesAllRoutes()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.RouteAsync("**/*", route =>
            {
                _ = route.AbortAsync();
            }).ConfigureAwait(false);
            await page.RouteAsync("**/empty.html", route =>
            {
                _ = route.AbortAsync();
            }).ConfigureAwait(false);
            await page.UnrouteAllAsync().ConfigureAwait(false);
            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response.Ok, Is.True);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page.unrouteAll should wait for pending handlers to complete")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageUnrouteAllShouldWaitForPendingHandlersToComplete()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await page.RouteAsync(AnyUrl, async route =>
            {
                secondHandlerCalled = true;
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> routeBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, async route =>
            {
                routeStarted.TrySetResult(true);
                await routeBarrier.Task.ConfigureAwait(false);
                await route.FallbackAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            Task navigation = page.GoToAsync(TestConstants.EmptyPage);
            await routeStarted.Task.ConfigureAwait(false);
            bool didUnroute = false;
            Task unroute = page.UnrouteAllAsync(UnrouteBehavior.Wait).ContinueWith(
                _ =>
                {
                    didUnroute = true;
                },
                TaskScheduler.Default);
            await Task.Delay(500).ConfigureAwait(false);
            Assert.That(didUnroute, Is.False);
            routeBarrier.TrySetResult(true);
            await unroute.ConfigureAwait(false);
            Assert.That(didUnroute, Is.True);
            await navigation.ConfigureAwait(false);
            Assert.That(secondHandlerCalled, Is.False);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page.unrouteAll should not wait for pending handlers to complete if behavior is ignoreErrors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageUnrouteAllShouldNotWaitForPendingHandlersToCompleteIfBehaviorIsIgnoreErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await page.RouteAsync(AnyUrl, async route =>
            {
                secondHandlerCalled = true;
                await route.AbortAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            TaskCompletionSource<bool> routeBarrier = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, async route =>
            {
                routeStarted.TrySetResult(true);
                await routeBarrier.Task.ConfigureAwait(false);
                throw new InvalidOperationException("Handler error");
            }).ConfigureAwait(false);

            Task navigation = page.GoToAsync(TestConstants.EmptyPage);
            await routeStarted.Task.ConfigureAwait(false);
            bool didUnroute = false;
            Task unroute = page.UnrouteAllAsync(UnrouteBehavior.IgnoreErrors).ContinueWith(
                _ =>
                {
                    didUnroute = true;
                },
                TaskScheduler.Default);
            await Task.Delay(500).ConfigureAwait(false);
            await unroute.ConfigureAwait(false);
            Assert.That(didUnroute, Is.True);
            routeBarrier.TrySetResult(true);
            try
            {
                await navigation.ConfigureAwait(false);
            }
            catch (Exception)
            {
            }

            Assert.That(secondHandlerCalled, Is.False);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "page.close does not wait for active route handlers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task PageCloseDoesNotWaitForActiveRouteHandlers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            bool secondHandlerCalled = false;
            await page.RouteAsync(AnyUrl, _ =>
            {
                secondHandlerCalled = true;
            }).ConfigureAwait(false);
            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, async route =>
            {
                routeStarted.TrySetResult(true);
                await new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously).Task
                    .ConfigureAwait(false);
            }).ConfigureAwait(false);
            _ = page.GoToAsync(TestConstants.EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            await routeStarted.Task.ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            await Task.Delay(500).ConfigureAwait(false);
            Assert.That(secondHandlerCalled, Is.False);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "route.continue should not throw if page has been closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RouteContinueShouldNotThrowIfPageHasBeenClosed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IRoute> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, route =>
            {
                routeStarted.TrySetResult(route);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            _ = page.GoToAsync(TestConstants.EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            IRoute route = await routeStarted.Task.ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            await route.ContinueAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "route.fallback should not throw if page has been closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RouteFallbackShouldNotThrowIfPageHasBeenClosed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IRoute> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, route =>
            {
                routeStarted.TrySetResult(route);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            _ = page.GoToAsync(TestConstants.EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            IRoute route = await routeStarted.Task.ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            await route.FallbackAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "route.fulfill should not throw if page has been closed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task RouteFulfillShouldNotThrowIfPageHasBeenClosed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IRoute> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync(AnyUrl, route =>
            {
                routeStarted.TrySetResult(route);
                return Task.CompletedTask;
            }).ConfigureAwait(false);
            _ = page.GoToAsync(TestConstants.EmptyPage).ContinueWith(_ => { }, TaskScheduler.Default);
            IRoute route = await routeStarted.Task.ConfigureAwait(false);
            await page.CloseAsync().ConfigureAwait(false);
            await route.FulfillAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "should not continue requests in flight (page)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotContinueRequestsInFlightPage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await page.RouteAsync("**/*", async route =>
            {
                routeStarted.TrySetResult(true);
                await Task.Delay(3000).ConfigureAwait(false);
                RouteFetchResult response = await route.FetchResultAsync().ConfigureAwait(false);
                await route.FulfillAsync(response).ConfigureAwait(false);
            }).ConfigureAwait(false);
            _ = page.EvaluateAsync("() => fetch('/')").ContinueWith(_ => { }, TaskScheduler.Default);
            await routeStarted.Task.ConfigureAwait(false);
            await page.UnrouteAllAsync(UnrouteBehavior.Wait).ConfigureAwait(false);
        }

        [PlaywrightTest("unroute-behavior.spec.ts", "should not continue requests in flight (context)")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotContinueRequestsInFlightContext()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            TaskCompletionSource<bool> routeStarted = new(TaskCreationOptions.RunContinuationsAsynchronously);
            await context.RouteAsync("**/*", async route =>
            {
                routeStarted.TrySetResult(true);
                await Task.Delay(3000).ConfigureAwait(false);
                RouteFetchResult response = await route.FetchResultAsync().ConfigureAwait(false);
                await route.FulfillAsync(response).ConfigureAwait(false);
            }).ConfigureAwait(false);
            _ = page.EvaluateAsync("() => fetch('/')").ContinueWith(_ => { }, TaskScheduler.Default);
            await routeStarted.Task.ConfigureAwait(false);
            await context.UnrouteAllAsync(UnrouteBehavior.Wait).ConfigureAwait(false);
        }
    }
}
