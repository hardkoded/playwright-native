/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Shared waiter for <c>page.waitForLoadState</c>. Resolves immediately when the
    /// requested lifecycle event is already recorded, otherwise subscribes until it
    /// fires or the timeout elapses. Mirrors upstream Frame._waitForLoadState.
    /// </summary>
    internal static class LifecycleWaiter
    {
        /// <summary>
        /// Maps a public <see cref="LoadState"/> to the internal lifecycle event name
        /// recorded by CR/FF/WK frame trackers (<c>load</c>, <c>DOMContentLoaded</c>,
        /// <c>networkidle</c>).
        /// </summary>
        /// <param name="state">The requested load state. <see cref="LoadState.Undefined"/> means load.</param>
        /// <returns>The lifecycle event name.</returns>
        internal static string ToEventName(LoadState state)
        {
            return state switch
            {
                LoadState.DOMContentLoaded => "DOMContentLoaded",
                LoadState.NetworkIdle => "networkidle",
                _ => "load",
            };
        }

        /// <summary>
        /// Waits until <paramref name="snapshot"/> contains the mapped lifecycle event.
        /// </summary>
        /// <param name="snapshot">Returns the currently recorded lifecycle event names.</param>
        /// <param name="subscribe">Adds a <c>LifecycleChanged</c> handler.</param>
        /// <param name="unsubscribe">Removes a <c>LifecycleChanged</c> handler.</param>
        /// <param name="state">The load state to wait for.</param>
        /// <param name="timeout">Timeout in milliseconds. <c>0</c> waits forever. <see langword="null"/> uses 30s.</param>
        /// <param name="apiName">Name used in the timeout message (upstream asserts on this).</param>
        /// <returns>A task that completes when the state is reached.</returns>
        internal static async Task WaitAsync(
            Func<IReadOnlyCollection<string>> snapshot,
            Action<Action<string>> subscribe,
            Action<Action<string>> unsubscribe,
            LoadState state,
            float? timeout,
            string apiName = "page.waitForLoadState")
        {
            if (snapshot == null)
            {
                throw new ArgumentNullException(nameof(snapshot));
            }

            if (subscribe == null)
            {
                throw new ArgumentNullException(nameof(subscribe));
            }

            if (unsubscribe == null)
            {
                throw new ArgumentNullException(nameof(unsubscribe));
            }

            string name = ToEventName(state);
            if (Contains(snapshot(), name))
            {
                return;
            }

            TaskCompletionSource<bool> tcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnChanged(string fired)
            {
                if (fired == name)
                {
                    tcs.TrySetResult(true);
                }
            }

            subscribe(OnChanged);
            try
            {
                if (Contains(snapshot(), name))
                {
                    return;
                }

                int timeoutMs = TimeoutSettings.TimeoutMs(timeout);
                if (timeoutMs == Timeout.Infinite)
                {
                    await tcs.Task.ConfigureAwait(false);
                    return;
                }

                using CancellationTokenSource cts = new(timeoutMs);
                cts.Token.Register(
                    () => tcs.TrySetException(
                        new TimeoutException($"{apiName}: Timeout {timeoutMs}ms exceeded.")));

                await tcs.Task.ConfigureAwait(false);
            }
            finally
            {
                unsubscribe(OnChanged);
            }
        }

        private static bool Contains(IReadOnlyCollection<string> events, string name)
        {
            if (events == null)
            {
                return false;
            }

            foreach (string item in events)
            {
                if (item == name)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
