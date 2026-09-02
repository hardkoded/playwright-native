/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>frame.addScriptTag</c> validation, <c>sourceURL</c> suffix, injected
    /// functions, and CSP-console race from
    /// <c>packages/playwright-core/src/server/frames.ts</c> and
    /// <c>packages/playwright-core/src/client/clientHelper.ts</c>.
    /// </summary>
    internal static class AddScriptTagHelper
    {
        /// <summary>
        /// Official error when neither <c>url</c>, <c>path</c>, nor <c>content</c> is set.
        /// </summary>
        internal const string MissingOptionsMessage =
            "Provide an object with a `url`, `path` or `content` property";

        /// <summary>
        /// Official <c>addScriptUrl</c> function: appends <c>&lt;script src&gt;</c> and
        /// waits for <c>load</c>, rejecting with <c>Failed to load script at …</c>.
        /// </summary>
        internal const string AddScriptUrlFunction =
            @"async (url, type) => {
                const script = document.createElement('script');
                script.src = url;
                if (type)
                    script.type = type;
                const promise = new Promise((res, rej) => {
                    script.onload = res;
                    script.onerror = e => rej(typeof e === 'string' ? new Error(e) : new Error('Failed to load script at ' + script.src));
                });
                document.head.appendChild(script);
                await promise;
                return script;
            }";

        /// <summary>
        /// Official <c>addScriptContent</c> function: sets <c>script.text</c> and throws
        /// when <c>onerror</c> fires (CSP-blocked inline scripts).
        /// </summary>
        internal const string AddScriptContentFunction =
            @"(content, type) => {
                const script = document.createElement('script');
                script.type = type || 'text/javascript';
                script.text = content;
                let error = null;
                script.onerror = e => error = e;
                document.head.appendChild(script);
                if (error)
                    throw error;
                return script;
            }";

        /// <summary>
        /// Validates options, reads <paramref name="path"/> when set, and appends
        /// <c>//# sourceURL=</c> so stacks name the original file.
        /// </summary>
        /// <param name="url">External script URL.</param>
        /// <param name="path">Local file to inject as content.</param>
        /// <param name="content">Inline script text.</param>
        /// <param name="type">Optional <c>type</c> attribute.</param>
        /// <returns>The resolved URL/content/type triple.</returns>
        internal static Resolved Resolve(string url, string path, string content, string type)
        {
            if (string.IsNullOrEmpty(url) && string.IsNullOrEmpty(path) && string.IsNullOrEmpty(content))
            {
                throw new PlaywrightSharpException(MissingOptionsMessage);
            }

            if (!string.IsNullOrEmpty(path))
            {
                content = AddSourceUrlToScript(PathIo.ReadText(path), path);
            }

            return new Resolved(url, content, type ?? string.Empty);
        }

        /// <summary>
        /// Official <c>addSourceUrlToScript</c>: appends <c>//# sourceURL=</c> plus the
        /// path with newlines stripped.
        /// </summary>
        /// <param name="source">File contents.</param>
        /// <param name="path">Filesystem path used as the source URL.</param>
        /// <returns>The source plus the sourceURL comment.</returns>
        internal static string AddSourceUrlToScript(string source, string path)
        {
            string safePath = (path ?? string.Empty).Replace("\n", string.Empty);
            return (source ?? string.Empty) + "\n//# sourceURL=" + safePath;
        }

        /// <summary>
        /// Whether <paramref name="message"/> is a console error about Content-Security-Policy.
        /// </summary>
        /// <param name="message">A page console message.</param>
        /// <returns><see langword="true"/> when the text names CSP.</returns>
        internal static bool IsCspError(IConsoleMessage message)
        {
            if (message == null || !string.Equals(message.Type, "error", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            string text = message.Text ?? string.Empty;
            return text.Contains("Content-Security-Policy", StringComparison.Ordinal)
                || text.Contains("Content Security Policy", StringComparison.Ordinal);
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
        internal static async Task<T> RaceWithCspErrorAsync<T>(
            Action<EventHandler<IConsoleMessage>> addHandler,
            Action<EventHandler<IConsoleMessage>> removeHandler,
            Func<Task<T>> action)
        {
            if (addHandler == null)
            {
                throw new ArgumentNullException(nameof(addHandler));
            }

            if (removeHandler == null)
            {
                throw new ArgumentNullException(nameof(removeHandler));
            }

            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            TaskCompletionSource<string> csp = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);

            void OnConsole(object sender, IConsoleMessage message)
            {
                if (IsCspError(message))
                {
                    csp.TrySetResult(message.Text ?? MissingOptionsMessage);
                }
            }

            addHandler(OnConsole);
            try
            {
                Task<T> actionTask = action();
                _ = await Task.WhenAny(actionTask, csp.Task).ConfigureAwait(false);

                if (csp.Task.IsCompletedSuccessfully)
                {
                    throw new PlaywrightSharpException(await csp.Task.ConfigureAwait(false));
                }

                return await actionTask.ConfigureAwait(false);
            }
            finally
            {
                removeHandler(OnConsole);
            }
        }

        /// <summary>
        /// Resolved <c>addScriptTag</c> inputs after path-to-content conversion.
        /// </summary>
        internal sealed class Resolved
        {
            /// <summary>
            /// Initializes a new instance of the <see cref="Resolved"/> class.
            /// </summary>
            /// <param name="url">External script URL, or <see langword="null"/>.</param>
            /// <param name="content">Inline script body, or <see langword="null"/>.</param>
            /// <param name="type">The <c>type</c> attribute, or empty.</param>
            internal Resolved(string url, string content, string type)
            {
                Url = url;
                Content = content;
                Type = type;
            }

            /// <summary>
            /// Gets the external script URL.
            /// </summary>
            internal string Url { get; }

            /// <summary>
            /// Gets the inline script body (including <c>sourceURL</c> when from a path).
            /// </summary>
            internal string Content { get; }

            /// <summary>
            /// Gets the <c>type</c> attribute.
            /// </summary>
            internal string Type { get; }
        }
    }
}
