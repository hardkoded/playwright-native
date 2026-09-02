/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Launch-level official <c>artifactsDir</c>.
    /// </summary>
    internal interface IHasArtifactsDir
    {
        /// <summary>Directory for downloads, videos, and other artifacts.</summary>
        string ArtifactsDir { get; set; }
    }
}
