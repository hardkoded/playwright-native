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
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Records official Playwright action-trace titles for page APIs.
    /// </summary>
    internal static class ActionTrace
    {
        private static readonly AsyncLocal<int> Depth = new AsyncLocal<int>();

        private static readonly AsyncLocal<int> Suppress = new AsyncLocal<int>();

        internal static IDisposable SuppressRecording()
        {
            Suppress.Value++;
            return new SuppressScope();
        }

        internal static Task RunAsync(IBrowserContext context, string title, string className, string method, Func<Task> body, object parameters = null, object result = null)
        {
            OfficialTraceSession session = OfficialTraceSession.Active(context);
            if (session == null || Suppress.Value > 0 || Depth.Value > 0)
            {
                return body();
            }

            return RunInsideAsync(() => session.RecordActionAsync(title, className, method, body, parameters, result));
        }

        internal static Task<T> RunAsync<T>(IBrowserContext context, string title, string className, string method, Func<Task<T>> body, object parameters = null, object result = null)
        {
            OfficialTraceSession session = OfficialTraceSession.Active(context);
            if (session == null || Suppress.Value > 0 || Depth.Value > 0)
            {
                return body();
            }

            return RunInsideAsync(() => session.RecordActionAsync(title, className, method, body, parameters, result));
        }

        internal static Task<T> EvaluateUserAsync<T>(IBrowserContext context, Func<Task<T>> body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            OfficialTraceSession session = OfficialTraceSession.Active(context);
            if (session == null || Depth.Value > 0)
            {
                return body();
            }

            return RunInsideAsync(() => session.RecordActionAsync(null, "Page", "evaluate", body));
        }

        internal static Task<T> EvaluateHandleUserAsync<T>(IBrowserContext context, Func<Task<T>> body)
        {
            if (body == null)
            {
                throw new ArgumentNullException(nameof(body));
            }

            OfficialTraceSession session = OfficialTraceSession.Active(context);
            if (session == null || Depth.Value > 0)
            {
                return body();
            }

            return RunInsideAsync(() => session.RecordActionAsync(null, "Page", "evaluateHandle", body));
        }

        internal static string NavigateTitle(string url)
        {
            if (string.IsNullOrEmpty(url))
            {
                return "Navigate";
            }

            if (Uri.TryCreate(url, UriKind.Absolute, out Uri uri))
            {
                return "Navigate " + uri.PathAndQuery;
            }

            return "Navigate " + url;
        }

        internal static string ClickTitle(string selector)
            => "Click " + LocatorLabel(selector);

        internal static string HoverTitle(string selector)
            => "Hover " + LocatorLabel(selector);

        internal static string SetInputFilesTitle(string selector)
            => "Set input files " + LocatorLabel(selector);

        internal static string LocatorLabel(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return "locator('unknown')";
            }

            if (selector.Length >= 2 && selector[0] == '"' && selector[selector.Length - 1] == '"')
            {
                return "locator('text=" + selector + "')";
            }

            return "locator('" + selector.Replace("'", "\\'", StringComparison.Ordinal) + "')";
        }

        private static async Task RunInsideAsync(Func<Task> body)
        {
            // Yield first so Depth is not set on the caller's ExecutionContext
            // (AsyncLocal sync-portion leak would suppress sibling waitForEvent
            // recording while an in-flight evaluate is pending).
            await Task.Yield();
            Depth.Value++;
            try
            {
                await body().ConfigureAwait(false);
            }
            finally
            {
                Depth.Value--;
            }
        }

        private static async Task<T> RunInsideAsync<T>(Func<Task<T>> body)
        {
            await Task.Yield();
            Depth.Value++;
            try
            {
                return await body().ConfigureAwait(false);
            }
            finally
            {
                Depth.Value--;
            }
        }

        private sealed class SuppressScope : IDisposable
        {
            public void Dispose()
            {
                if (Suppress.Value > 0)
                {
                    Suppress.Value--;
                }
            }
        }
    }
}
