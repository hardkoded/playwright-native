/*
 * Copyright (c) Microsoft Corporation.
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
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// The istanbul coverage format, mirrors the shapes from istanbul-lib-coverage.
    /// Port of <c>packages/isomorphic/istanbulCoverage.ts</c>.
    /// </summary>
    internal static class IstanbulCoverage
    {
        /// <summary>Prefix of the <c>localStorage</c> keys that hold coverage stashes.</summary>
        internal const string StashPrefix = "__pwCoverage.";

        /// <summary>Message prefix of the error raised when a stash could not be written.</summary>
        internal const string StashError = "Failed to stash the coverage";

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        /// <summary>Parses a coverage chunk reported by the page.</summary>
        /// <param name="json">The chunk JSON.</param>
        /// <returns>The chunk.</returns>
        internal static IstanbulCoverageChunk ParseChunk(string json)
            => JsonSerializer.Deserialize<IstanbulCoverageChunk>(json, JsonOptions);

        /// <summary>Parses serialized coverage, e.g. a <c>trace.coverage</c> entry.</summary>
        /// <param name="json">The coverage JSON.</param>
        /// <returns>The coverage keyed by file.</returns>
        internal static Dictionary<string, IstanbulFileCoverage> Parse(string json)
            => JsonSerializer.Deserialize<Dictionary<string, IstanbulFileCoverage>>(json, JsonOptions);

        /// <summary>Serializes <paramref name="coverage"/> sorted by file.</summary>
        /// <param name="coverage">The coverage keyed by file.</param>
        /// <returns>The coverage JSON.</returns>
        internal static string Serialize(Dictionary<string, IstanbulFileCoverage> coverage)
            => JsonSerializer.Serialize(Sorted(coverage), JsonOptions);

        /// <summary>Returns <paramref name="coverage"/> sorted by file.</summary>
        /// <param name="coverage">The coverage keyed by file.</param>
        /// <returns>The sorted coverage.</returns>
        internal static SortedDictionary<string, IstanbulFileCoverage> Sorted(Dictionary<string, IstanbulFileCoverage> coverage)
            => new(coverage, StringComparer.Ordinal);

        /// <summary>Adds the counters of <paramref name="data"/> into <paramref name="into"/>.</summary>
        /// <param name="into">The accumulated coverage.</param>
        /// <param name="data">A coverage delta, the maps are only present in the first report of a file.</param>
        internal static void Merge(Dictionary<string, IstanbulFileCoverage> into, IReadOnlyDictionary<string, IstanbulFileCoverage> data)
        {
            foreach (KeyValuePair<string, IstanbulFileCoverage> item in data)
            {
                IstanbulFileCoverage fileCov = item.Value;
                if (!into.TryGetValue(item.Key, out IstanbulFileCoverage existing))
                {
                    existing = new IstanbulFileCoverage { Path = fileCov.Path, StatementMap = new(), FnMap = new(), BranchMap = new() };
                    into[item.Key] = existing;
                }

                // The maps can arrive late if the report that carried them was lost.
                if (fileCov.StatementMap != null && existing.StatementMap.Count == 0)
                {
                    existing.StatementMap = fileCov.StatementMap;
                    existing.FnMap = fileCov.FnMap ?? new();
                    existing.BranchMap = fileCov.BranchMap ?? new();

                    // Reports only carry the counters that were hit, zero fill the rest.
                    foreach (string key in existing.StatementMap.Keys)
                    {
                        existing.S.TryAdd(key, 0);
                    }

                    foreach (string key in existing.FnMap.Keys)
                    {
                        existing.F.TryAdd(key, 0);
                    }

                    foreach (KeyValuePair<string, IstanbulBranchMapping> branch in existing.BranchMap)
                    {
                        existing.B.TryAdd(branch.Key, branch.Value.Locations.Select(_ => 0L).ToList());
                    }
                }

                foreach (KeyValuePair<string, long> counter in fileCov.S ?? new())
                {
                    existing.S[counter.Key] = existing.S.GetValueOrDefault(counter.Key) + counter.Value;
                }

                foreach (KeyValuePair<string, long> counter in fileCov.F ?? new())
                {
                    existing.F[counter.Key] = existing.F.GetValueOrDefault(counter.Key) + counter.Value;
                }

                foreach (KeyValuePair<string, List<long>> counter in fileCov.B ?? new())
                {
                    if (!existing.B.TryGetValue(counter.Key, out List<long> branches))
                    {
                        branches = new List<long>();
                        existing.B[counter.Key] = branches;
                    }

                    for (int i = 0; i < counter.Value.Count; i++)
                    {
                        if (i < branches.Count)
                        {
                            branches[i] += counter.Value[i];
                        }
                        else
                        {
                            branches.Add(counter.Value[i]);
                        }
                    }
                }
            }
        }
    }
}
