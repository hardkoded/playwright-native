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
    /// Shared interesting-node filtering and tree serialization for
    /// accessibility snapshots used by aria matchers.
    /// </summary>
    internal static class AccessibilitySnapshotHelper
    {
        /// <summary>
        /// Builds the public snapshot from a browser AX tree.
        /// </summary>
        /// <param name="tree">The full tree root.</param>
        /// <param name="needle">Optional subtree root (when a DOM root was requested).</param>
        /// <param name="interestingOnly">When <see langword="true"/>, prune uninteresting nodes.</param>
        /// <returns>The serialized root, or <see langword="null"/>.</returns>
        internal static AccessibilitySnapshotResult Snapshot(IAXNode tree, IAXNode needle, bool interestingOnly)
        {
            if (tree == null)
            {
                return null;
            }

            if (!interestingOnly)
            {
                IAXNode root = needle ?? tree;
                IReadOnlyList<AccessibilitySnapshotResult> full = SerializeTree(root, null);
                return full.Count == 0 ? null : full[0];
            }

            HashSet<IAXNode> interesting = new HashSet<IAXNode>();
            CollectInterestingNodes(interesting, tree, insideControl: false);
            if (needle != null && !interesting.Contains(needle))
            {
                return null;
            }

            IReadOnlyList<AccessibilitySnapshotResult> filtered = SerializeTree(needle ?? tree, interesting);
            return filtered.Count == 0 ? null : filtered[0];
        }

        private static void CollectInterestingNodes(HashSet<IAXNode> collection, IAXNode node, bool insideControl)
        {
            if (node.IsInteresting(insideControl))
            {
                collection.Add(node);
            }

            if (node.IsLeafNode())
            {
                return;
            }

            bool nextInside = insideControl || node.IsControl();
            foreach (IAXNode child in node.ChildNodes)
            {
                CollectInterestingNodes(collection, child, nextInside);
            }
        }

        private static IReadOnlyList<AccessibilitySnapshotResult> SerializeTree(IAXNode node, HashSet<IAXNode> whitelisted)
        {
            List<AccessibilitySnapshotResult> children = new List<AccessibilitySnapshotResult>();
            foreach (IAXNode child in node.ChildNodes)
            {
                children.AddRange(SerializeTree(child, whitelisted));
            }

            if (whitelisted != null && !whitelisted.Contains(node))
            {
                return children;
            }

            AccessibilitySnapshotResult serialized = node.Serialize();
            if (children.Count > 0)
            {
                serialized.Children = children;
            }

            return new[] { serialized };
        }
    }
}
