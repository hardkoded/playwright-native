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
using System.Collections.Generic;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Holds WebSocket frames that arrive before <c>waitForEvent('framereceived')</c>
    /// subscribes. Official Node dispatches created and the first frame on separate
    /// tasks so the waiter can attach in between.
    /// </summary>
    /// <typeparam name="T">The event payload type.</typeparam>
    internal sealed class PendingWebSocketEvent<T>
    {
        private readonly object _gate = new object();
        private readonly List<T> _pending = new List<T>();
        private EventHandler<T> _handler;

        /// <summary>
        /// Subscribes and replays frames that arrived with no listener.
        /// </summary>
        /// <param name="handler">The listener to add.</param>
        internal void Add(EventHandler<T> handler)
        {
            if (handler == null)
            {
                return;
            }

            T[] replay;
            lock (_gate)
            {
                _handler += handler;
                replay = _pending.ToArray();
                _pending.Clear();
            }

            foreach (T payload in replay)
            {
                handler(null, payload);
            }
        }

        /// <summary>
        /// Removes <paramref name="handler"/>.
        /// </summary>
        /// <param name="handler">The listener to remove.</param>
        internal void Remove(EventHandler<T> handler)
        {
            lock (_gate)
            {
                _handler -= handler;
            }
        }

        /// <summary>
        /// Raises the event, or buffers the payload when no one is listening.
        /// </summary>
        /// <param name="sender">The socket.</param>
        /// <param name="payload">The frame or error payload.</param>
        internal void Invoke(object sender, T payload)
        {
            EventHandler<T> handler;
            lock (_gate)
            {
                handler = _handler;
                if (handler == null)
                {
                    _pending.Add(payload);
                    return;
                }
            }

            handler(sender, payload);
        }
    }
}
