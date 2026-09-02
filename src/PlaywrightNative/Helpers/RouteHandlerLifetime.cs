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
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Tracks in-flight route handler invocations so
    /// <see cref="UnrouteBehavior"/> can wait or ignore errors.
    /// </summary>
    internal sealed class RouteHandlerLifetime
    {
        private readonly object _lock = new();
        private readonly List<TaskCompletionSource<bool>> _active = new();
        private bool _ignoreErrors;

        /// <summary>
        /// Gets a value indicating whether later handler errors should be swallowed.
        /// </summary>
        internal bool IgnoreErrors
        {
            get
            {
                lock (_lock)
                {
                    return _ignoreErrors;
                }
            }
        }

        /// <summary>
        /// Applies <paramref name="behavior"/> to each lifetime.
        /// </summary>
        /// <param name="lifetimes">Removed route lifetimes.</param>
        /// <param name="behavior">Official wait / ignore / default.</param>
        /// <returns>A task that completes when wait (if any) is done.</returns>
        internal static Task StopAllAsync(IEnumerable<RouteHandlerLifetime> lifetimes, UnrouteBehavior behavior)
        {
            if (behavior != UnrouteBehavior.Wait && behavior != UnrouteBehavior.IgnoreErrors)
            {
                return Task.CompletedTask;
            }

            List<Task> tasks = new();
            foreach (RouteHandlerLifetime lifetime in lifetimes)
            {
                tasks.Add(lifetime.StopAsync(behavior));
            }

            return tasks.Count == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
        }

        /// <summary>
        /// Starts tracking one handler invocation.
        /// </summary>
        /// <returns>The completion source to pass to <see cref="End"/>.</returns>
        internal TaskCompletionSource<bool> Begin()
        {
            TaskCompletionSource<bool> invocation = new(TaskCreationOptions.RunContinuationsAsynchronously);
            lock (_lock)
            {
                _active.Add(invocation);
            }

            return invocation;
        }

        /// <summary>
        /// Marks one handler invocation complete.
        /// </summary>
        /// <param name="invocation">The value returned by <see cref="Begin"/>.</param>
        internal void End(TaskCompletionSource<bool> invocation)
        {
            invocation.TrySetResult(true);
            lock (_lock)
            {
                _active.Remove(invocation);
            }
        }

        /// <summary>
        /// Waits for in-flight invocations, or marks errors ignored.
        /// </summary>
        /// <param name="behavior">Official wait / ignore / default.</param>
        /// <returns>A task that completes when wait (if any) is done.</returns>
        internal Task StopAsync(UnrouteBehavior behavior)
        {
            if (behavior == UnrouteBehavior.IgnoreErrors)
            {
                lock (_lock)
                {
                    _ignoreErrors = true;
                }

                return Task.CompletedTask;
            }

            if (behavior != UnrouteBehavior.Wait)
            {
                return Task.CompletedTask;
            }

            Task[] tasks;
            lock (_lock)
            {
                tasks = new Task[_active.Count];
                for (int i = 0; i < _active.Count; i++)
                {
                    tasks[i] = _active[i].Task;
                }
            }

            return tasks.Length == 0 ? Task.CompletedTask : Task.WhenAll(tasks);
        }
    }
}
