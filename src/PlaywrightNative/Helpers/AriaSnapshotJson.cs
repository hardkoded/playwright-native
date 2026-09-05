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
using System.IO;
using System.Text;
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Formats an <see cref="AccessibilitySnapshotResult"/> tree as official
    /// Playwright <c>ariaSnapshotJSON</c>.
    /// </summary>
    internal static class AriaSnapshotJson
    {
        /// <summary>
        /// Serializes <paramref name="node"/> to a JSON array of aria nodes.
        /// The same tree as <see cref="AriaSnapshotYaml.Format"/>, as JSON.
        /// </summary>
        /// <param name="node">The snapshot root, or <see langword="null"/>.</param>
        /// <param name="mode">Official Playwright <c>mode</c>. AI mode adds <c>ref</c>.</param>
        /// <param name="depth">
        /// Official Playwright <c>depth</c>. Maximum descendant level to
        /// include. The root is level 0. When omitted, the full tree is written.
        /// </param>
        /// <param name="boxes">
        /// Official Playwright <c>boxes</c>. Pre-order
        /// <c>x,y,width,height</c> rows written as <c>box</c> objects.
        /// </param>
        /// <returns>JSON text, or <c>[]</c> when <paramref name="node"/> is null.</returns>
        internal static string Format(
            AccessibilitySnapshotResult node,
            AriaSnapshotMode mode = AriaSnapshotMode.Default,
            int? depth = default,
            IReadOnlyList<int[]> boxes = null)
        {
            using MemoryStream stream = new MemoryStream();
            using Utf8JsonWriter writer = new Utf8JsonWriter(stream);
            writer.WriteStartArray();
            int nextRef = 1;
            int nextBox = 0;
            if (node != null)
            {
                Write(writer, node, mode == AriaSnapshotMode.Ai, ref nextRef, 0, depth, boxes, ref nextBox);
            }

            writer.WriteEndArray();
            writer.Flush();
            return Encoding.UTF8.GetString(stream.ToArray());
        }

        private static void Write(
            Utf8JsonWriter writer,
            AccessibilitySnapshotResult node,
            bool includeRefs,
            ref int nextRef,
            int level,
            int? maxDepth,
            IReadOnlyList<int[]> boxes,
            ref int nextBox)
        {
            if (maxDepth != null && level > maxDepth.Value)
            {
                return;
            }

            writer.WriteStartObject();
            string role = string.IsNullOrEmpty(node.Role) ? "generic" : node.Role;
            if (includeRefs && string.Equals(role, "iframe", System.StringComparison.OrdinalIgnoreCase))
            {
                role = "iframe";
            }

            writer.WriteString("role", role);
            if (!string.IsNullOrEmpty(node.Name))
            {
                writer.WriteString("name", node.Name);
            }

            if (node.Level > 0)
            {
                writer.WriteNumber("level", node.Level);
            }

            if (includeRefs && node.Focused)
            {
                writer.WriteBoolean("active", true);
            }

            if (includeRefs)
            {
                writer.WriteString("ref", "e" + nextRef.ToString(System.Globalization.CultureInfo.InvariantCulture));
                nextRef++;
            }

            if (boxes != null && nextBox < boxes.Count)
            {
                int[] box = boxes[nextBox];
                nextBox++;
                writer.WritePropertyName("box");
                writer.WriteStartObject();
                writer.WriteNumber("x", box[0]);
                writer.WriteNumber("y", box[1]);
                writer.WriteNumber("width", box[2]);
                writer.WriteNumber("height", box[3]);
                writer.WriteEndObject();
            }

            if (node.Children != null && (maxDepth == null || level < maxDepth.Value))
            {
                bool wroteChildren = false;
                foreach (AccessibilitySnapshotResult child in node.Children)
                {
                    if (!wroteChildren)
                    {
                        writer.WritePropertyName("children");
                        writer.WriteStartArray();
                        wroteChildren = true;
                    }

                    Write(writer, child, includeRefs, ref nextRef, level + 1, maxDepth, boxes, ref nextBox);
                }

                if (wroteChildren)
                {
                    writer.WriteEndArray();
                }
            }

            writer.WriteEndObject();
        }
    }
}
