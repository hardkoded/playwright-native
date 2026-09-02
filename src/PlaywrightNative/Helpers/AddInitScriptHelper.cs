/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>page.addInitScript</c> / <c>page.addInitScript({ path, content })</c>
    /// resolution from <c>packages/playwright-core/src/client/clientHelper.ts</c>
    /// (<c>evaluationScript</c>) and Chromium's concatenated
    /// <c>Page.addScriptToEvaluateOnNewDocument</c> sources.
    /// </summary>
    internal static class AddInitScriptHelper
    {
        /// <summary>
        /// Official error when neither <c>path</c> nor <c>content</c> is set.
        /// </summary>
        internal const string MissingOptionsMessage =
            "Either path or content property must be present";

        /// <summary>
        /// Reads <paramref name="scriptPath"/> when set, invokes function-like
        /// <paramref name="script"/> strings, and appends a trailing newline so a
        /// script that ends with <c>// comment</c> cannot comment out the next
        /// init script when the browser concatenates them.
        /// </summary>
        /// <param name="script">Inline script or function string.</param>
        /// <param name="scriptPath">Filesystem path to load instead of <paramref name="script"/>.</param>
        /// <param name="arg">Optional argument serialized and applied to a function script.</param>
        /// <returns>The source to install on new documents.</returns>
        internal static string Resolve(string script, string scriptPath, object arg = default)
        {
            if (string.IsNullOrEmpty(script) && !string.IsNullOrEmpty(scriptPath))
            {
                script = AddScriptTagHelper.AddSourceUrlToScript(PathIo.ReadText(scriptPath), scriptPath);
            }

            if (string.IsNullOrEmpty(script))
            {
                throw new PlaywrightNativeException(MissingOptionsMessage);
            }

            if (arg != null)
            {
                script = EvaluateWithArg.Wrap(script, EvaluateCallbacks.DropFunctions(arg), throwOnFunctions: false);
            }
            else
            {
                script = EvaluateWithArg.InvokeIfFunction(script);
            }

            if (!script.EndsWith('\n'))
            {
                script += "\n";
            }

            return script;
        }

        /// <summary>
        /// WebKit accepts one bootstrap blob per target, so each registered script is
        /// wrapped in its own scope and joined with newlines.
        /// </summary>
        /// <param name="scripts">Registered init-script sources.</param>
        /// <returns>The combined <c>Page.setBootstrapScript</c> source.</returns>
        internal static string CombineBootstrap(IEnumerable<string> scripts)
        {
            StringBuilder builder = new();
            if (scripts != null)
            {
                foreach (string script in scripts)
                {
                    builder.Append("(() => { ").Append(script).Append(" })();\n");
                }
            }

            return builder.ToString();
        }

        /// <summary>
        /// Returns a disposable that invokes <paramref name="disposeAsync"/> once.
        /// </summary>
        /// <param name="disposeAsync">Unregister callback.</param>
        /// <returns>An <see cref="IAsyncDisposable"/> for the registered script.</returns>
        internal static IAsyncDisposable CreateDisposable(Func<Task> disposeAsync)
            => new InitScriptDisposable(disposeAsync);

        private sealed class InitScriptDisposable : IAsyncDisposable
        {
            private readonly Func<Task> _disposeAsync;
            private int _disposed;

            internal InitScriptDisposable(Func<Task> disposeAsync)
            {
                _disposeAsync = disposeAsync;
            }

            public ValueTask DisposeAsync()
            {
                if (Interlocked.Exchange(ref _disposed, 1) != 0 || _disposeAsync == null)
                {
                    return default;
                }

                return new ValueTask(_disposeAsync());
            }
        }
    }
}
