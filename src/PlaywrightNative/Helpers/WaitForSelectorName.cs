/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>waitForSelector</c> option names.
    /// </summary>
    internal static class WaitForSelectorName
    {
        /// <summary>
        /// Validates official <c>waitFor</c> / <c>visibility</c> options.
        /// <c>waitFor: 'visible'</c> is tolerated; any other <c>waitFor</c>
        /// throws. Any <c>visibility</c> throws.
        /// </summary>
        /// <param name="waitFor">Official <c>options.waitFor</c>.</param>
        /// <param name="visibility">Official <c>options.visibility</c>.</param>
        internal static void Validate(string waitFor, string visibility)
        {
            if (visibility != null)
            {
                throw new PlaywrightNativeException("options.visibility is not supported, did you mean options.state?");
            }

            if (waitFor != null && !string.Equals(waitFor, "visible", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("options.waitFor is not supported, did you mean options.state?");
            }
        }

        /// <summary>
        /// Parses an official waitForSelector state name or throws the official error.
        /// </summary>
        /// <param name="state">The official string state.</param>
        /// <returns>The matching <see cref="WaitForSelectorState"/>.</returns>
        internal static WaitForSelectorState Parse(string state)
        {
            if (string.Equals(state, "attached", StringComparison.OrdinalIgnoreCase))
            {
                return WaitForSelectorState.Attached;
            }

            if (string.Equals(state, "detached", StringComparison.OrdinalIgnoreCase))
            {
                return WaitForSelectorState.Detached;
            }

            if (string.Equals(state, "visible", StringComparison.OrdinalIgnoreCase))
            {
                return WaitForSelectorState.Visible;
            }

            if (string.Equals(state, "hidden", StringComparison.OrdinalIgnoreCase))
            {
                return WaitForSelectorState.Hidden;
            }

            throw InvalidState();
        }

        /// <summary>
        /// Boolean <c>state</c> is never a valid official option.
        /// </summary>
        /// <param name="state">The boolean state value.</param>
        /// <returns>Never returns.</returns>
        internal static WaitForSelectorState Parse(bool state)
        {
            _ = state;
            throw InvalidState();
        }

        /// <summary>Maps legacy state/waitFor/visibility values onto official states.</summary>
        internal static WaitForSelectorState? ToOfficialState(object value, string visibility = null)
        {
            if (value == null && visibility == null)
            {
                return null;
            }

            if (value is WaitForSelectorState state)
            {
                return state;
            }

            if (value is bool boolean)
            {
                return Parse(boolean);
            }

            if (value is string text)
            {
                if (!string.IsNullOrEmpty(visibility))
                {
                    Validate(text, visibility);
                }

                return Parse(text);
            }

            throw InvalidState();
        }

        private static PlaywrightNativeException InvalidState()
            => new PlaywrightNativeException("state: expected one of (attached|detached|visible|hidden)");
    }
}
