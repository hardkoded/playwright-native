// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Prefetches browser binaries once per test assembly via
    /// <see cref="BrowserExecutable"/> in the PlaywrightSharp.NUnit package.
    /// </summary>
    [SetUpFixture]
    public class BrowserExecutableAssemblySetup : BrowserExecutableFixture
    {
        [OneTimeSetUp]
        public Task OneTimeSetUpAsync() => ResolveAsync();
    }
}
