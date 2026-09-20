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
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Retain/release gate that stays open until scheduled main-frame
    /// navigations commit. Mirrors upstream <c>SignalBarrier</c>.
    /// </summary>
    internal sealed class ActionSignalBarrier
    {
        private readonly object _lock = new object();
        private TaskCompletionSource<bool> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _protectCount;

        // Policy-check / scheduled navigations — cancelled by didCheck abort.
        private int _pendingPolicyNavigations;

        // Document Network requests — only cleared by commit. A late didCheck
        // cancel must not drop the retain for a real form GET
        // (ShouldWorkWithGotoFollowingClick under suite load).
        private int _pendingDocumentNavigations;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionSignalBarrier"/> class.
        /// Starts retained so <see cref="WaitForAsync"/> can drop the last hold.
        /// </summary>
        internal ActionSignalBarrier()
        {
            lock (_lock)
            {
                RetainUnderLock();
            }
        }

        /// <summary>
        /// Gets a value indicating whether a main-frame navigation retain is open.
        /// Used to end the post-action poll as soon as policy-check / request
        /// signals arrive instead of burning the full WebKit empty-poll budget.
        /// </summary>
        internal bool HasPendingNavigations
        {
            get
            {
                lock (_lock)
                {
                    return _pendingPolicyNavigations > 0 || _pendingDocumentNavigations > 0;
                }
            }
        }

        /// <summary>
        /// Records that a main-frame navigation was requested and waits for
        /// the matching commit.
        /// </summary>
        /// <param name="fromDocumentRequest">
        /// When <see langword="true"/>, the retain is only released by commit
        /// (not by <see cref="OnNavigationAborted"/>).
        /// </param>
        internal void ExpectMainFrameNavigation(bool fromDocumentRequest = false)
        {
            lock (_lock)
            {
                if (fromDocumentRequest)
                {
                    _pendingDocumentNavigations++;
                }
                else
                {
                    _pendingPolicyNavigations++;
                }

                RetainUnderLock();
            }
        }

        /// <summary>
        /// Releases every pending navigation retain when the main frame commits.
        /// Multiple signals (policy check + document request) describe one navigation.
        /// </summary>
        internal void OnMainFrameNavigated()
        {
            lock (_lock)
            {
                int pending = _pendingPolicyNavigations + _pendingDocumentNavigations;
                _pendingPolicyNavigations = 0;
                _pendingDocumentNavigations = 0;
                for (int i = 0; i < pending; i++)
                {
                    ReleaseUnderLock();
                }
            }
        }

        /// <summary>
        /// Releases a single policy-check retain when a navigation is cancelled.
        /// Must not clear document-request retains (a cancelled speculative
        /// willCheck must not drop the retain for the real form GET).
        /// </summary>
        internal void OnNavigationAborted()
        {
            lock (_lock)
            {
                if (_pendingPolicyNavigations <= 0)
                {
                    return;
                }

                _pendingPolicyNavigations--;
                ReleaseUnderLock();
            }
        }

        /// <summary>
        /// Releases a single document-request retain when that navigation fails.
        /// </summary>
        internal void OnDocumentNavigationAborted()
        {
            lock (_lock)
            {
                if (_pendingDocumentNavigations <= 0)
                {
                    return;
                }

                _pendingDocumentNavigations--;
                ReleaseUnderLock();
            }
        }

        /// <summary>
        /// Releases every click retain when a main-frame document navigation
        /// fails terminally (TLS / certificate). A cancelled speculative
        /// willCheck must not use this path — that leaves the real form GET
        /// retained (ShouldWorkWithGotoFollowingClick).
        /// </summary>
        internal void OnTerminalDocumentNavigationFailed()
        {
            lock (_lock)
            {
                int pending = _pendingPolicyNavigations + _pendingDocumentNavigations;
                _pendingPolicyNavigations = 0;
                _pendingDocumentNavigations = 0;
                for (int i = 0; i < pending; i++)
                {
                    ReleaseUnderLock();
                }
            }
        }

        /// <summary>
        /// Drops the constructor retain and waits until the protect count is 0.
        /// Reopens if a late <see cref="ExpectMainFrameNavigation"/> races the release
        /// (WebKit form GET after empty poll — ShouldWorkWithGotoFollowingClick).
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when no retains remain.</returns>
        internal async Task WaitForAsync(float? timeout)
        {
            Task wait;
            lock (_lock)
            {
                ReleaseUnderLock();
                wait = _done.Task;
            }

            while (true)
            {
                await WaitUntilDoneAsync(wait, timeout).ConfigureAwait(false);

                lock (_lock)
                {
                    if (_protectCount <= 0
                        && _pendingPolicyNavigations <= 0
                        && _pendingDocumentNavigations <= 0
                        && _done.Task.IsCompleted)
                    {
                        return;
                    }

                    wait = _done.Task;
                }
            }
        }

        /// <summary>
        /// Waits until protect count is 0 without an additional Release.
        /// Used after a late retain reopens the barrier.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds.</param>
        /// <returns>A task that completes when idle.</returns>
        internal async Task WaitUntilIdleAsync(float? timeout)
        {
            while (true)
            {
                Task wait;
                lock (_lock)
                {
                    if (_protectCount <= 0
                        && _pendingPolicyNavigations <= 0
                        && _pendingDocumentNavigations <= 0
                        && _done.Task.IsCompleted)
                    {
                        return;
                    }

                    wait = _done.Task;
                }

                await WaitUntilDoneAsync(wait, timeout).ConfigureAwait(false);
            }
        }

        private static async Task WaitUntilDoneAsync(Task wait, float? timeout)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            if (timeoutMs == Timeout.Infinite)
            {
                await wait.ConfigureAwait(false);
                return;
            }

            Task delay = Task.Delay(timeoutMs);
            Task completed = await Task.WhenAny(wait, delay).ConfigureAwait(false);
            if (completed != wait)
            {
                throw new TimeoutException(
                    "Timeout " +
                    timeoutMs.ToString(CultureInfo.InvariantCulture) +
                    "ms exceeded.\nCall log:\n  - waiting for scheduled navigations to finish");
            }

            await wait.ConfigureAwait(false);
        }

        private void RetainUnderLock()
        {
            if (_protectCount++ == 0 && _done.Task.IsCompleted)
            {
                _done = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }
        }

        private void ReleaseUnderLock()
        {
            if (--_protectCount == 0)
            {
                _done.TrySetResult(true);
            }
        }
    }
}
