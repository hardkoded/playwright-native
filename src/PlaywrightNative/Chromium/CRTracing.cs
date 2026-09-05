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
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Chromium performance tracing via the CDP <c>Tracing</c> domain.
    /// </summary>
    internal sealed partial class CRTracing : ITracing
    {
        private const string DefaultCategories = "devtools.timeline,v8.execute,blink.user_timing";

        private readonly CRSession _session;
        private readonly IBrowserContext _context;
        private readonly TracingHar _har;
        private readonly List<JsonElement> _events = new();
        private readonly Stack<string> _openGroups = new();
        private readonly object _gate = new();
        private TaskCompletionSource<bool> _complete;
        private bool _recording;

        internal CRTracing(CRSession session, IBrowserContext context)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _context = context;
            _har = new TracingHar(context);
        }

        /// <inheritdoc/>
        public Task StartAsync(PlaywrightNative.TracingStartOptions options)
        {
            OfficialTraceSession session = OfficialSession();
            session.Start(options, chunk: false);

            // Keep an in-memory chrome-events buffer for Direct StopAsync(.json) paths
            // (groups / non-empty traceEvents). Official zip remains for .zip stops.
            lock (_gate)
            {
                _recording = true;
                _events.Clear();
                _openGroups.Clear();
                _events.Add(ChromeTraceEvents.GroupBegin("__tracing__"));
                _events.Add(ChromeTraceEvents.GroupEnd("__tracing__"));
            }

            return Task.CompletedTask;
        }

        Task ITracing.StartAsync(Microsoft.Playwright.TracingStartOptions options)
            => StartAsync(MicrosoftOptionsBridge.ToTracingStartOptions(options));

        /// <inheritdoc/>
        public async Task StartAsync(string categories = null)
        {
            lock (_gate)
            {
                if (_recording)
                {
                    throw new PlaywrightNativeException("Tracing has already been started.");
                }

                _recording = true;
                _events.Clear();
                _openGroups.Clear();
                _complete = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
            }

            _session.MessageReceived += OnMessage;
            await _session.SendAsync("Tracing.start", new
            {
                transferMode = "ReportEvents",
                categories = string.IsNullOrEmpty(categories) ? DefaultCategories : categories,
            }).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task StartChunkAsync(string name = default, string title = default)
        {
            OfficialTraceSession official = OfficialSessionOrNull();
            if (official != null && official.IsRecording)
            {
                official.Start(new TracingStartOptions { Name = name, Title = title }, chunk: true);
                lock (_gate)
                {
                    _events.Clear();
                    _openGroups.Clear();
                    _recording = true;
                    _events.Add(ChromeTraceEvents.GroupBegin("__tracing__"));
                    _events.Add(ChromeTraceEvents.GroupEnd("__tracing__"));
                }

                return Task.CompletedTask;
            }

            _ = name;
            _ = title;
            lock (_gate)
            {
                if (!_recording)
                {
                    throw new PlaywrightNativeException("Tracing has not been started.");
                }

                _events.Clear();
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> StartHarAsync(string path, HarContentPolicy content = EnumCompat.UndefinedHarContentPolicy, HarMode mode = default, string url = default, Regex urlRegex = default, string resourcesDir = default)
            => _har.StartAsync(path, content, mode, url, urlRegex, resourcesDir);

        /// <inheritdoc/>
        public Task GroupAsync(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
            }

            OfficialTraceSession official = OfficialSessionOrNull();
            if (official != null && official.IsRecording)
            {
                official.Group(name);
            }

            lock (_gate)
            {
                if (!_recording)
                {
                    return Task.CompletedTask;
                }

                _openGroups.Push(name);
                _events.Add(ChromeTraceEvents.GroupBegin(name));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task GroupEndAsync()
        {
            OfficialTraceSession official = OfficialSessionOrNull();
            if (official != null && official.IsRecording)
            {
                official.GroupEnd();
            }

            lock (_gate)
            {
                if (!_recording || _openGroups.Count == 0)
                {
                    return Task.CompletedTask;
                }

                string name = _openGroups.Pop();
                _events.Add(ChromeTraceEvents.GroupEnd(name));
            }

            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public async Task StopChunkAsync(string path = default)
        {
            OfficialTraceSession official = OfficialSessionOrNull();
            if (official != null && official.IsRecording)
            {
                if (ChromeTraceEvents.IsJsonTracePath(path))
                {
                    await official.StopAsync(null, keepRecording: true).ConfigureAwait(false);
                    List<JsonElement> chunkSnapshot;
                    lock (_gate)
                    {
                        chunkSnapshot = new List<JsonElement>(_events);
                        _events.Clear();
                        _openGroups.Clear();
                        _recording = true;
                        _events.Add(ChromeTraceEvents.GroupBegin("__tracing__"));
                        _events.Add(ChromeTraceEvents.GroupEnd("__tracing__"));
                    }

                    await WriteTraceSnapshotAsync(path, chunkSnapshot).ConfigureAwait(false);
                    return;
                }

                await official.StopAsync(path, keepRecording: true).ConfigureAwait(false);
                lock (_gate)
                {
                    _events.Clear();
                }

                return;
            }

            List<JsonElement> snapshot;
            lock (_gate)
            {
                if (!_recording)
                {
                    throw new PlaywrightNativeException("Tracing has not been started.");
                }

                snapshot = new List<JsonElement>(_events);
                _events.Clear();
            }

            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            await WriteTraceSnapshotAsync(path, snapshot).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task StopAsync(TracingStopOptions options = default)
        {
            OfficialTraceSession session = OfficialSessionOrNull();
            if (session == null)
            {
                if (options?.Path != null)
                {
                    throw new PlaywrightNativeException("Must start tracing before stopping");
                }

                return;
            }

            string path = options?.Path;
            if (ChromeTraceEvents.IsJsonTracePath(path))
            {
                await session.StopAsync(null, keepRecording: false).ConfigureAwait(false);
                List<JsonElement> snapshot;
                lock (_gate)
                {
                    snapshot = new List<JsonElement>(_events);
                    _events.Clear();
                    _openGroups.Clear();
                    _recording = false;
                }

                await WriteTraceSnapshotAsync(path, snapshot).ConfigureAwait(false);
                return;
            }

            if (_recording)
            {
                // CDP may be active when StartAsync(categories) was used directly.
                await DiscardCdpTracingAsync().ConfigureAwait(false);
            }

            lock (_gate)
            {
                _events.Clear();
                _openGroups.Clear();
                _recording = false;
            }

            await session.StopAsync(path, keepRecording: false).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public async Task StopAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path must be non-empty", nameof(path));
            }

            TaskCompletionSource<bool> complete;
            lock (_gate)
            {
                if (!_recording)
                {
                    throw new PlaywrightNativeException("Tracing has not been started.");
                }

                complete = _complete;
            }

            try
            {
                await _session.SendAsync("Tracing.end").ConfigureAwait(false);
                await complete.Task.ConfigureAwait(false);
            }
            finally
            {
                _session.MessageReceived -= OnMessage;
                lock (_gate)
                {
                    _recording = false;
                }
            }

            List<JsonElement> snapshot;
            lock (_gate)
            {
                snapshot = new List<JsonElement>(_events);
            }

            await WriteTraceSnapshotAsync(path, snapshot).ConfigureAwait(false);
        }

        /// <inheritdoc/>
        public Task StopHarAsync() => _har.StopAsync();

        private OfficialTraceSession OfficialSession()
        {
            if (_context is IHasOfficialTrace host)
            {
                host.OfficialTrace ??= new OfficialTraceSession(_context);
                return host.OfficialTrace;
            }

            throw new PlaywrightNativeException("Tracing is not bound to a browser context.");
        }

        private OfficialTraceSession OfficialSessionOrNull()
        {
            return _context is IHasOfficialTrace host ? host.OfficialTrace : null;
        }

        private async Task DiscardCdpTracingAsync()
        {
            TaskCompletionSource<bool> complete;
            lock (_gate)
            {
                if (!_recording)
                {
                    return;
                }

                complete = _complete;
            }

            try
            {
                await _session.SendAsync("Tracing.end").ConfigureAwait(false);
                await complete.Task.ConfigureAwait(false);
            }
            finally
            {
                _session.MessageReceived -= OnMessage;
                lock (_gate)
                {
                    _recording = false;
                    _events.Clear();
                    _openGroups.Clear();
                }
            }
        }

        private async Task WriteTraceSnapshotAsync(string path, List<JsonElement> snapshot)
        {
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            using FileStream stream = File.Create(path);
            using Utf8JsonWriter writer = new(stream);
            writer.WriteStartObject();
            writer.WritePropertyName("traceEvents");
            writer.WriteStartArray();
            foreach (JsonElement item in snapshot)
            {
                item.WriteTo(writer);
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
            await writer.FlushAsync().ConfigureAwait(false);
        }

        private void OnMessage(string method, JsonElement? parameters)
        {
            if (!parameters.HasValue)
            {
                return;
            }

            if (method == "Tracing.dataCollected")
            {
                if (parameters.Value.TryGetProperty("value", out JsonElement value)
                    && value.ValueKind == JsonValueKind.Array)
                {
                    lock (_gate)
                    {
                        foreach (JsonElement item in value.EnumerateArray())
                        {
                            _events.Add(item.Clone());
                        }
                    }
                }

                return;
            }

            if (method == "Tracing.tracingComplete")
            {
                _complete?.TrySetResult(true);
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        async Task<IAsyncDisposable> ITracing.GroupAsync(string name, TracingGroupOptions options)
        {
            await GroupAsync(name).ConfigureAwait(false);
            return new GroupScope(this);
        }

        Task ITracing.StartChunkAsync(TracingStartChunkOptions options)
            => StartChunkAsync(options?.Name, options?.Title);

        Task<IAsyncDisposable> ITracing.StartHarAsync(string path, TracingStartHarOptions options)
            => StartHarAsync(
                path,
                options?.Content ?? EnumCompat.UndefinedHarContentPolicy,
                options?.Mode ?? default,
                options?.UrlFilter ?? options?.UrlFilterString,
                options?.UrlFilterRegex);

        Task ITracing.StopChunkAsync(TracingStopChunkOptions options)
            => StopChunkAsync(options?.Path);

        private sealed class GroupScope : IAsyncDisposable
        {
            private readonly CRTracing _owner;

            internal GroupScope(CRTracing owner) => _owner = owner;

            public ValueTask DisposeAsync() => new ValueTask(_owner.GroupEndAsync());
        }
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
