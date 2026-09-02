/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
