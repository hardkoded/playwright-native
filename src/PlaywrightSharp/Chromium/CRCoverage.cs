/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightSharp.Chromium
{
    /// <summary>
    /// Chromium JS/CSS coverage via the Profiler and CSS CDP domains.
    /// </summary>
    internal sealed class CRCoverage : ICoverage
    {
        private readonly CRSession _session;
        private readonly ConcurrentDictionary<string, ScriptRecord> _scripts = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, StyleRecord> _styles = new(StringComparer.Ordinal);
        private bool _jsEnabled;
        private bool _cssEnabled;
        private bool _jsResetOnNavigation = true;
        private bool _jsReportAnonymousScripts;
        private bool _cssResetOnNavigation = true;

        internal CRCoverage(CRSession session)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _session.MessageReceived += OnMessage;
        }

        /// <inheritdoc/>
        public async Task StartJSCoverageAsync(bool resetOnNavigation = true, bool reportAnonymousScripts = false)
        {
            _jsResetOnNavigation = resetOnNavigation;
            _jsReportAnonymousScripts = reportAnonymousScripts;
            _scripts.Clear();
            await _session.SendAsync("Profiler.enable").ConfigureAwait(false);
            await _session.SendAsync("Profiler.startPreciseCoverage", new
            {
                callCount = true,
                detailed = true,
            }).ConfigureAwait(false);
            await _session.SendAsync("Debugger.enable").ConfigureAwait(false);
            await _session.SendAsync("Debugger.setSkipAllPauses", new { skip = true }).ConfigureAwait(false);
            _jsEnabled = true;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<JSCoverageEntry>> StopJSCoverageAsync()
        {
            if (!_jsEnabled)
            {
                return Array.Empty<JSCoverageEntry>();
            }

            _jsEnabled = false;
            JsonElement? response = null;
            try
            {
                response = await _session.SendAsync("Profiler.takePreciseCoverage").ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
            }

            try
            {
                await _session.SendAsync("Profiler.stopPreciseCoverage").ConfigureAwait(false);
                await _session.SendAsync("Profiler.disable").ConfigureAwait(false);
                await _session.SendAsync("Debugger.disable").ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }

            List<JSCoverageEntry> entries = new();
            HashSet<string> emitted = new(StringComparer.Ordinal);
            if (!response.HasValue
                || !response.Value.TryGetProperty("result", out JsonElement result)
                || result.ValueKind != JsonValueKind.Array)
            {
                AppendRemainingScripts(entries, emitted);
                return entries;
            }

            foreach (JsonElement item in result.EnumerateArray())
            {
                string scriptId = GetString(item, "scriptId");
                if (!string.IsNullOrEmpty(scriptId) && !_scripts.ContainsKey(scriptId))
                {
                    continue;
                }

                string url = GetString(item, "url") ?? string.Empty;
                string source = null;
                if (!string.IsNullOrEmpty(scriptId) && _scripts.TryGetValue(scriptId, out ScriptRecord script))
                {
                    if (string.IsNullOrEmpty(url))
                    {
                        url = script.Url ?? string.Empty;
                    }

                    source = script.Source;
                }

                if (IsAnonymousScript(url) && !_jsReportAnonymousScripts)
                {
                    continue;
                }

                List<JSCoverageFunction> functions = new();
                if (item.TryGetProperty("functions", out JsonElement functionsElement)
                    && functionsElement.ValueKind == JsonValueKind.Array)
                {
                    foreach (JsonElement function in functionsElement.EnumerateArray())
                    {
                        List<JSCoverageRange> ranges = new();
                        if (function.TryGetProperty("ranges", out JsonElement rangesElement)
                            && rangesElement.ValueKind == JsonValueKind.Array)
                        {
                            foreach (JsonElement range in rangesElement.EnumerateArray())
                            {
                                ranges.Add(new JSCoverageRange
                                {
                                    StartOffset = GetInt(range, "startOffset"),
                                    EndOffset = GetInt(range, "endOffset"),
                                    Count = GetInt(range, "count"),
                                });
                            }
                        }

                        functions.Add(new JSCoverageFunction
                        {
                            FunctionName = GetString(function, "functionName") ?? string.Empty,
                            Ranges = ranges,
                        });
                    }
                }

                entries.Add(new JSCoverageEntry
                {
                    Url = url,
                    ScriptId = scriptId,
                    Source = source,
                    Functions = functions,
                });
                if (!string.IsNullOrEmpty(scriptId))
                {
                    emitted.Add(scriptId);
                }
            }

            AppendRemainingScripts(entries, emitted);
            return entries;
        }

        /// <inheritdoc/>
        public async Task StartCSSCoverageAsync(bool resetOnNavigation = true)
        {
            _cssResetOnNavigation = resetOnNavigation;
            _styles.Clear();
            await _session.SendAsync("DOM.enable").ConfigureAwait(false);
            await _session.SendAsync("CSS.enable").ConfigureAwait(false);
            await _session.SendAsync("Runtime.enable").ConfigureAwait(false);
            await _session.SendAsync("CSS.startRuleUsageTracking").ConfigureAwait(false);
            _cssEnabled = true;
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<CSSCoverageEntry>> StopCSSCoverageAsync()
        {
            if (!_cssEnabled)
            {
                return Array.Empty<CSSCoverageEntry>();
            }

            _cssEnabled = false;
            JsonElement? response = null;
            try
            {
                response = await _session.SendAsync("CSS.stopRuleUsageTracking").ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
            }

            try
            {
                await _session.SendAsync("CSS.disable").ConfigureAwait(false);
                await _session.SendAsync("DOM.disable").ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }

            Dictionary<string, List<UsageRange>> used = new(StringComparer.Ordinal);
            if (response.HasValue
                && response.Value.TryGetProperty("ruleUsage", out JsonElement coverage)
                && coverage.ValueKind == JsonValueKind.Array)
            {
                foreach (JsonElement rule in coverage.EnumerateArray())
                {
                    string styleSheetId = GetString(rule, "styleSheetId");
                    if (string.IsNullOrEmpty(styleSheetId))
                    {
                        continue;
                    }

                    bool isUsed = rule.TryGetProperty("used", out JsonElement usedElement)
                        && usedElement.ValueKind == JsonValueKind.True;
                    if (!used.TryGetValue(styleSheetId, out List<UsageRange> ranges))
                    {
                        ranges = new List<UsageRange>();
                        used[styleSheetId] = ranges;
                    }

                    ranges.Add(new UsageRange
                    {
                        StartOffset = GetInt(rule, "startOffset"),
                        EndOffset = GetInt(rule, "endOffset"),
                        Count = isUsed ? 1 : 0,
                    });
                }
            }

            List<CSSCoverageEntry> entries = new();
            foreach (KeyValuePair<string, StyleRecord> pair in _styles)
            {
                used.TryGetValue(pair.Key, out List<UsageRange> nested);
                entries.Add(new CSSCoverageEntry
                {
                    Url = pair.Value.Url ?? string.Empty,
                    Text = pair.Value.Text,
                    Ranges = ConvertToDisjointRanges(nested),
                });
            }

            return entries;
        }

        private static string GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                ? property.GetString()
                : null;
        }

        private static int GetInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                && property.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static bool IsAnonymousScript(string url)
            => string.IsNullOrEmpty(url);

        private void AppendRemainingScripts(List<JSCoverageEntry> entries, HashSet<string> emitted)
        {
            foreach (KeyValuePair<string, ScriptRecord> pair in _scripts)
            {
                if (emitted.Contains(pair.Key))
                {
                    continue;
                }

                string url = pair.Value.Url ?? string.Empty;
                if (IsAnonymousScript(url) && !_jsReportAnonymousScripts)
                {
                    continue;
                }

                entries.Add(new JSCoverageEntry
                {
                    Url = url,
                    ScriptId = pair.Key,
                    Source = pair.Value.Source,
                    Functions = Array.Empty<JSCoverageFunction>(),
                });
            }
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            if (method == "Debugger.paused")
            {
                if (_jsEnabled)
                {
                    _ = ResumeDebuggerAsync();
                }

                return;
            }

            if (method == "Runtime.executionContextsCleared")
            {
                if (_jsEnabled && _jsResetOnNavigation)
                {
                    _scripts.Clear();
                }

                if (_cssEnabled && _cssResetOnNavigation)
                {
                    _styles.Clear();
                }

                return;
            }

            if (!parameters.HasValue)
            {
                return;
            }

            if (method == "Debugger.scriptParsed")
            {
                OnScriptParsed(parameters.Value);
            }
            else if (method == "CSS.styleSheetAdded")
            {
                OnStyleSheetAdded(parameters.Value);
            }
        }

        private void OnScriptParsed(JsonElement payload)
        {
            string scriptId = GetString(payload, "scriptId");
            if (string.IsNullOrEmpty(scriptId))
            {
                return;
            }

            string url = GetString(payload, "url") ?? string.Empty;
            if (IsAnonymousScript(url) && !_jsReportAnonymousScripts)
            {
                return;
            }

            ScriptRecord record = new()
            {
                Url = url,
            };
            _scripts[scriptId] = record;
            _ = FetchScriptSourceAsync(scriptId, record);
        }

        private void OnStyleSheetAdded(JsonElement payload)
        {
            if (!payload.TryGetProperty("header", out JsonElement header))
            {
                return;
            }

            string styleSheetId = GetString(header, "styleSheetId");
            if (string.IsNullOrEmpty(styleSheetId))
            {
                return;
            }

            // Official cssCoverage ignores anonymous / inspector-injected sheets.
            string sourceUrl = GetString(header, "sourceURL");
            if (string.IsNullOrEmpty(sourceUrl))
            {
                return;
            }

            StyleRecord record = new()
            {
                Url = sourceUrl,
            };
            _styles[styleSheetId] = record;
            _ = FetchStyleSheetTextAsync(styleSheetId, record);
        }

        private async Task ResumeDebuggerAsync()
        {
            try
            {
                await _session.SendAsync("Debugger.resume").ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }
        }

        private async Task FetchScriptSourceAsync(string scriptId, ScriptRecord record)
        {
            try
            {
                JsonElement? response = await _session.SendAsync(
                    "Debugger.getScriptSource",
                    new { scriptId }).ConfigureAwait(false);
                if (response.HasValue)
                {
                    record.Source = GetString(response.Value, "scriptSource");
                }
            }
            catch (TargetClosedException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }
        }

        private async Task FetchStyleSheetTextAsync(string styleSheetId, StyleRecord record)
        {
            try
            {
                JsonElement? response = await _session.SendAsync(
                    "CSS.getStyleSheetText",
                    new { styleSheetId }).ConfigureAwait(false);
                if (response.HasValue)
                {
                    record.Text = GetString(response.Value, "text");
                }
            }
            catch (TargetClosedException)
            {
            }
            catch (PlaywrightSharpException)
            {
            }
        }

        private IReadOnlyList<CSSCoverageRange> ConvertToDisjointRanges(List<UsageRange> nestedRanges)
        {
            if (nestedRanges == null || nestedRanges.Count == 0)
            {
                return Array.Empty<CSSCoverageRange>();
            }

            List<ScanPoint> points = new(nestedRanges.Count * 2);
            for (int i = 0; i < nestedRanges.Count; i++)
            {
                UsageRange range = nestedRanges[i];
                points.Add(new ScanPoint { Offset = range.StartOffset, Type = 0, Range = range });
                points.Add(new ScanPoint { Offset = range.EndOffset, Type = 1, Range = range });
            }

            points.Sort(static (left, right) =>
            {
                int offset = left.Offset.CompareTo(right.Offset);
                if (offset != 0)
                {
                    return offset;
                }

                if (left.Type != right.Type)
                {
                    return right.Type.CompareTo(left.Type);
                }

                int leftLength = left.Range.EndOffset - left.Range.StartOffset;
                int rightLength = right.Range.EndOffset - right.Range.StartOffset;
                return left.Type == 0
                    ? rightLength.CompareTo(leftLength)
                    : leftLength.CompareTo(rightLength);
            });

            List<int> hitCountStack = new();
            List<CSSCoverageRange> results = new();
            int lastOffset = 0;
            for (int i = 0; i < points.Count; i++)
            {
                ScanPoint point = points[i];
                if (hitCountStack.Count > 0
                    && lastOffset < point.Offset
                    && hitCountStack[hitCountStack.Count - 1] > 0)
                {
                    CSSCoverageRange last = results.Count > 0 ? results[results.Count - 1] : null;
                    if (last != null && last.End == lastOffset)
                    {
                        last.End = point.Offset;
                    }
                    else
                    {
                        results.Add(new CSSCoverageRange { Start = lastOffset, End = point.Offset });
                    }
                }

                lastOffset = point.Offset;
                if (point.Type == 0)
                {
                    hitCountStack.Add(point.Range.Count);
                }
                else if (hitCountStack.Count > 0)
                {
                    hitCountStack.RemoveAt(hitCountStack.Count - 1);
                }
            }

            List<CSSCoverageRange> kept = new();
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i].End - results[i].Start > 1)
                {
                    kept.Add(results[i]);
                }
            }

            return kept;
        }

        private sealed class UsageRange
        {
            internal int StartOffset { get; set; }

            internal int EndOffset { get; set; }

            internal int Count { get; set; }
        }

        private sealed class ScanPoint
        {
            internal int Offset { get; set; }

            internal int Type { get; set; }

            internal UsageRange Range { get; set; }
        }

        private sealed class ScriptRecord
        {
            internal string Url { get; set; }

            internal string Source { get; set; }
        }

        private sealed class StyleRecord
        {
            internal string Url { get; set; }

            internal string Text { get; set; }
        }
    }
}
