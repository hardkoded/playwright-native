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
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using Microsoft.Playwright.NUnit;

namespace PlaywrightNative.NUnit;

/// <summary>
/// Worker service that owns one PlaywrightNative <see cref="IBrowser"/>.
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
            "firefox" => await PlaywrightNative.Playwright.LaunchFirefoxAsync(options).ConfigureAwait(false),
            "webkit" => await PlaywrightNative.Playwright.LaunchWebkitAsync(options).ConfigureAwait(false),
            _ => await PlaywrightNative.Playwright.LaunchChromiumAsync(options).ConfigureAwait(false),
        };
    }

    public Task ResetAsync() => Task.CompletedTask;

    public async Task DisposeAsync()
    {
        // Prefer full disposal so pipe transports / process handles are released.
        // CloseAsync alone historically left AnonymousPipeServerStream FDs open,
        // and WorkerAwareTest disposes the browser after every failed test.
        if (Browser is IAsyncDisposable asyncDisposable)
        {
            await asyncDisposable.DisposeAsync().ConfigureAwait(false);
            return;
        }

        await Browser.CloseAsync().ConfigureAwait(false);
    }
}
