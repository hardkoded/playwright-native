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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using CRViewport = PlaywrightNative.Input.ViewportSize;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for CDP-backed emulation: viewport, color scheme, user agent.
    /// </summary>
    [TestFixture]
    public class CREmulationTests : CRTestBase
    {
        [PlaywrightTest("emulation-focus.spec.ts", "Set viewport should change inner dimensions")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetViewportShouldChangeInnerDimensions()
        {
            await Page.SetViewportSizeAsync(new CRViewport(500, 400)).ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            int width = await Page.EvaluateAsync<int>("window.innerWidth").ConfigureAwait(false);
            int height = await Page.EvaluateAsync<int>("window.innerHeight").ConfigureAwait(false);
            Assert.That(width, Is.EqualTo(500));
            Assert.That(height, Is.EqualTo(400));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "Set viewport with scale factor should affect device pixel ratio")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetViewportWithScaleFactorShouldAffectDevicePixelRatio()
        {
            await Page.SetViewportSizeAsync(new CRViewport(400, 300), deviceScaleFactor: 2.0).ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            double dpr = await Page.EvaluateAsync<double>("window.devicePixelRatio").ConfigureAwait(false);
            Assert.That(dpr, Is.EqualTo(2.0));
        }

        [PlaywrightTest("emulation-focus.spec.ts", "Set color scheme to light should match media query")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetColorSchemeToLightShouldMatchMediaQuery()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.SetColorSchemeAsync("light").ConfigureAwait(false);

            bool matches = await Page.EvaluateAsync<bool>(
                "matchMedia('(prefers-color-scheme: light)').matches").ConfigureAwait(false);
            Assert.That(matches, Is.True);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "Set color scheme to dark should match media query")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetColorSchemeToDarkShouldMatchMediaQuery()
        {
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.SetColorSchemeAsync("dark").ConfigureAwait(false);

            bool matches = await Page.EvaluateAsync<bool>(
                "matchMedia('(prefers-color-scheme: dark)').matches").ConfigureAwait(false);
            Assert.That(matches, Is.True);
        }

        [PlaywrightTest("emulation-focus.spec.ts", "Set user agent should affect navigator user agent")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SetUserAgentShouldAffectNavigatorUserAgent()
        {
            await Page.SetUserAgentAsync("MyBot/1.0").ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string ua = await Page.EvaluateAsync<string>("navigator.userAgent").ConfigureAwait(false);
            Assert.That(ua, Is.EqualTo("MyBot/1.0"));
        }
    }
}
