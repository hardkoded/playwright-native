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
using System.IO;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>screencast.start</c> files in <c>artifactsDir</c>.
    /// </summary>
    internal static class ScreencastArtifacts
    {
        /// <summary>
        /// Starts a writer under the browser <c>artifactsDir</c> when set.
        /// </summary>
        /// <param name="page">The page that owns the screencast.</param>
        /// <param name="width">Video width.</param>
        /// <param name="height">Video height.</param>
        /// <returns>The artifacts writer, or <see langword="null"/>.</returns>
        internal static ScreencastVideoWriter TryStart(IPage page, int width, int height)
        {
            string directory = Resolve(page);
            if (string.IsNullOrEmpty(directory))
            {
                return null;
            }

            Directory.CreateDirectory(directory);
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + ".webm");
            return ScreencastVideoWriter.Start(path, width, height);
        }

        private static string Resolve(IPage page)
        {
            IBrowser browser = page?.Context?.Browser;
            return browser is IHasArtifactsDir host ? host.ArtifactsDir : null;
        }
    }
}
