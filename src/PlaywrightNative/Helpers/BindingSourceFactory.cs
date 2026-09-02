// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Reflection;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Creates <see cref="Microsoft.Playwright.BindingSource"/> instances.
    /// Official type only exposes an internal constructor.
    /// </summary>
    internal static class BindingSourceFactory
    {
        private static readonly ConstructorInfo Ctor = typeof(Microsoft.Playwright.BindingSource)
            .GetConstructor(
                BindingFlags.Instance | BindingFlags.NonPublic,
                binder: null,
                types: new[] { typeof(Microsoft.Playwright.IBrowserContext), typeof(Microsoft.Playwright.IPage), typeof(Microsoft.Playwright.IFrame) },
                modifiers: null)
            ?? throw new InvalidOperationException("Microsoft.Playwright.BindingSource internal constructor not found.");

        public static Microsoft.Playwright.BindingSource Create(
            Microsoft.Playwright.IBrowserContext context,
            Microsoft.Playwright.IPage page,
            Microsoft.Playwright.IFrame frame)
            => (Microsoft.Playwright.BindingSource)Ctor.Invoke(new object[] { context, page, frame });
    }
}
