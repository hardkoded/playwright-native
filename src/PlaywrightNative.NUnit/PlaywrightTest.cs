/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
 */
using System;
using Microsoft.Playwright;
using NUnit.Framework;

namespace PlaywrightNative.NUnit;

/// <summary>
/// PlaywrightNative NUnit fixture that selects the browser product and exposes
/// <see cref="Expect(ILocator)"/> helpers. Extends <see cref="WorkerAwareTest"/>
/// (and thereby <see cref="Microsoft.Playwright.NUnit.WorkerAwareTest"/>).
/// </summary>
/// <remarks>
/// Unlike <see cref="Microsoft.Playwright.NUnit.PlaywrightTest"/>, this type does
/// not call <c>Microsoft.Playwright.Playwright.CreateAsync()</c> — PlaywrightNative
/// launches browsers through <see cref="PlaywrightNative.Playwright"/> directly.
/// </remarks>
public class PlaywrightTest : WorkerAwareTest
{
    /// <summary>
    /// Gets the browser name for this run (<c>chromium</c>, <c>firefox</c>, or <c>webkit</c>).
    /// </summary>
    public string BrowserName { get; private set; } = null!;

    /// <summary>
    /// Gets the PlaywrightNative <see cref="IBrowserType"/> for <see cref="BrowserName"/>.
    /// </summary>
    public IBrowserType BrowserType { get; private set; } = null!;

    /// <summary>
    /// Resolves <see cref="BrowserName"/> / <see cref="BrowserType"/> from
    /// <c>PRODUCT</c> or <c>BROWSER</c> environment variables.
    /// </summary>
    [SetUp]
    public void PlaywrightSetup()
    {
        BrowserName = ResolveBrowserName();
        BrowserType = BrowserName switch
        {
            "firefox" => PlaywrightNative.Playwright.Firefox,
            "webkit" => PlaywrightNative.Playwright.Webkit,
            _ => PlaywrightNative.Playwright.Chromium,
        };

        string testIdAttribute = Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_ID_ATTRIBUTE");
        if (!string.IsNullOrEmpty(testIdAttribute))
        {
            PlaywrightNative.Playwright.SetTestIdAttribute(testIdAttribute);
        }
    }

    /// <summary>
    /// Sets the default timeout used by <see cref="Expect(ILocator)"/> assertions.
    /// </summary>
    /// <param name="timeout">Timeout in milliseconds.</param>
    public static void SetDefaultExpectTimeout(float timeout)
        => PlaywrightNative.Assertions.SetDefaultExpectTimeout(timeout);

    /// <summary>Creates locator assertions.</summary>
    public ILocatorAssertions Expect(ILocator locator) => PlaywrightNative.Assertions.Expect(locator);

    /// <summary>Creates page assertions.</summary>
    public IPageAssertions Expect(IPage page) => PlaywrightNative.Assertions.Expect(page);

    /// <summary>Creates API response assertions.</summary>
    public IAPIResponseAssertions Expect(IAPIResponse response) => PlaywrightNative.Assertions.Expect(response);

    private static string ResolveBrowserName()
    {
        string product = Environment.GetEnvironmentVariable("PRODUCT");
        if (!string.IsNullOrEmpty(product))
        {
            if (product.Equals("FIREFOX", StringComparison.OrdinalIgnoreCase))
            {
                return "firefox";
            }

            if (product.Equals("WEBKIT", StringComparison.OrdinalIgnoreCase))
            {
                return "webkit";
            }

            return "chromium";
        }

        string browser = Environment.GetEnvironmentVariable("BROWSER");
        if (!string.IsNullOrEmpty(browser))
        {
            return browser.Trim().ToLowerInvariant();
        }

        return "chromium";
    }
}
