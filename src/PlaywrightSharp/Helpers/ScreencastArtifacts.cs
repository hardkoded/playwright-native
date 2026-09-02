/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;

namespace PlaywrightSharp.Helpers
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
