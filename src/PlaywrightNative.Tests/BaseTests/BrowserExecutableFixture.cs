// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Prefetches browser binaries once per test assembly via
    /// <see cref="BrowserExecutable"/> in the PlaywrightNative.NUnit package.
    /// </summary>
    [SetUpFixture]
    public class BrowserExecutableAssemblySetup : BrowserExecutableFixture
    {
        [OneTimeSetUp]
        public Task OneTimeSetUpAsync() => ResolveAsync();
    }
}
