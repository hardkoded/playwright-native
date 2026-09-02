/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IWebError"/> payload.
    /// </summary>
    public sealed partial class WebError : IWebError
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebError"/> class.
        /// </summary>
        /// <param name="page">The page that produced the exception.</param>
        /// <param name="error">The unhandled error text.</param>
        /// <param name="location">Optional file, line, and column.</param>
        public WebError(IPage page, string error, WebErrorLocation location = null)
        {
            Page = page;
            Error = error ?? string.Empty;
            Location = location ?? new WebErrorLocation();
        }

        /// <inheritdoc/>
        public IPage Page { get; }

        /// <inheritdoc/>
        public string Error { get; }

        /// <inheritdoc/>
        public WebErrorLocation Location { get; }
    }
}
