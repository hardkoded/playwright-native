/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// In-memory buffer of <see cref="IConsoleMessage"/> values raised on a page.
    /// </summary>
    internal sealed class PageConsoleLog
    {
        private readonly List<IConsoleMessage> _messages = new();
        private readonly object _gate = new();
        private int _navigationWatermark;

        /// <summary>
        /// Appends <paramref name="message"/> when it is not <see langword="null"/>.
        /// </summary>
        /// <param name="message">The console message.</param>
        internal void Add(IConsoleMessage message)
        {
            if (message == null)
            {
                return;
            }

            lock (_gate)
            {
                _messages.Add(message);
            }
        }

        /// <summary>
        /// Returns stored messages using official
        /// <c>page.consoleMessages({ filter })</c> rules.
        /// </summary>
        /// <param name="filter">
        /// <see cref="ConsoleMessagesFilter.All"/> returns the full buffer.
        /// Any other value (including the default) returns messages logged
        /// after the last <see cref="MarkNavigation"/>.
        /// </param>
        /// <returns>The messages in arrival order.</returns>
        internal IReadOnlyList<IConsoleMessage> Snapshot(ConsoleMessagesFilter filter)
        {
            lock (_gate)
            {
                if (filter == ConsoleMessagesFilter.All)
                {
                    return _messages.ToArray();
                }

                if (_navigationWatermark <= 0)
                {
                    return _messages.ToArray();
                }

                if (_navigationWatermark >= _messages.Count)
                {
                    return Array.Empty<IConsoleMessage>();
                }

                int count = _messages.Count - _navigationWatermark;
                IConsoleMessage[] slice = new IConsoleMessage[count];
                _messages.CopyTo(_navigationWatermark, slice, 0, count);
                return slice;
            }
        }

        /// <summary>
        /// Marks the current end of the buffer as the last committed
        /// main-frame navigation. Subsequent
        /// <see cref="Snapshot(ConsoleMessagesFilter)"/> calls with the
        /// default filter omit earlier messages.
        /// </summary>
        internal void MarkNavigation()
        {
            lock (_gate)
            {
                _navigationWatermark = _messages.Count;
            }
        }

        /// <summary>
        /// Drops every recorded message.
        /// </summary>
        internal void Clear()
        {
            lock (_gate)
            {
                _messages.Clear();
                _navigationWatermark = 0;
            }
        }
    }
}
