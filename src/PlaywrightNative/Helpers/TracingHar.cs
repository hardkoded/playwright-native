/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>tracing.startHar</c> / <c>tracing.stopHar</c> on top of
    /// <see cref="HarRecorder"/>.
    /// </summary>
    internal sealed class TracingHar
    {
        private readonly IBrowserContext _context;
        private IAPIRequestContext _api;
        private bool _started;

        internal TracingHar(IBrowserContext context)
        {
            _context = context;
        }

        internal void AttachApi(IAPIRequestContext api) => _api = api;

        internal Task<IAsyncDisposable> StartAsync(string path, HarContentPolicy content = default, HarMode mode = default, string url = default, Regex urlRegex = default, string resourcesDir = default)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("path must be non-empty", nameof(path));
            }

            if (!string.IsNullOrEmpty(resourcesDir)
                && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("resourcesDir option is not compatible with a .zip har file");
            }

            if (_started)
            {
                throw new PlaywrightNativeException("HAR recording has already been started");
            }

            IAPIRequestContext api = _api ?? (_context != null ? APIRequestContext.For(_context) : null);
            HarContentPolicy effective = content;
            if (effective == EnumCompat.UndefinedHarContentPolicy
                && path.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                effective = HarContentPolicy.Attach;
            }

            if (_context != null)
            {
                HarRecorder.StartTracing(_context, api, path, mode, effective, url, urlRegex, resourcesDir);
            }
            else
            {
                HarRecorder.StartApi(api, path, mode, effective, url, urlRegex, resourcesDir);
            }

            _started = true;
            return Task.FromResult<IAsyncDisposable>(new StopOnDispose(this));
        }

        internal async Task StopAsync()
        {
            if (!_started)
            {
                throw new PlaywrightNativeException("HAR recording has not been started");
            }

            _started = false;
            IAPIRequestContext api = _api ?? (_context != null ? APIRequestContext.For(_context) : null);
            await HarRecorder.FlushTracingAsync(_context, api).ConfigureAwait(false);
        }

        private sealed class StopOnDispose : IAsyncDisposable
        {
            private readonly TracingHar _owner;

            internal StopOnDispose(TracingHar owner)
            {
                _owner = owner;
            }

            public ValueTask DisposeAsync() => new ValueTask(_owner.StopAsync());
        }
    }
}
