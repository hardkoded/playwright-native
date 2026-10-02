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
using System.Text.Json;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <c>CRPage.ExposeFunctionAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRExposeFunctionTests : CRTestBase
    {
        [PlaywrightTest("page-expose-function.spec.ts", "should survive navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldSurviveNavigation()
        {
            await Page.ExposeFunctionAsync("persistent", _ => Task.FromResult<object>("still-here")).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);

            string result = await Page.EvaluateAsync<string>("window.persistent()").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("still-here"));
        }
    }
}
