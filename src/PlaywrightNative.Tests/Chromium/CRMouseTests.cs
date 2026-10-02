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
    /// Integration tests for <see cref="PlaywrightNative.Input.Mouse"/>.
    /// Uses simple inline HTML so tests are self-contained and don't depend on
    /// complex external fixtures.
    /// </summary>
    [TestFixture]
    public class CRMouseTests : CRTestBase
    {
        [PlaywrightTest("page-mouse.spec.ts", "should click the document @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldClickTheDocument()
        {
            await Page.GoToAsync("data:text/html,<script>window.clickCount = 0; document.addEventListener('click', () => window.clickCount++);</script>").ConfigureAwait(false);

            await Page.Mouse.ClickAsync(50, 60).ConfigureAwait(false);

            int count = await Page.EvaluateAsync<int>("window.clickCount").ConfigureAwait(false);
            Assert.That(count, Is.EqualTo(1));
        }
    }
}
