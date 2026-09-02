/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
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
