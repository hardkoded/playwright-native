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
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Collects <c>[box=x,y,width,height]</c> rectangles for official
    /// <c>ariaSnapshot({ boxes })</c>.
    /// </summary>
    internal static class AriaSnapshotBoxes
    {
        private const string CollectScript = @"el => {
  function collect(node) {
    const rect = node.getBoundingClientRect();
    const row = [rect.x, rect.y, rect.width, rect.height];
    const out = [row];
    for (let i = 0; i < node.children.length; i++) {
      const nested = collect(node.children[i]);
      for (let j = 0; j < nested.length; j++) {
        out.push(nested[j]);
      }
    }
    return out;
  }
  return collect(el);
}";

        /// <summary>
        /// Returns pre-order bounding boxes for <paramref name="root"/> and
        /// its element descendants, rounded to integers.
        /// </summary>
        /// <param name="root">The snapshot root element.</param>
        /// <returns>Boxes as <c>x,y,width,height</c> rows.</returns>
        internal static async Task<IReadOnlyList<int[]>> CollectAsync(IElementHandle root)
        {
            if (root == null)
            {
                throw new ArgumentNullException(nameof(root));
            }

            JsonElement raw = await root.EvaluateAsync<JsonElement>(CollectScript).ConfigureAwait(false);
            if (raw.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<int[]>();
            }

            List<int[]> boxes = new List<int[]>();
            foreach (JsonElement row in raw.EnumerateArray())
            {
                if (row.ValueKind != JsonValueKind.Array)
                {
                    continue;
                }

                int[] values = new int[4];
                int index = 0;
                foreach (JsonElement number in row.EnumerateArray())
                {
                    if (index >= 4)
                    {
                        break;
                    }

                    if (number.ValueKind == JsonValueKind.Number)
                    {
                        values[index] = (int)Math.Round(number.GetDouble(), MidpointRounding.AwayFromZero);
                    }

                    index++;
                }

                if (index >= 4)
                {
                    boxes.Add(values);
                }
            }

            return boxes;
        }
    }
}
