/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy browser wait helpers.
    /// </summary>
    public static class BrowserWaitCompatExtensions
    {
        /// <summary>Wait for the next browser context.</summary>
        public static Task<IBrowserContext> WaitForContextAsync(this IBrowser browser, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IBrowserContext>(
                h => browser.Context += h,
                h => browser.Context -= h,
                _ => true,
                timeout,
                "browser.waitForEvent",
                waitForEventName: "context");

        /// <summary>Wait for the browser disconnected event.</summary>
        public static Task<IBrowser> WaitForDisconnectedAsync(this IBrowser browser, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IBrowser>(
                h => browser.Disconnected += h,
                h => browser.Disconnected -= h,
                _ => true,
                timeout,
                "browser.waitForEvent",
                waitForEventName: "disconnected");
    }
}
