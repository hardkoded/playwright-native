/*
 * Copyright (c) Microsoft Corporation.
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
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/webmcp-disabled.spec.ts</c> parity. Uses the shared
    /// browser, which is launched without WebMCP.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryWebmcpDisabledParityTests : PageTestEx
    {
        [SetUp]
        public void SkipWebKit()
        {
            if (TestConstants.IsWebKit)
            {
                Assert.Ignore("WebKit does not implement WebMCP");
            }
        }

        [PlaywrightTest("webmcp-disabled.spec.ts", "should throw when the browser was launched without WebMCP")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldThrowWhenTheBrowserWasLaunchedWithoutWebMCP()
        {
            Exception error = Assert.CatchAsync(() => Page.Webmcp().ToolsAsync());
            Assert.That(error.Message, Does.Contain("WebMCP is not enabled."));
            Assert.That(error.Message, Does.Contain(TestConstants.IsFirefox ? "dom.modelcontext.enabled" : "--enable-features=WebMCP"));
        }
    }
}
