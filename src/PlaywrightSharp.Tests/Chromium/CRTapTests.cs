/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.Chromium;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="CRElementHandle.TapAsync"/>.
    /// </summary>
    [TestFixture]
    public class CRTapTests : CRTestBase
    {
        [PlaywrightTest("tap.spec.ts", "should fire touch start and touch end")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireTouchStartAndTouchEnd()
        {
            await Page.GoToAsync(@"data:text/html,<div id='t' style='position:absolute;left:20px;top:20px;width:80px;height:80px'>tap</div>
                <script>
                window.events = [];
                const t = document.getElementById('t');
                t.addEventListener('touchstart', () => window.events.push('touchstart'));
                t.addEventListener('touchend', () => window.events.push('touchend'));
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            await handle.TapAsync().ConfigureAwait(false);

            string json = await Page.EvaluateAsync<string>("JSON.stringify(window.events)").ConfigureAwait(false);
            Assert.That(json, Is.EqualTo("[\"touchstart\",\"touchend\"]"));
        }

        [PlaywrightTest("tap.spec.ts", "should fire click on simple button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFireClickOnSimpleButton()
        {
            // With touch events on a plain button, browsers synthesize a click.
            await Page.GoToAsync(@"data:text/html,<button id='b' style='position:absolute;left:20px;top:20px;width:80px;height:40px'>tap me</button>
                <script>
                window.clicked = false;
                document.getElementById('b').addEventListener('click', () => window.clicked = true);
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#b").ConfigureAwait(false);
            await handle.TapAsync().ConfigureAwait(false);

            bool clicked = await Page.EvaluateAsync<bool>("window.clicked").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }

        [PlaywrightTest("tap.spec.ts", "should throw for invisible element")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowForInvisibleElement()
        {
            await Page.GoToAsync("data:text/html,<div id='t' style='display:none'>hidden</div>").ConfigureAwait(false);

            CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            PlaywrightSharpException ex = Assert.ThrowsAsync<PlaywrightSharpException>(
                () => handle.TapAsync());
            Assert.That(ex.Message, Does.Contain("no layout").Or.Contain("not visible"));

            await handle.DisposeAsync().ConfigureAwait(false);
        }
    }
}
