/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
 */
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;

namespace PlaywrightSharp.NUnit;

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
