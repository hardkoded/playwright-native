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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Thread-safe barrier list used by Chromium and WebKit frame managers.
    /// </summary>
    internal sealed class ActionSignalHubState
    {
        private readonly object _lock = new object();
        private readonly List<ActionSignalBarrier> _barriers = new();

        /// <summary>
        /// Registers a barrier for the current action.
        /// </summary>
        /// <param name="barrier">The barrier.</param>
        internal void AddBarrier(ActionSignalBarrier barrier)
        {
            if (barrier == null)
            {
                return;
            }

            lock (_lock)
            {
                _barriers.Add(barrier);
            }
        }

        /// <summary>
        /// Unregisters a barrier after the action finishes.
        /// </summary>
        /// <param name="barrier">The barrier.</param>
        internal void RemoveBarrier(ActionSignalBarrier barrier)
        {
            if (barrier == null)
            {
                return;
            }

            lock (_lock)
            {
                _barriers.Remove(barrier);
            }
        }

        /// <summary>
        /// Tells every barrier that a main-frame navigation was requested.
        /// </summary>
        internal void ExpectMainFrameNavigation()
        {
            foreach (ActionSignalBarrier barrier in Snapshot())
            {
                barrier.ExpectMainFrameNavigation();
            }
        }

        /// <summary>
        /// Tells every barrier that the main frame committed a navigation.
        /// </summary>
        internal void OnMainFrameNavigated()
        {
            foreach (ActionSignalBarrier barrier in Snapshot())
            {
                barrier.OnMainFrameNavigated();
            }
        }

        private ActionSignalBarrier[] Snapshot()
        {
            lock (_lock)
            {
                return _barriers.ToArray();
            }
        }
    }
}
