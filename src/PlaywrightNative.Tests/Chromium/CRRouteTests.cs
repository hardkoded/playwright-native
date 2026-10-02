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
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    [TestFixture]
    public class CRRouteTests : CRTestBase
    {
        [PlaywrightTest("page-route.spec.ts", "should intercept @smoke")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldIntercept()
        {
            bool intercepted = false;

            Server.SetRoute("/intercept.html", context =>
            {
                context.Response.ContentType = "text/html";
                return context.Response.WriteAsync("<html><body>intercepted</body></html>");
            });

            await Page.RouteAsync("**/intercept.html", async route =>
            {
                intercepted = true;
                await route.ContinueAsync().ConfigureAwait(false);
            }).ConfigureAwait(false);

            await Page.GoToAsync(TestConstants.ServerUrl + "/intercept.html").ConfigureAwait(false);

            Assert.That(intercepted, Is.True);
        }
    }
}
