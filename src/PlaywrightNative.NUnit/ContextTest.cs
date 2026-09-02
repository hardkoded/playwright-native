/*
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
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

namespace PlaywrightNative.NUnit;

/// <summary>
/// Fresh <see cref="IBrowserContext"/> per test. Mirrors
/// <see cref="Microsoft.Playwright.NUnit.ContextTest"/>.
/// </summary>
public class ContextTest : BrowserTest
{
    /// <summary>
    /// Gets the context created for the current test.
    /// </summary>
    public IBrowserContext Context { get; private set; } = null!;

    /// <summary>
    /// Creates <see cref="Context"/> from <see cref="ContextOptions"/>.
    /// </summary>
    [SetUp]
    public async Task ContextSetup()
    {
        Context = await NewContext(ContextOptions()).ConfigureAwait(false);
    }

    /// <summary>
    /// Default context options. Override in fixtures that need touch, HTTPS errors, etc.
    /// </summary>
    /// <returns>Options passed to <see cref="BrowserTest.NewContext(BrowserContextOptions)"/>.</returns>
    public virtual BrowserContextOptions ContextOptions()
    {
        return new BrowserContextOptions
        {
            Locale = "en-US",
            ColorScheme = ColorScheme.Light,
        };
    }
}
