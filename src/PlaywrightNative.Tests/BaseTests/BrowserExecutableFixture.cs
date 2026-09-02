// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
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
