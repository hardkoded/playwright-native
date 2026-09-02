/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/tap.spec.ts</c> parity. Official <c>page.tap</c>
    /// with context <c>hasTouch</c>. Do not edit leftover
    /// <c>PageTapScrollTests</c>, <c>TapStrictTests</c>, or
    /// <c>CRTapTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryTapParityTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("tap.spec.ts", "should send all of the correct events @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendAllOfTheCorrectEventsSmoke()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
  <div id=""a"" style=""background: lightblue; width: 50px; height: 50px"">a</div>
  <div id=""b"" style=""background: pink; width: 50px; height: 50px"">b</div>
").ConfigureAwait(false);
                await page.TapAsync("#a").ConfigureAwait(false);
                IJSHandle eventsHandle = await TrackEventsAsync(await page.QuerySelectorAsync("#b").ConfigureAwait(false)).ConfigureAwait(false);
                await page.TapAsync("#b").ConfigureAwait(false);

                // webkit doesn't send pointerenter or pointerleave or mouseout
                Assert.That(
                    await eventsHandle.JsonValueAsync<string[]>().ConfigureAwait(false),
                    Is.EqualTo(new[]
                    {
                        "pointerover", "pointerenter",
                        "pointerdown", "touchstart",
                        "pointerup", "pointerout",
                        "pointerleave", "touchend",
                        "mouseover", "mouseenter",
                        "mousemove", "mousedown",
                        "mouseup", "click",
                    }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "trial run should not tap")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task TrialRunShouldNotTap()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
    <div id=""a"" style=""background: lightblue; width: 50px; height: 50px"">a</div>
    <div id=""b"" style=""background: pink; width: 50px; height: 50px"">b</div>
  ").ConfigureAwait(false);
                await page.TapAsync("#a").ConfigureAwait(false);
                IJSHandle eventsHandle = await TrackEventsAsync(await page.QuerySelectorAsync("#b").ConfigureAwait(false)).ConfigureAwait(false);
                await page.TapAsync("#b", new() { Trial = true }).ConfigureAwait(false);
                string[] expected = { "pointerover", "pointerenter", "pointerout", "pointerleave" };
                Assert.That(
                    await eventsHandle.JsonValueAsync<string[]>().ConfigureAwait(false),
                    Is.EqualTo(expected));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should not send mouse events touchstart is canceled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSendMouseEventsTouchstartIsCanceled()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div style=\"width: 50px; height: 50px; background: red\">").ConfigureAwait(false);
                await page.EvaluateAsync(@"() => {
    // touchstart is not cancelable unless passive is false
    document.addEventListener('touchstart', t => t.preventDefault(), { passive: false });
  }").ConfigureAwait(false);
                IJSHandle eventsHandle = await TrackEventsAsync(await page.QuerySelectorAsync("div").ConfigureAwait(false)).ConfigureAwait(false);
                await page.TapAsync("div").ConfigureAwait(false);
                Assert.That(
                    await eventsHandle.JsonValueAsync<string[]>().ConfigureAwait(false),
                    Is.EqualTo(new[]
                    {
                        "pointerover", "pointerenter",
                        "pointerdown", "touchstart",
                        "pointerup", "pointerout",
                        "pointerleave", "touchend",
                    }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should not send mouse events when touchend is canceled")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotSendMouseEventsWhenTouchendIsCanceled()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("<div style=\"width: 50px; height: 50px; background: red\">").ConfigureAwait(false);
                await page.EvaluateAsync(@"() => {
    document.addEventListener('touchend', t => t.preventDefault());
  }").ConfigureAwait(false);
                IJSHandle eventsHandle = await TrackEventsAsync(await page.QuerySelectorAsync("div").ConfigureAwait(false)).ConfigureAwait(false);
                await page.TapAsync("div").ConfigureAwait(false);
                Assert.That(
                    await eventsHandle.JsonValueAsync<string[]>().ConfigureAwait(false),
                    Is.EqualTo(new[]
                    {
                        "pointerover", "pointerenter",
                        "pointerdown", "touchstart",
                        "pointerup", "pointerout",
                        "pointerleave", "touchend",
                    }));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should not wait for a navigation caused by a tap")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotWaitForANavigationCausedByATap()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            await WithPageAsync(async page =>
            {
                await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
                await page.SetContentAsync("<a href=\"/intercept-this.html\">link</a>;").ConfigureAwait(false);
                TaskCompletionSource<bool> intercepted = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                Server.SetRoute("/intercept-this.html", _ =>
                {
                    intercepted.TrySetResult(true);
                    return new TaskCompletionSource().Task;
                });
                await Task.WhenAll(intercepted.Task, page.TapAsync("a")).ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should work with modifiers")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithModifiers()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync("hello world").ConfigureAwait(false);
                Task<bool> altKeyPromise = page.EvaluateAsync<bool>(@"() => new Promise(resolve => {
    document.addEventListener('touchstart', event => {
      resolve(event.altKey);
    }, { passive: false });
  })");

                // make sure the evals hit the page
                await page.EvaluateAsync("() => void 0").ConfigureAwait(false);
                await page.TapAsync("body", new() { Modifiers = new[] { KeyboardModifier.Alt } }).ConfigureAwait(false);
                Assert.That(await altKeyPromise.ConfigureAwait(false), Is.True);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should send well formed touch points")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSendWellFormedTouchPoints()
        {
            await WithPageAsync(async page =>
            {
                const string touchSnapshot = @"() => new Promise(resolve => {
      document.addEventListener('EVENT_NAME', event => {
        resolve([...event.touches].map(t => ({
          identifier: t.identifier,
          clientX: t.clientX,
          clientY: t.clientY,
          pageX: t.pageX,
          pageY: t.pageY,
          radiusX: 'radiusX' in t ? t.radiusX : t['webkitRadiusX'],
          radiusY: 'radiusY' in t ? t.radiusY : t['webkitRadiusY'],
          rotationAngle: 'rotationAngle' in t ? t.rotationAngle : t['webkitRotationAngle'],
          force: 'force' in t ? t.force : t['webkitForce'],
        })));
      }, false);
    })";
                Task<TouchPointSnapshot[]> startTask = page.EvaluateAsync<TouchPointSnapshot[]>(
                    touchSnapshot.Replace("EVENT_NAME", "touchstart", StringComparison.Ordinal));
                Task<TouchPointSnapshot[]> endTask = page.EvaluateAsync<TouchPointSnapshot[]>(
                    touchSnapshot.Replace("EVENT_NAME", "touchend", StringComparison.Ordinal));

                // make sure the evals hit the page
                await page.EvaluateAsync("() => void 0").ConfigureAwait(false);
                await page.Touchscreen.TapAsync(40, 60).ConfigureAwait(false);
                TouchPointSnapshot[] touchstart = await startTask.ConfigureAwait(false);
                TouchPointSnapshot[] touchend = await endTask.ConfigureAwait(false);

                Assert.That(touchstart, Has.Length.EqualTo(1));
                Assert.That(touchstart[0].ClientX, Is.EqualTo(40));
                Assert.That(touchstart[0].ClientY, Is.EqualTo(60));
                Assert.That(touchstart[0].Force, Is.EqualTo(1));
                Assert.That(touchstart[0].Identifier, Is.EqualTo(0));
                Assert.That(touchstart[0].PageX, Is.EqualTo(40));
                Assert.That(touchstart[0].PageY, Is.EqualTo(60));
                Assert.That(touchstart[0].RadiusX, Is.EqualTo(1));
                Assert.That(touchstart[0].RadiusY, Is.EqualTo(1));
                Assert.That(touchstart[0].RotationAngle, Is.EqualTo(0));
                Assert.That(touchend, Is.Empty);
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should wait until an element is visible to tap it")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWaitUntilAnElementIsVisibleToTapIt()
        {
            await WithPageAsync(async page =>
            {
                IJSHandle div = await page.EvaluateHandleAsync(@"() => {
    const button = document.createElement('button');
    button.textContent = 'not clicked';
    document.body.appendChild(button);
    button.style.display = 'none';
    return button;
  }").ConfigureAwait(false);
                IElementHandle button = div.AsElement();
                Task tapPromise = button.TapAsync();
                await button.EvaluateAsync("div => div.onclick = () => div.textContent = 'clicked'").ConfigureAwait(false);
                await button.EvaluateAsync("div => div.style.display = 'block'").ConfigureAwait(false);
                await tapPromise.ConfigureAwait(false);
                Assert.That(await button.TextContentAsync().ConfigureAwait(false), Is.EqualTo("clicked"));
            }).ConfigureAwait(false);
        }

        [PlaywrightTest("tap.spec.ts", "should send all of the correct events")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task LocatorsShouldSendAllOfTheCorrectEvents()
        {
            await WithPageAsync(async page =>
            {
                await page.SetContentAsync(@"
      <div id=""a"" style=""background: lightblue; width: 50px; height: 50px"">a</div>
      <div id=""b"" style=""background: pink; width: 50px; height: 50px"">b</div>
    ").ConfigureAwait(false);
                await page.Locator("#a").TapAsync().ConfigureAwait(false);
                await page.Locator("#b").TapAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);
        }

        private static async Task WithPageAsync(Func<IPage, Task> body)
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HasTouch = true }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await body(page).ConfigureAwait(false);
        }

        private static Task<IJSHandle> TrackEventsAsync(IElementHandle target)
        {
            return target.EvaluateHandleAsync(@"target => {
    const events = [];
    for (const event of [
      'mousedown', 'mouseenter', 'mouseleave', 'mousemove', 'mouseout', 'mouseover', 'mouseup', 'click',
      'pointercancel', 'pointerdown', 'pointerenter', 'pointerleave', 'pointermove', 'pointerout', 'pointerover', 'pointerup',
      'touchstart', 'touchend', 'touchmove', 'touchcancel',
    ])
      target.addEventListener(event, () => events.push(event), false);
    return events;
  }");
        }

        private sealed class TouchPointSnapshot
        {
            [JsonPropertyName("identifier")]
            public int Identifier { get; set; }

            [JsonPropertyName("clientX")]
            public double ClientX { get; set; }

            [JsonPropertyName("clientY")]
            public double ClientY { get; set; }

            [JsonPropertyName("pageX")]
            public double PageX { get; set; }

            [JsonPropertyName("pageY")]
            public double PageY { get; set; }

            [JsonPropertyName("radiusX")]
            public double RadiusX { get; set; }

            [JsonPropertyName("radiusY")]
            public double RadiusY { get; set; }

            [JsonPropertyName("rotationAngle")]
            public double RotationAngle { get; set; }

            [JsonPropertyName("force")]
            public double Force { get; set; }
        }
    }
}
