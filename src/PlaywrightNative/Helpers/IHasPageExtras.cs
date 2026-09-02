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
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>PlaywrightNative-only page extras not on official <see cref="IPage"/>.</summary>
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
