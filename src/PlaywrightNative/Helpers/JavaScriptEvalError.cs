/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Reconstructed JS <c>Error</c> from evaluate / jsonValue.
    /// </summary>
    internal sealed class JavaScriptEvalError
    {
        /// <summary>JS <c>error.name</c>.</summary>
        public string Name { get; set; }

        /// <summary>JS <c>error.message</c>.</summary>
        public string Message { get; set; }

        /// <summary>JS <c>error.stack</c>.</summary>
        public string Stack { get; set; }
    }
}
