/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using MicrosoftPlaywright = Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>Maps legacy <see cref="UnrouteBehavior"/> to official values.</summary>
    internal static class UnrouteBehaviorBridge
    {
        internal static MicrosoftPlaywright.UnrouteBehavior ToOfficial(UnrouteBehavior behavior)
            => behavior switch
            {
                UnrouteBehavior.Wait => MicrosoftPlaywright.UnrouteBehavior.Wait,
                UnrouteBehavior.IgnoreErrors => MicrosoftPlaywright.UnrouteBehavior.IgnoreErrors,
                UnrouteBehavior.Default => MicrosoftPlaywright.UnrouteBehavior.Default,
                _ => MicrosoftPlaywright.UnrouteBehavior.Default,
            };
    }
}
