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
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared waiter for <see cref="IPage.WaitForFileChooserAsync(float?, CancellationToken)"/>. Resolves
    /// the page default timeout when no per-call timeout is passed, and honors
    /// an optional <see cref="CancellationToken"/> (Node <c>signal</c>).
    /// </summary>
    internal static class FileChooserWaitHelper
    {
        /// <summary>
        /// Waits for the next <see cref="IPage.FileChooser"/> event on
        /// <paramref name="page"/>.
        /// </summary>
        /// <param name="page">The page that raises the event.</param>
        /// <param name="timeout">
        /// Timeout in milliseconds. When omitted, <see cref="IPage.DefaultTimeout"/>
        /// is used. Pass <c>0</c> to disable the timeout.
        /// </param>
        /// <param name="cancellationToken">Cancels the wait (Node <c>signal</c>).</param>
        /// <returns>The file chooser that opened.</returns>
        internal static async Task<IFileChooser> WaitAsync(
            IPage page,
            float? timeout = default,
            CancellationToken cancellationToken = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            await EnsureInterceptAsync(page).ConfigureAwait(false);
            return await WaitForEventHelper.WaitAsync<IFileChooser>(
                h => page.FileChooser += h,
                h => page.FileChooser -= h,
                _ => true,
                timeout ?? page.DefaultTimeout(),
                "page.waitForFileChooser",
                cancellationToken: cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// Raises a file chooser without surfacing resolve failures when the
        /// input's frame is detached immediately after <c>click()</c>.
        /// </summary>
        /// <param name="raiseAsync">Creates and raises the chooser.</param>
        /// <returns>A task that completes when raise finishes or is swallowed.</returns>
        internal static async Task RaiseSafelyAsync(Func<Task> raiseAsync)
        {
            if (raiseAsync == null)
            {
                throw new ArgumentNullException(nameof(raiseAsync));
            }

            try
            {
                await raiseAsync().ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
            catch (InvalidOperationException)
            {
            }
        }

        private static Task EnsureInterceptAsync(IPage page)
        {
            if (page is Page chromium)
            {
                return chromium.CrPage.Session.SendAsync(
                    "Page.setInterceptFileChooserDialog",
                    new { enabled = true });
            }

            if (page is WebKit.WKPage webkit)
            {
                WebKit.WKTargetSession target = webkit.CurrentTargetSession;
                if (target == null)
                {
                    return Task.CompletedTask;
                }

                return target.SendAsync(
                    "Page.setInterceptFileChooserDialog",
                    new { enabled = true });
            }

            return Task.CompletedTask;
        }
    }
}
