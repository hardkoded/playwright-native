/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Collections.Generic;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>browserContext._origins</c> and storage-state page flag.
    /// </summary>
    internal interface IHasStorageStateInternals
    {
        /// <summary>
        /// Origins visited by user pages, used when collecting storage state.
        /// </summary>
        IReadOnlyCollection<string> VisitedOrigins { get; }

        /// <summary>
        /// True while creating the official internal storage-state page.
        /// </summary>
        bool CreatingStorageStatePage { get; set; }
    }
}
