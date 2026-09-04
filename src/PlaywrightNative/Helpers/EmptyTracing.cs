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
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// In-memory <see cref="ITracing"/> for browsers without a Tracing domain.
    /// Records official group markers into the same Chrome JSON shape.
    /// </summary>
    internal sealed partial class EmptyTracing : ITracing
    {
        private readonly IBrowserContext _context;
        private readonly TracingHar _har;
        private readonly List<JsonElement> _events = new();
        private readonly Stack<string> _openGroups = new();
        private readonly object _gate = new();
        private OfficialTraceSession _ownOfficial;
        private bool _recording;

        internal EmptyTracing(IBrowserContext context = null)
        {
            _context = context;
            _har = new TracingHar(context);
        }

        /// <inheritdoc/>
        public Task StartAsync(PlaywrightNative.TracingStartOptions options)
        {
            if (_context is IHasOfficialTrace host)
            {
                host.OfficialTrace ??= new OfficialTraceSession(_context);
                host.OfficialTrace.Start(options, chunk: false);
                return Task.CompletedTask;
            }

            // Standalone APIRequest tracing has no browser context. Keep the lightweight
            // chrome-events recorder so StopAsync(path) writes JSON with traceEvents
            // (see ApiRequestTracingTests.StandaloneTracingGroupShouldBeWritten).
            if (_context == null)
            {
                return StartAsync(categories: null);
            }

            OwnOfficial().Start(options, chunk: false);
            return Task.CompletedTask;
        }

        Task ITracing.StartAsync(Microsoft.Playwright.TracingStartOptions options)
            => StartAsync(MicrosoftOptionsBridge.ToTracingStartOptions(options));

        /// <inheritdoc/>
        public Task StartAsync(string categories = null)
        {
            _ = categories;
            lock (_gate)
            {
                _recording = true;
                _events.Clear();
                _openGroups.Clear();
            }

            return Task.CompletedTask;
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
            return Task.CompletedTask;
        }

        /// <inheritdoc/>
        public Task<IAsyncDisposable> StartHarAsync(string path, HarContentPolicy content = default, HarMode mode = default, string url = default, Regex urlRegex = default, string resourcesDir = default)
            => EnsureHar().StartAsync(path, content, mode, url, urlRegex, resourcesDir);

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
        public Task StopChunkAsync(string path = default)
        {
            OfficialTraceSession official = OfficialSessionOrNull();
            if (official != null && official.IsRecording)
            {
                return official.StopAsync(path, keepRecording: true);
            }

            List<JsonElement> snapshot;
            lock (_gate)
            {
                snapshot = new List<JsonElement>(_events);
                _events.Clear();
            }

            if (string.IsNullOrEmpty(path))
            {
                return Task.CompletedTask;
            }

            return ChromeTraceEvents.WriteAsync(path, snapshot);
        }

        /// <inheritdoc/>
        public Task StopAsync(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return Task.CompletedTask;
            }

            List<JsonElement> snapshot;
            lock (_gate)
            {
                snapshot = new List<JsonElement>(_events);
                _recording = false;
            }

            return ChromeTraceEvents.WriteAsync(path, snapshot);
        }

        /// <inheritdoc/>
        public Task StopAsync(TracingStopOptions options = default)
        {
            OfficialTraceSession session = OfficialSessionOrNull();
            if (session == null)
            {
                if (options?.Path != null)
                {
                    return StopAsync(options.Path);
                }

                return Task.CompletedTask;
            }

            return session.StopAsync(options?.Path, keepRecording: false);
        }

        /// <inheritdoc/>
        public Task StopHarAsync() => EnsureHar().StopAsync();

        /// <summary>
        /// Binds standalone API HAR recording to <paramref name="api"/>.
        /// </summary>
        /// <param name="api">The request context that owns this tracer.</param>
        internal void AttachApi(IAPIRequestContext api) => _har?.AttachApi(api);

        /// <summary>
        /// Official action-trace session owned by this tracer (API request).
        /// </summary>
        /// <returns>The session when official API tracing is active.</returns>
        internal OfficialTraceSession OwnOfficialSession() => OfficialSessionOrNull();

        private OfficialTraceSession OwnOfficial()
        {
            _ownOfficial ??= new OfficialTraceSession(_context, apiOnly: true);
            return _ownOfficial;
        }

        private OfficialTraceSession OfficialSessionOrNull()
        {
            if (_ownOfficial != null)
            {
                return _ownOfficial;
            }

            if (_context is IHasOfficialTrace host)
            {
                return host.OfficialTrace;
            }

            return null;
        }

        private TracingHar EnsureHar()
        {
            if (_har == null)
            {
                throw new PlaywrightNativeException("HAR recording has not been started");
            }

            return _har;
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
                options?.Content ?? default,
                options?.Mode ?? default,
                options?.UrlFilter ?? options?.UrlFilterString,
                options?.UrlFilterRegex);

        Task ITracing.StopChunkAsync(TracingStopChunkOptions options)
            => StopChunkAsync(options?.Path);

        private sealed class GroupScope : IAsyncDisposable
        {
            private readonly EmptyTracing _owner;

            internal GroupScope(EmptyTracing owner) => _owner = owner;

            public ValueTask DisposeAsync() => new ValueTask(_owner.GroupEndAsync());
        }
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
