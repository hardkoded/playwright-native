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

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.AddInitScriptAsync</c> and
    /// <c>CRPage.RemoveInitScriptAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRInitScriptTests : CRTestBase
    {
        [PlaywrightTest("page-add-init-script.spec.ts", "should run on new document")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRunOnNewDocument()
        {
            await Page.AddInitScriptAsync("window.__marker = 'ran';").ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string v = await Page.EvaluateAsync<string>("window.__marker").ConfigureAwait(false);
            Assert.That(v, Is.EqualTo("ran"));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should survive navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveNavigation()
        {
            await Page.AddInitScriptAsync("window.__count = (window.__count || 0) + 1;").ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            await Page.GoToAsync("data:text/html,<div>second</div>").ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.__count").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should support multiple scripts")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSupportMultipleScripts()
        {
            await Page.AddInitScriptAsync("window.__a = 'first';").ConfigureAwait(false);
            await Page.AddInitScriptAsync("window.__b = 'second';").ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string a = await Page.EvaluateAsync<string>("window.__a").ConfigureAwait(false);
            string b = await Page.EvaluateAsync<string>("window.__b").ConfigureAwait(false);
            Assert.That(a, Is.EqualTo("first"));
            Assert.That(b, Is.EqualTo("second"));
        }

        [PlaywrightTest("page-add-init-script.spec.ts", "should remove registered script")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldRemoveRegisteredScript()
        {
            string id = await Page.AddInitScriptAsync("window.__removable = true;").ConfigureAwait(false);
            Assert.That(id, Is.Not.Null.And.Not.Empty);

            await Page.RemoveInitScriptAsync(id).ConfigureAwait(false);
            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            bool present = await Page.EvaluateAsync<bool>(
                "typeof window.__removable !== 'undefined'").ConfigureAwait(false);
            Assert.That(present, Is.False);
        }
    }
}
