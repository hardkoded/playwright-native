/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp
{
    /// <summary>
    /// Legacy unroute behavior including an explicit <see cref="Undefined"/> sentinel.
    /// </summary>
    public enum UnrouteBehavior
    {
        /// <summary>Unset behavior (defaults to <see cref="Default"/>).</summary>
        Undefined = 0,

        /// <summary>Wait for in-flight handlers to finish.</summary>
        Wait = 1,

        /// <summary>Stop routing and ignore handler errors.</summary>
        IgnoreErrors = 2,

        /// <summary>Stop routing without waiting.</summary>
        Default = 3,
    }
}
