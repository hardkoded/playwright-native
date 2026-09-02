/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/screencast-actions.spec.ts</c> titles.
    /// Do not edit leftover <c>PageScreencastActions*.cs</c> classes.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryScreencastActionsParityTests : PageTestEx
    {
        private static string Prefix => TestConstants.ServerUrl;

        private static string ButtonUrl => Prefix + "/input/button.html";

        private static string TextareaUrl => Prefix + "/input/textarea.html";

        [PlaywrightTest("screencast-actions.spec.ts", "should show annotation on click")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldShowAnnotationOnClick()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000 }).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            await Assertions.Expect(page.Locator("x-pw-highlight")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-action-point")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-title")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-title")).ToHaveTextAsync(new Regex("click", RegexOptions.IgnoreCase)).ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should render annotation styles")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRenderAnnotationStyles()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000, FontSize = 32 }).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            ILocator highlight = page.Locator("x-pw-highlight");
            await Assertions.Expect(highlight).ToBeVisibleAsync().ConfigureAwait(false);
            JsonElement highlightStyle = await highlight.EvaluateAsync<JsonElement>(
                "el => ({ backgroundColor: el.style.backgroundColor, borderColor: el.style.borderColor })").ConfigureAwait(false);
            Assert.That(highlightStyle.GetProperty("backgroundColor").GetString(), Is.EqualTo("rgba(0, 128, 255, 0.15)"));
            Assert.That(highlightStyle.GetProperty("borderColor").GetString(), Is.EqualTo("rgba(0, 128, 255, 0.6)"));
            var box = await highlight.BoundingBoxAsync().ConfigureAwait(false);
            Assert.That(box, Is.Not.Null);
            Assert.That(box.Width, Is.GreaterThan(0));
            Assert.That(box.Height, Is.GreaterThan(0));

            ILocator actionPoint = page.Locator("x-pw-action-point");
            await Assertions.Expect(actionPoint).ToBeVisibleAsync().ConfigureAwait(false);
            JsonElement apStyle = await actionPoint.EvaluateAsync<JsonElement>(
                @"el => {
    const cs = getComputedStyle(el);
    return { width: cs.width, height: cs.height, background: cs.backgroundColor, borderRadius: cs.borderRadius };
}").ConfigureAwait(false);
            Assert.That(apStyle.GetProperty("width").GetString(), Is.EqualTo("20px"));
            Assert.That(apStyle.GetProperty("height").GetString(), Is.EqualTo("20px"));
            Assert.That(apStyle.GetProperty("background").GetString(), Is.EqualTo("rgb(255, 0, 0)"));
            Assert.That(apStyle.GetProperty("borderRadius").GetString(), Is.EqualTo("10px"));

            ILocator title = page.Locator("x-pw-title");
            await Assertions.Expect(title).ToBeVisibleAsync().ConfigureAwait(false);
            JsonElement titleStyle = await title.EvaluateAsync<JsonElement>(
                @"el => {
    const cs = getComputedStyle(el);
    return {
      color: cs.color, borderRadius: cs.borderRadius, padding: cs.padding,
      top: el.style.top, right: el.style.right, fontSize: el.style.fontSize,
    };
}").ConfigureAwait(false);
            Assert.That(titleStyle.GetProperty("color").GetString(), Is.EqualTo("rgb(255, 255, 255)"));
            Assert.That(titleStyle.GetProperty("borderRadius").GetString(), Is.EqualTo("6px"));
            Assert.That(titleStyle.GetProperty("padding").GetString(), Is.EqualTo("6px"));
            Assert.That(titleStyle.GetProperty("top").GetString(), Is.EqualTo("6px"));
            Assert.That(titleStyle.GetProperty("right").GetString(), Is.EqualTo("6px"));
            Assert.That(titleStyle.GetProperty("fontSize").GetString(), Is.EqualTo("32px"));

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should position title at top-left")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldPositionTitleAtTopLeft()
            => AssertTitlePositionAsync(AnnotatePosition.TopLeft, top: "6px", left: "6px");

        [PlaywrightTest("screencast-actions.spec.ts", "should position title at top")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldPositionTitleAtTop()
            => AssertTitlePositionAsync(AnnotatePosition.Top, top: "6px", left: "50%");

        [PlaywrightTest("screencast-actions.spec.ts", "should position title at bottom-left")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldPositionTitleAtBottomLeft()
            => AssertTitlePositionAsync(AnnotatePosition.BottomLeft, bottom: "6px", left: "6px");

        [PlaywrightTest("screencast-actions.spec.ts", "should position title at bottom")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldPositionTitleAtBottom()
            => AssertTitlePositionAsync(AnnotatePosition.Bottom, bottom: "6px", left: "50%");

        [PlaywrightTest("screencast-actions.spec.ts", "should position title at bottom-right")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldPositionTitleAtBottomRight()
            => AssertTitlePositionAsync(AnnotatePosition.BottomRight, bottom: "6px", right: "6px");

        [PlaywrightTest("screencast-actions.spec.ts", "should clear annotation after duration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClearAnnotationAfterDuration()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 1000 }).ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("x-pw-action-point")).ToBeHiddenAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-title")).ToBeHiddenAsync().ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should annotate fill action")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAnnotateFillAction()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(TextareaUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000 }).ConfigureAwait(false);
            Observe(page.FillAsync("textarea", "hello"));

            ILocator title = page.Locator("x-pw-title");
            await Assertions.Expect(title).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(title).ToHaveTextAsync(new Regex("fill", RegexOptions.IgnoreCase)).ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should stop showing actions after dispose")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStopShowingActionsAfterDispose()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            IAsyncDisposable actions = await page.Screencast.ShowActionsAsync(new() { Duration = 1000 }).ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            await actions.DisposeAsync().ConfigureAwait(false);

            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("x-pw-title")).ToBeHiddenAsync().ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should stop showing actions after hideActions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldStopShowingActionsAfterHideActions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 1000 }).ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);
            await page.Screencast.HideActionsAsync().ConfigureAwait(false);

            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);
            await page.ClickAsync("button").ConfigureAwait(false);

            await Assertions.Expect(page.Locator("x-pw-title")).ToBeHiddenAsync().ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should render an action cursor that animates to the click point")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRenderAnActionCursorThatAnimatesToTheClickPoint()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000 }).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            ILocator cursor = page.Locator("x-pw-action-cursor");
            await Assertions.Expect(cursor).ToBeVisibleAsync().ConfigureAwait(false);

            JsonElement initial = await cursor.EvaluateAsync<JsonElement>(
                "el => ({ top: el.style.top, left: el.style.left })").ConfigureAwait(false);
            Assert.That(initial.GetProperty("top").GetString(), Does.Match(@"\d+px"));
            Assert.That(initial.GetProperty("left").GetString(), Does.Match(@"\d+px"));

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "cursor moves between two pointer actions")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CursorMovesBetweenTwoPointerActions()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync(@"
    <div style=""position: fixed; top: 20px; left: 20px; width: 60px; height: 60px;"" id=""a"">A</div>
    <div style=""position: fixed; bottom: 20px; right: 20px; width: 60px; height: 60px;"" id=""b"">B</div>
  ").ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000 }).ConfigureAwait(false);
            ILocator cursor = page.Locator("x-pw-action-cursor");

            Observe(page.ClickAsync("#a", new() { Force = true }));
            await Assertions.Expect(cursor).ToBeVisibleAsync().ConfigureAwait(false);
            JsonElement first = await cursor.EvaluateAsync<JsonElement>(
                "el => ({ top: el.style.top, left: el.style.left })").ConfigureAwait(false);

            Observe(page.ClickAsync("#b", new() { Force = true }));
            await PollAsync(
                async () =>
                {
                    JsonElement current = await cursor.EvaluateAsync<JsonElement>(
                        "el => ({ top: el.style.top, left: el.style.left })").ConfigureAwait(false);
                    return current.GetProperty("top").GetString() != first.GetProperty("top").GetString()
                        || current.GetProperty("left").GetString() != first.GetProperty("left").GetString();
                },
                TestConstants.DefaultTestTimeout).ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "cursor: \"none\" suppresses the action cursor decoration")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CursorNoneSuppressesTheActionCursorDecoration()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000, Cursor = ScreencastCursor.None }).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            await Assertions.Expect(page.Locator("x-pw-action-point")).ToBeVisibleAsync().ConfigureAwait(false);
            await Assertions.Expect(page.Locator("x-pw-action-cursor")).ToBeHiddenAsync().ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("screencast-actions.spec.ts", "should survive navigation")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveNavigation()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000 }).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            await Assertions.Expect(page.Locator("x-pw-title")).ToBeVisibleAsync().ConfigureAwait(false);

            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            await Assertions.Expect(page.Locator("x-pw-title")).ToBeVisibleAsync().ConfigureAwait(false);

            await context.CloseAsync().ConfigureAwait(false);
        }

        private static void Observe(Task task)
        {
            _ = task.ContinueWith(static t => _ = t.Exception, TaskContinuationOptions.OnlyOnFaulted);
        }

        private static async Task AssertTitlePositionAsync(
            AnnotatePosition position,
            string top = null,
            string bottom = null,
            string left = null,
            string right = null)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(ButtonUrl).ConfigureAwait(false);

            await page.Screencast.ShowActionsAsync(new() { Duration = 5000, Position = position }).ConfigureAwait(false);
            Observe(page.ClickAsync("button"));

            ILocator title = page.Locator("x-pw-title");
            await Assertions.Expect(title).ToBeVisibleAsync().ConfigureAwait(false);

            JsonElement titleStyle = await title.EvaluateAsync<JsonElement>(
                "el => ({ top: el.style.top, bottom: el.style.bottom, left: el.style.left, right: el.style.right })").ConfigureAwait(false);
            if (top != null)
            {
                Assert.That(titleStyle.GetProperty("top").GetString(), Is.EqualTo(top));
            }

            if (bottom != null)
            {
                Assert.That(titleStyle.GetProperty("bottom").GetString(), Is.EqualTo(bottom));
            }

            if (left != null)
            {
                Assert.That(titleStyle.GetProperty("left").GetString(), Is.EqualTo(left));
            }

            if (right != null)
            {
                Assert.That(titleStyle.GetProperty("right").GetString(), Is.EqualTo(right));
            }

            await context.CloseAsync().ConfigureAwait(false);
        }

        private static async Task PollAsync(Func<Task<bool>> predicate, int timeoutMs)
        {
            DateTimeOffset deadline = DateTimeOffset.UtcNow.AddMilliseconds(timeoutMs);
            while (DateTimeOffset.UtcNow < deadline)
            {
                if (await predicate().ConfigureAwait(false))
                {
                    return;
                }

                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.Fail("Timed out waiting for cursor position to change.");
        }
    }
}
