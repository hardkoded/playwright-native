/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
                    throw new PlaywrightNativeException("Must start tracing before stopping");
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
        Task<IAsyncDisposable> ITracing.GroupAsync(string name, TracingGroupOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task ITracing.StartChunkAsync(TracingStartChunkOptions options) => Task.CompletedTask;

        Task<IAsyncDisposable> ITracing.StartHarAsync(string path, TracingStartHarOptions options) => Task.FromResult<IAsyncDisposable>(default!);

        Task ITracing.StopChunkAsync(TracingStopChunkOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
