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
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.Transport;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="BrowserTypeLaunchOptions.Env"/>.
    /// </summary>
    [TestFixture]
    public class LaunchEnvTests : PageTestEx
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "BrowserProcessManager applies Env")]
        [Test]
        public void BrowserProcessManagerShouldApplyEnvironmentVariables()
        {
            Dictionary<string, string> env = new()
            {
                ["PLAYWRIGHT_SHARP_WAVE424"] = "1",
            };

            using BrowserProcessManager manager = new(
                "/bin/true",
                Array.Empty<string>(),
                environment: env);
            Assert.That(manager.Process.StartInfo.Environment["PLAYWRIGHT_SHARP_WAVE424"], Is.EqualTo("1"));
        }
    }
}
