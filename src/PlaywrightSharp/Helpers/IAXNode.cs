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
    /// Browser-specific accessibility node used to build
    /// <see cref="AccessibilitySnapshotResult"/>.
    /// </summary>
    internal interface IAXNode
    {
        /// <summary>Gets the child nodes.</summary>
        IReadOnlyList<IAXNode> ChildNodes { get; }

        /// <summary>Whether this node should be kept when pruning the interesting tree.</summary>
        /// <param name="insideControl">Whether an ancestor is a control.</param>
        /// <returns><see langword="true"/> if the node is interesting.</returns>
        bool IsInteresting(bool insideControl);

        /// <summary>Whether this node is treated as a leaf for snapshot purposes.</summary>
        /// <returns><see langword="true"/> if children should not be walked.</returns>
        bool IsLeafNode();

        /// <summary>Whether this node is a control role.</summary>
        /// <returns><see langword="true"/> if the role is a control.</returns>
        bool IsControl();

        /// <summary>Serializes this node without children.</summary>
        /// <returns>The snapshot node.</returns>
        AccessibilitySnapshotResult Serialize();
    }
}
