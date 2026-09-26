using System;
using Microsoft.Playwright;

namespace PlaywrightNative
{
    /// <summary>
    /// Exception thrown when a <see cref="IPage"/> fails to navigate an URL.
    /// </summary>
    public class NavigationException : PlaywrightException
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationException"/> class.
        /// </summary>
        public NavigationException()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        public NavigationException(string message) : base(message)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="url">Url.</param>
        /// <param name="innerException">Inner exception.</param>
        public NavigationException(string message, string url, Exception innerException = null) : base(TryAddUrl(message, url), innerException)
        {
            Url = url;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationException"/> class
        /// with the Chromium document (loader) id from an aborted <c>Page.navigate</c>.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="url">Url.</param>
        /// <param name="documentId">
        /// CDP <c>loaderId</c> for the aborted navigation, or <see langword="null"/>.
        /// </param>
        /// <param name="innerException">Inner exception.</param>
        public NavigationException(string message, string url, string documentId, Exception innerException = null)
            : base(TryAddUrl(message, url), innerException)
        {
            Url = url;
            DocumentId = documentId;
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="NavigationException"/> class.
        /// </summary>
        /// <param name="message">Message.</param>
        /// <param name="innerException">Inner exception.</param>
        public NavigationException(string message, Exception innerException)
            : this(message, (innerException as NavigationException)?.Url, (innerException as NavigationException)?.DocumentId, innerException)
        {
        }

        /// <summary>
        /// Url that caused the exception.
        /// </summary>
        /// <value>The URL.</value>
        public string Url { get; }

        /// <summary>
        /// Chromium document id (<c>loaderId</c>) when <c>Page.navigate</c> aborted
        /// after assigning one, or <see langword="null"/>.
        /// </summary>
        public string DocumentId { get; }

        private static string TryAddUrl(string message, string url)
        {
            if (string.IsNullOrEmpty(url) || string.IsNullOrEmpty(message) || message.Contains(url, StringComparison.Ordinal))
            {
                return message;
            }

            return message + " (" + url + ")";
        }
    }
}
