/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>waitForLoadState</c> string names.
    /// </summary>
    internal static class LoadStateName
    {
        /// <summary>
        /// Parses an official load-state name or throws the official error.
        /// </summary>
        /// <param name="state">The official string state.</param>
        /// <returns>The matching <see cref="LoadState"/>.</returns>
        internal static LoadState Parse(string state)
        {
            if (string.Equals(state, "load", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.Load;
            }

            if (string.Equals(state, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.DOMContentLoaded;
            }

            if (string.Equals(state, "networkidle", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.NetworkIdle;
            }

            if (string.Equals(state, "commit", StringComparison.OrdinalIgnoreCase))
            {
                return LoadState.Load;
            }

            throw new PlaywrightSharpException("state: expected one of (load|domcontentloaded|networkidle|commit)");
        }
    }
}
