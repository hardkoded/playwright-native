/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Retain/release gate that stays open until scheduled main-frame
    /// navigations commit. Mirrors upstream <c>SignalBarrier</c>.
    /// </summary>
    internal sealed class ActionSignalBarrier
    {
        private readonly TaskCompletionSource<bool> _done = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private int _protectCount;
        private int _pendingNavigations;

        /// <summary>
        /// Initializes a new instance of the <see cref="ActionSignalBarrier"/> class.
        /// Starts retained so <see cref="WaitForAsync"/> can drop the last hold.
        /// </summary>
        internal ActionSignalBarrier()
        {
            Retain();
        }

        /// <summary>
        /// Records that a main-frame navigation was requested and waits for
        /// the matching commit.
        /// </summary>
        internal void ExpectMainFrameNavigation()
        {
            Interlocked.Increment(ref _pendingNavigations);
            Retain();
        }

        /// <summary>
        /// Releases every pending navigation retain when the main frame commits.
        /// Multiple signals (policy check + document request) describe one navigation.
        /// </summary>
        internal void OnMainFrameNavigated()
        {
            int pending = Interlocked.Exchange(ref _pendingNavigations, 0);
            for (int i = 0; i < pending; i++)
            {
                Release();
            }
        }

        /// <summary>
        /// Drops the constructor retain and waits until the protect count is 0.
        /// </summary>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever.</param>
        /// <returns>A task that completes when no retains remain.</returns>
        internal async Task WaitForAsync(float? timeout)
        {
            Release();
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
            if (timeoutMs == Timeout.Infinite)
            {
                await _done.Task.ConfigureAwait(false);
                return;
            }

            Task delay = Task.Delay(timeoutMs);
            Task completed = await Task.WhenAny(_done.Task, delay).ConfigureAwait(false);
            if (completed != _done.Task)
            {
                throw new TimeoutException(
                    "Timeout " +
                    timeoutMs.ToString(CultureInfo.InvariantCulture) +
                    "ms exceeded.\nCall log:\n  - waiting for scheduled navigations to finish");
            }

            await _done.Task.ConfigureAwait(false);
        }

        private void Retain() => Interlocked.Increment(ref _protectCount);

        private void Release()
        {
            if (Interlocked.Decrement(ref _protectCount) == 0)
            {
                _done.TrySetResult(true);
            }
        }
    }
}
