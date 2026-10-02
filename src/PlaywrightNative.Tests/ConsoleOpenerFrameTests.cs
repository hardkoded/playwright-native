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
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Console, pageerror, opener, input setters, and Chromium frame surfaces.
    /// </summary>
    [TestFixture]
    public class ConsoleOpenerFrameTests : PageTestEx
    {
        [PlaywrightTest("page-event-console.spec.ts", "should work")]
        [Test]
        [Timeout(30_000)]
        public async Task ConsoleEventShouldFireForLog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<IConsoleMessage> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.Console += (_, message) => tcs.TrySetResult(message);

            await page.GoToAsync("about:blank").ConfigureAwait(false);
            await page.EvaluateAsync<object>("console.log('hello-wave-42')").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            IConsoleMessage received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Is.Not.Null);
            Assert.That(received.Page, Is.SameAs(page));
            Assert.That(received.Text, Does.Contain("hello-wave-42"));
            Assert.That(received.Type, Is.EqualTo("log"));
        }

        [PlaywrightTest("page-event-pageerror.spec.ts", "should fire")]
        [Test]
        [Timeout(30_000)]
        public async Task PageErrorShouldFireForUncaughtException()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            TaskCompletionSource<string> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            page.PageError += (_, error) => tcs.TrySetResult(error.ToString());

            await page.GoToAsync("data:text/html,<script>throw new Error('boom-wave-42');</script>").ConfigureAwait(false);

            using CancellationTokenSource cts = new(5_000);
            cts.Token.Register(() => tcs.TrySetCanceled());
            string received = await tcs.Task.ConfigureAwait(false);

            Assert.That(received, Does.Contain("boom-wave-42"));
        }
        private static async Task<IFrame> WaitForChildFrameAsync(IPage page)
        {
            IFrame child = null;
            for (int i = 0; i < 50 && child == null; i++)
            {
                foreach (IFrame frame in page.MainFrame.ChildFrames)
                {
                    child = frame;
                    break;
                }

                if (child != null)
                {
                    break;
                }

                await Task.Delay(100).ConfigureAwait(false);
            }

            return child;
        }
    }
}
