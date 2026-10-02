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
using PlaywrightNative.Input;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Click tests against the <c>/input/button.html</c> fixture. Each test navigates to the
    /// fixture, resolves the button center via JS, clicks it, and verifies <c>window.result</c>.
    /// </summary>
    [TestFixture]
    public class CRClickTests : CRTestBase
    {
        private async Task<(double X, double Y)> GetButtonCenterAsync()
        {
            double x = await Page.EvaluateAsync<double>("(() => { const r = document.querySelector('button').getBoundingClientRect(); return r.x + r.width / 2; })()").ConfigureAwait(false);
            double y = await Page.EvaluateAsync<double>("(() => { const r = document.querySelector('button').getBoundingClientRect(); return r.y + r.height / 2; })()").ConfigureAwait(false);
            return (x, y);
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButton()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }

        [PlaywrightTest("page-click.spec.ts", "should click the button after navigation ")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheButtonAfterNavigation()
        {
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.ServerUrl + "/input/button.html").ConfigureAwait(false);
            (double x, double y) = await GetButtonCenterAsync().ConfigureAwait(false);

            await Page.Mouse.ClickAsync(x, y).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.result").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Clicked"));
        }
    }
}
