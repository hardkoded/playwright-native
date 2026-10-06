/*
 * Copyright (c) Microsoft Corporation.
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
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Collects istanbul <c>window.__coverage__</c> counters into the trace.
    /// Port of <c>packages/playwright-core/src/server/coverageRecorder.ts</c>.
    /// </summary>
    internal sealed class CoverageRecorder
    {
        private const string TakeName = "__pwCoverageTake";

        private const string TakeExpression = "window[\"" + TakeName + "\"] ? window[\"" + TakeName + "\"]() : []";

        private const string DisposeExpression = "window[\"" + TakeName + "\"]?.dispose()";

        private static readonly Lazy<string> Source = new(LoadSource);

        private readonly object _gate = new();
        private readonly IBrowserContext _context;
        private readonly string _sessionId;
        private readonly Dictionary<string, IstanbulFileCoverage> _coverage = new(StringComparer.Ordinal);
        private readonly HashSet<string> _stashedChunkIds = new(StringComparer.Ordinal);
        private PlaywrightException _stashError;
        private IAsyncDisposable _initScript;

        internal CoverageRecorder(IBrowserContext context, string sessionId)
        {
            _context = context;
            _sessionId = JsonSerializer.Serialize(sessionId);
        }

        internal async Task InstallAsync()
        {
            if (_initScript != null)
            {
                return;
            }

            // The WebKit setContent replays the init scripts on the written document,
            // a second instance would reset the counters the page already hit.
            string source = ModuleExpression("if (!window[" + JsonSerializer.Serialize(TakeName) + "]) new (module.exports.CoverageScript())(window, " + JsonSerializer.Serialize(TakeName) + ", " + _sessionId + ");");
            using (ActionTrace.SuppressRecording())
            {
                _initScript = await _context.AddInitScriptAsync(source).ConfigureAwait(false);
                await EvaluateInAllFramesAsync(source).ConfigureAwait(false);
            }
        }

        internal async Task UninstallAsync()
        {
            using (ActionTrace.SuppressRecording())
            {
                try
                {
                    await Task.WhenAll(
                        _initScript?.DisposeAsync().AsTask() ?? Task.CompletedTask,
                        EvaluateInAllFramesAsync(DisposeExpression)).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
            }
        }

        /// <summary>
        /// Returns the coverage collected since the last take, serialized, or
        /// <see langword="null"/> when there is none.
        /// </summary>
        /// <param name="discard">Drops the collected coverage without pulling.</param>
        /// <returns>The coverage JSON.</returns>
        internal async Task<string> TakeAsync(bool discard)
        {
            if (discard)
            {
                lock (_gate)
                {
                    _coverage.Clear();
                }

                return null;
            }

            await FlushAsync().ConfigureAwait(false);
            lock (_gate)
            {
                if (_stashError != null)
                {
                    throw _stashError;
                }

                _stashedChunkIds.Clear();
                if (_coverage.Count == 0)
                {
                    return null;
                }

                string json = IstanbulCoverage.Serialize(_coverage);
                _coverage.Clear();
                return json;
            }
        }

        internal async Task CollectFromPageAsync(IPage page)
        {
            if (page == null || page.IsClosed)
            {
                return;
            }

            using (ActionTrace.SuppressRecording())
            {
                await Task.WhenAll(page.Frames.Select(async frame =>
                {
                    foreach (string json in await TakeFromFrameAsync(frame, TakeExpression).ConfigureAwait(false))
                    {
                        Append(json);
                    }
                })).ConfigureAwait(false);
            }
        }

        private static string ModuleExpression(string call)
            => "(() => {\nconst module = {};\n" + Source.Value + "\n" + call + "\n})()";

        private static string LoadSource()
        {
            Assembly assembly = typeof(CoverageRecorder).Assembly;
            using Stream stream = assembly.GetManifestResourceStream("PlaywrightNative.Helpers.coverageScriptSource.js")
                ?? throw new PlaywrightException("Bundled Playwright coverage script source is missing.");
            using StreamReader reader = new StreamReader(stream);
            return reader.ReadToEnd();
        }

        private async Task FlushAsync()
        {
            await Task.WhenAll(_context.Pages.Select(CollectFromPageAsync)).ConfigureAwait(false);
            await HarvestOriginsWithoutPageAsync().ConfigureAwait(false);
        }

        private async Task HarvestOriginsWithoutPageAsync()
        {
            if (_context is not IHasStorageStateInternals internals || _context.IsClosed)
            {
                return;
            }

            HashSet<string> liveOrigins = new(StringComparer.Ordinal);
            foreach (IPage page in _context.Pages)
            {
                foreach (IFrame frame in page.Frames)
                {
                    if (StorageStateHelper.TryGetHttpOrigin(frame.Url, out string origin))
                    {
                        liveOrigins.Add(origin);
                    }
                }
            }

            // The origins that still have a page were drained by the sweep above.
            HashSet<string> origins = new(internals.VisitedOrigins.Where(origin => !liveOrigins.Contains(origin)), StringComparer.Ordinal);
            if (origins.Count == 0)
            {
                return;
            }

            string source = ModuleExpression("return (module.exports.takeCoverageStashes())(window, " + _sessionId + ");");
            using (ActionTrace.SuppressRecording())
            {
                await StorageStateHelper.VisitOriginsAsync(_context, origins, async (frame, _) =>
                {
                    foreach (string json in await TakeFromFrameAsync(frame, source).ConfigureAwait(false))
                    {
                        Append(json);
                    }
                }).ConfigureAwait(false);
            }
        }

        private async Task<string[]> TakeFromFrameAsync(IFrame frame, string expression)
        {
            try
            {
                return await frame.EvaluateAsync<string[]>(expression).ConfigureAwait(false) ?? Array.Empty<string>();
            }
            catch (PlaywrightException ex)
            {
                return TakeFailed(ex);
            }
            catch (TimeoutException ex)
            {
                return TakeFailed(ex);
            }
            catch (InvalidOperationException ex)
            {
                return TakeFailed(ex);
            }
        }

        private Task EvaluateInAllFramesAsync(string expression)
            => Task.WhenAll(_context.Pages.SelectMany(page => page.Frames).Select(async frame =>
            {
                try
                {
                    await frame.EvaluateAsync(expression).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
                catch (InvalidOperationException)
                {
                }
            }));

        // Evaluation fails for pages on their way out, only the stash error matters.
        private string[] TakeFailed(Exception error)
        {
            string message = error.Message ?? string.Empty;
            int start = message.IndexOf(IstanbulCoverage.StashError, StringComparison.Ordinal);
            if (start >= 0)
            {
                message = message.Substring(start).Split('\n')[0];
                lock (_gate)
                {
                    _stashError ??= new PlaywrightException(message);
                }
            }

            return Array.Empty<string>();
        }

        private void Append(string json)
        {
            IstanbulCoverageChunk chunk;
            try
            {
                chunk = IstanbulCoverage.ParseChunk(json);
            }
            catch (JsonException)
            {
                return;
            }

            if (chunk?.Data == null)
            {
                return;
            }

            lock (_gate)
            {
                if (chunk.Id != null && !_stashedChunkIds.Add(chunk.Id))
                {
                    return;
                }

                IstanbulCoverage.Merge(_coverage, chunk.Data);
            }
        }
    }
}
