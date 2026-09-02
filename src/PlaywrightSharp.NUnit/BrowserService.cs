/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
 */
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace PlaywrightSharp.NUnit;

/// <summary>
/// Worker service that owns one PlaywrightSharp <see cref="IBrowser"/>.
/// </summary>
internal sealed class BrowserService : IWorkerService
{
    private BrowserService(IBrowser browser)
    {
        Browser = browser;
    }

    public IBrowser Browser { get; }

    public static Task<BrowserService> Register(
        WorkerAwareTest test,
        string browserName,
        BrowserTypeLaunchOptions launchOptions)
    {
        return test.RegisterService(
            "Browser",
            async () => new BrowserService(await CreateBrowserAsync(browserName, launchOptions).ConfigureAwait(false)));
    }

    private static async Task<IBrowser> CreateBrowserAsync(string browserName, BrowserTypeLaunchOptions launchOptions)
    {
        BrowserTypeLaunchOptions options = launchOptions ?? new BrowserTypeLaunchOptions();
        return browserName switch
        {
            "firefox" => await PlaywrightSharp.Playwright.LaunchFirefoxAsync(options).ConfigureAwait(false),
            "webkit" => await PlaywrightSharp.Playwright.LaunchWebkitAsync(options).ConfigureAwait(false),
            _ => await PlaywrightSharp.Playwright.LaunchChromiumAsync(options).ConfigureAwait(false),
        };
    }

    public Task ResetAsync() => Task.CompletedTask;

    public Task DisposeAsync() => Browser.CloseAsync();
}
