/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>PlaywrightSharp-only page extras not on official <see cref="IPage"/>.</summary>
    internal interface IHasPageExtras
    {
        /// <summary>Emitted when a dialog is closed.</summary>
        event EventHandler<IDialog> DialogClosed;

        /// <summary>JS/CSS coverage.</summary>
        ICoverage Coverage { get; }

        /// <summary>
        /// Captures the accessibility tree for aria snapshots and expect matchers.
        /// </summary>
        /// <param name="interestingOnly">Prune uninteresting nodes. Defaults to <see langword="true"/>.</param>
        /// <param name="root">Optional DOM root. Defaults to the whole page.</param>
        /// <returns>The serialized tree root, or <see langword="null"/>.</returns>
        Task<AccessibilitySnapshotResult> SnapshotAccessibilityAsync(bool? interestingOnly = null, IElementHandle root = null);
    }
}
