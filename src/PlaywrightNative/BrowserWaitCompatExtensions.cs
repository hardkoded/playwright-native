/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
