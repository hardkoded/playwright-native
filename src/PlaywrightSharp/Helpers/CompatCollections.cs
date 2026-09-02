/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Collection helpers for compatibility extension return types.
    /// </summary>
    internal static class CompatCollections
    {
        /// <summary>Adapts official <see cref="IReadOnlyList{T}"/> to <see cref="IReadOnlyCollection{T}"/>.</summary>
        internal static IReadOnlyCollection<T> AsCollection<T>(IReadOnlyList<T> items)
            => items as IReadOnlyCollection<T> ?? items.ToArray();

        /// <summary>Awaits a select-option call and adapts the collection type.</summary>
        internal static async Task<IReadOnlyCollection<T>> AsCollectionAsync<T>(Task<IReadOnlyList<T>> task)
            => AsCollection(await task.ConfigureAwait(false));

        /// <summary>Awaits a select-option call and adapts the collection type.</summary>
        internal static async Task<IReadOnlyList<T>> AsListAsync<T>(Task<IReadOnlyList<T>> task)
            => await task.ConfigureAwait(false);

        /// <summary>Awaits a select-option call and adapts the collection type.</summary>
        internal static async Task<IReadOnlyList<T>> AsListAsync<T>(Task<IReadOnlyCollection<T>> task)
            => AsList(await task.ConfigureAwait(false));

        /// <summary>Adapts <see cref="IReadOnlyCollection{T}"/> to <see cref="IReadOnlyList{T}"/>.</summary>
        internal static IReadOnlyList<T> AsList<T>(IReadOnlyCollection<T> items)
            => items as IReadOnlyList<T> ?? items?.ToArray() ?? Array.Empty<T>();
    }
}
