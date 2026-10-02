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
    }
}
