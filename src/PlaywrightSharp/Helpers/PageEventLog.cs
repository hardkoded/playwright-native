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
    /// In-memory buffer of page-level events (errors, requests, ...).
    /// </summary>
    /// <typeparam name="T">The recorded payload type.</typeparam>
    internal sealed class PageEventLog<T>
    {
        private readonly List<T> _items = new();
        private readonly object _gate = new();
        private readonly int _maxCount;
        private int _navigationWatermark;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageEventLog{T}"/> class.
        /// </summary>
        /// <param name="maxCount">
        /// When greater than zero, only the most recent <paramref name="maxCount"/>
        /// items are retained (official <c>page.requests()</c> keeps 100).
        /// </param>
        internal PageEventLog(int maxCount = 0)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(maxCount);
            _maxCount = maxCount;
        }

        /// <summary>
        /// Appends <paramref name="item"/> when it is not the default value for reference types.
        /// </summary>
        /// <param name="item">The payload to record.</param>
        internal void Add(T item)
        {
            if (item is null)
            {
                return;
            }

            lock (_gate)
            {
                _items.Add(item);
                if (_maxCount > 0 && _items.Count > _maxCount)
                {
                    _items.RemoveAt(0);
                    if (_navigationWatermark > 0)
                    {
                        _navigationWatermark--;
                    }
                }
            }
        }

        /// <summary>
        /// Returns a snapshot of items recorded so far.
        /// </summary>
        /// <returns>The items in arrival order.</returns>
        internal IReadOnlyList<T> Snapshot()
        {
            lock (_gate)
            {
                return _items.ToArray();
            }
        }

        /// <summary>
        /// Returns items recorded after the last <see cref="MarkNavigation"/>.
        /// When no navigation has been marked, returns the full buffer.
        /// </summary>
        /// <returns>The items in arrival order.</returns>
        internal IReadOnlyList<T> SnapshotAfterNavigation()
        {
            lock (_gate)
            {
                if (_navigationWatermark <= 0)
                {
                    return _items.ToArray();
                }

                if (_navigationWatermark >= _items.Count)
                {
                    return Array.Empty<T>();
                }

                int count = _items.Count - _navigationWatermark;
                T[] slice = new T[count];
                _items.CopyTo(_navigationWatermark, slice, 0, count);
                return slice;
            }
        }

        /// <summary>
        /// Marks the current end of the buffer as the last committed
        /// main-frame navigation.
        /// </summary>
        internal void MarkNavigation()
        {
            lock (_gate)
            {
                _navigationWatermark = _items.Count;
            }
        }

        /// <summary>
        /// Drops every recorded item.
        /// </summary>
        internal void Clear()
        {
            lock (_gate)
            {
                _items.Clear();
                _navigationWatermark = 0;
            }
        }
    }
}
