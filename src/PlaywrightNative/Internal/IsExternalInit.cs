/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */

#if !NET5_0_OR_GREATER

using System.ComponentModel;

namespace System.Runtime.CompilerServices
{
    /// <summary>
    /// Marker type required by the C# compiler to emit <c>init</c> accessors
    /// on target frameworks (like netstandard2.1) that do not ship it.
    /// Intentionally internal so it doesn't leak into consumers.
    /// </summary>
    [EditorBrowsable(EditorBrowsableState.Never)]
    internal static class IsExternalInit
    {
    }
}

#endif
