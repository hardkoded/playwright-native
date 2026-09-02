/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
            return Task.CompletedTask;
        }

        Task ITracing.StartAsync(Microsoft.Playwright.TracingStartOptions options)
        {
            OfficialTraceSession session = OfficialSession();
            session.Start(MicrosoftOptionsBridge.ToTracingStartOptions(options), chunk: false);
            return Task.CompletedTask;
        }

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
        public Task<IAsyncDisposable> StartHarAsync(string path, HarContentPolicy content = default, HarMode mode = default, string url = default, Regex urlRegex = default, string resourcesDir = default)
            => _har.StartAsync(path, content, mode, url, urlRegex, resourcesDir);

        /// <inheritdoc/>
        public Task GroupAsync(string name)
        {
            OfficialTraceSession official = OfficialSessionOrNull();
            if (official != null && official.IsRecording)
            {
                official.Group(name);
                return Task.CompletedTask;
            }

            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("name must be non-empty", nameof(name));
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
                return Task.CompletedTask;
            }

            lock (_gate)
            {
                if (!_recording)
                {
                    return Task.CompletedTask;
                }

                if (_openGroups.Count == 0)
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
                await official.StopAsync(path, keepRecording: true).ConfigureAwait(false);
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
        public Task StopAsync(TracingStopOptions options = default)
        {
            OfficialTraceSession session = OfficialSessionOrNull();
            if (session == null)
            {
                if (options?.Path != null)
                {
                    throw new PlaywrightNativeException("Must start tracing before stopping");
                }

                return Task.CompletedTask;
            }

            return session.StopAsync(options?.Path, keepRecording: false);
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
        Task<IAsyncDisposable> ITracing.GroupAsync(string name, TracingGroupOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task ITracing.StartChunkAsync(TracingStartChunkOptions options) => Task.CompletedTask;

        Task<IAsyncDisposable> ITracing.StartHarAsync(string path, TracingStartHarOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task ITracing.StopChunkAsync(TracingStopChunkOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
