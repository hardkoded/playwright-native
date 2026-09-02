/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>frame.addStyleTag</c> validation, CSS <c>sourceURL</c> suffix, injected
    /// functions, and CSP-console race from
    /// <c>packages/playwright-core/src/server/frames.ts</c> and
    /// <c>packages/playwright-core/src/client/frame.ts</c>.
    /// </summary>
    internal static class AddStyleTagHelper
    {
        /// <summary>
        /// Official error when neither <c>url</c>, <c>path</c>, nor <c>content</c> is set.
        /// </summary>
        internal const string MissingOptionsMessage =
            "Provide an object with a `url`, `path` or `content` property";

        /// <summary>
        /// Official <c>addStyleUrl</c> function: appends <c>&lt;link rel="stylesheet"&gt;</c>
        /// and waits for <c>load</c>.
        /// </summary>
        internal const string AddStyleUrlFunction =
            @"async (url) => {
                const link = document.createElement('link');
                link.rel = 'stylesheet';
                link.href = url;
                const promise = new Promise((res, rej) => {
                    link.onload = res;
                    link.onerror = rej;
                });
                document.head.appendChild(link);
                await promise;
                return link;
            }";

        /// <summary>
        /// Official <c>addStyleContent</c> function: appends <c>&lt;style&gt;</c> and
        /// waits for <c>load</c> / <c>error</c> (CSP-blocked inline styles).
        /// </summary>
        internal const string AddStyleContentFunction =
            @"async (content) => {
                const style = document.createElement('style');
                style.type = 'text/css';
                style.appendChild(document.createTextNode(content));
                const promise = new Promise((res, rej) => {
                    style.onload = res;
                    style.onerror = rej;
                });
                document.head.appendChild(style);
                await promise;
                return style;
            }";

        /// <summary>
        /// Validates options, reads <paramref name="path"/> when set, and appends
        /// <c>/*# sourceURL=</c> so DevTools names the original file.
        /// </summary>
        /// <param name="url">External stylesheet URL.</param>
        /// <param name="path">Local file to inject as content.</param>
        /// <param name="content">Inline CSS text.</param>
        /// <returns>The resolved URL/content pair.</returns>
        internal static Resolved Resolve(string url, string path, string content)
        {
            if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(path) && string.IsNullOrEmpty(content))
            {
                throw new PlaywrightNativeException(MissingOptionsMessage);
            }

            if (!string.IsNullOrEmpty(path))
            {
                content = AddSourceUrlToStyle(PathIo.ReadText(path), path);
            }

            return new Resolved(url, content);
        }

        /// <summary>
        /// Official client <c>addStyleTag</c> path suffix: <c>/*# sourceURL=…*/</c>
        /// with newlines stripped from the path.
        /// </summary>
        /// <param name="source">File contents.</param>
        /// <param name="path">Filesystem path used as the source URL.</param>
        /// <returns>The source plus the sourceURL comment.</returns>
        internal static string AddSourceUrlToStyle(string source, string path)
        {
            string safePath = (path ?? string.Empty).Replace("\n", string.Empty);
            return (source ?? string.Empty) + "/*# sourceURL=" + safePath + "*/";
        }

        /// <summary>
        /// Official <c>_raceWithCSPError</c>: prefers a CSP console error over a
        /// successful inject or a load failure.
        /// </summary>
        /// <typeparam name="T">The inject result type.</typeparam>
        /// <param name="addHandler">Subscribes to page console events.</param>
        /// <param name="removeHandler">Unsubscribes from page console events.</param>
        /// <param name="action">The inject that may complete or throw.</param>
        /// <returns>The inject result when no CSP error arrived first.</returns>
        internal static Task<T> RaceWithCspErrorAsync<T>(
            Action<EventHandler<IConsoleMessage>> addHandler,
            Action<EventHandler<IConsoleMessage>> removeHandler,
            Func<Task<T>> action)
            => AddScriptTagHelper.RaceWithCspErrorAsync(addHandler, removeHandler, action);

        /// <summary>
        /// Resolved <c>addStyleTag</c> inputs after path-to-content conversion.
        /// </summary>
        internal sealed class Resolved
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Resolved"/> class.
            /// </summary>
            /// <param name="url">External stylesheet URL, or <see langword="null"/>.</param>
            /// <param name="content">Inline CSS body, or <see langword="null"/>.</param>
            internal Resolved(string url, string content)
            {
                Url = url;
                Content = content;
            }

            /// <summary>
            /// Gets the external stylesheet URL.
            /// </summary>
            internal string Url { get; }

            /// <summary>
            /// Gets the inline CSS body (including <c>sourceURL</c> when from a path).
            /// </summary>
            internal string Content { get; }
        }
    }
}
