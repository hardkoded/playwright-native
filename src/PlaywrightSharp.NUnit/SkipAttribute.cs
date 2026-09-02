/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
 */
using System;
using System.Linq;
using System.Runtime.InteropServices;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;

namespace PlaywrightSharp.NUnit;

/// <summary>
/// Skips tests by browser × OS combination. Extends
/// <see cref="Microsoft.Playwright.NUnit.SkipAttribute"/> and re-evaluates
/// using PlaywrightSharp's <c>PRODUCT</c> (or <c>BROWSER</c>) environment variable.
/// </summary>
public class SkipAttribute : Microsoft.Playwright.NUnit.SkipAttribute, IApplyToTest
{
    private readonly Targets[] _combinations;

    /// <summary>
    /// Skip target flags (browser and/or OS).
    /// </summary>
    [Flags]
    public new enum Targets : short
    {
        /// <summary>Windows.</summary>
        Windows = 1 << 0,

        /// <summary>Linux.</summary>
        Linux = 1 << 1,

        /// <summary>macOS.</summary>
        OSX = 1 << 2,

        /// <summary>Chromium.</summary>
        Chromium = 1 << 3,

        /// <summary>Firefox.</summary>
        Firefox = 1 << 4,

        /// <summary>WebKit.</summary>
        Webkit = 1 << 5,
    }

    /// <summary>
    /// Skips the combinations provided.
    /// </summary>
    /// <param name="combinations">AND within flags, OR across arguments.</param>
    public SkipAttribute(params Targets[] combinations)
        : base(Array.Empty<Microsoft.Playwright.NUnit.SkipAttribute.Targets>())
    {
        _combinations = combinations;
    }

    /// <inheritdoc />
    void IApplyToTest.ApplyToTest(Test test)
    {
        string browserName = ResolveBrowserName();
        if (_combinations.Any(combination =>
        {
            Targets[] requirements = (Enum.GetValues(typeof(Targets)) as Targets[])!
                .Where(x => combination.HasFlag(x))
                .ToArray();
            return requirements.All(flag =>
                flag switch
                {
                    Targets.Windows => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows),
                    Targets.Linux => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Linux),
                    Targets.OSX => RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX),
                    Targets.Chromium => browserName == "chromium",
                    Targets.Firefox => browserName == "firefox",
                    Targets.Webkit => browserName == "webkit",
                    _ => false,
                });
        }))
        {
            test.RunState = RunState.Ignored;
            test.Properties.Set(PropertyNames.SkipReason, "Skipped by browser/platform");
        }
    }

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
        return string.IsNullOrEmpty(browser) ? "chromium" : browser.Trim().ToLowerInvariant();
    }
}
