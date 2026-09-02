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
/// Fresh <see cref="IPage"/> per test. Mirrors
/// <see cref="Microsoft.Playwright.NUnit.PageTest"/>.
/// </summary>
public class PageTest : ContextTest
{
    /// <summary>
    /// Gets the page created for the current test.
    /// </summary>
    public IPage Page { get; private set; } = null!;

    /// <summary>
    /// Creates <see cref="Page"/> on <see cref="ContextTest.Context"/>.
    /// </summary>
    [SetUp]
    public async Task PageSetup()
    {
        Page = await Context.NewPageAsync().ConfigureAwait(false);
    }
}
