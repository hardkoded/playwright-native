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
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="CRElementHandle.FillAsync"/> against input and textarea elements.
    /// </summary>
    [TestFixture]
    public class CRFillTests : CRTestBase
    {
        [PlaywrightTest("page-fill.spec.ts", "should fill textarea @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldFillTextarea()
        {
            await Page.GoToAsync("data:text/html,<textarea id='ta'></textarea>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#ta").ConfigureAwait(false);
            await handle.FillAsync("multi\nline").ConfigureAwait(false);

            string value = await Page.EvaluateAsync<string>("document.querySelector('#ta').value").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo("multi\nline"));
        }
    }
}
