/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright <c>Clock</c>: injects <c>clockSource</c>, logs each
    /// command for new documents, and drives
    /// <c>globalThis.__pwClock.controller</c>.
    /// </summary>
    internal sealed partial class Clock : IClock
    {
        private readonly IBrowserContext _context;
        private readonly object _lock = new object();
        private bool _injectorInstalled;

        internal Clock(IBrowserContext context)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
        }

        /// <inheritdoc/>
        public Task InstallAsync()
            => InstallAsync(DateTimeOffset.UtcNow.ToUnixTimeMilliseconds());

        /// <inheritdoc/>
        public Task InstallAsync(long time)
            => ApplyAsync("install", ClockScript.ParseTime(time));

        /// <inheritdoc/>
        public Task InstallAsync(DateTime time)
            => InstallAsync(ClockScript.ToUnixMilliseconds(time));

        /// <inheritdoc/>
        public Task InstallAsync(string time)
            => WrapAsync("install", () => InstallAsync(ClockScript.ParseTime(time)));

        /// <inheritdoc/>
        public Task InstallAsync(ClockInstallOptions options)
        {
            if (options == null)
            {
                return InstallAsync();
            }

            if (options.TimeDate.HasValue)
            {
                return InstallAsync(options.TimeDate.Value);
            }

            string time = options.Time ?? options.TimeString;
            if (time != null)
            {
                return InstallAsync(time);
            }

            return InstallAsync();
        }

        /// <inheritdoc/>
        public Task PauseAtAsync(long time)
            => ApplyAsync("pauseAt", ClockScript.ParseTime(time));

        /// <inheritdoc/>
        public Task PauseAtAsync(DateTime time)
            => PauseAtAsync(ClockScript.ToUnixMilliseconds(time));

        /// <inheritdoc/>
        public Task PauseAtAsync(string time)
            => WrapAsync("pauseAt", () => PauseAtAsync(ClockScript.ParseTime(time)));

        /// <inheritdoc/>
        public Task ResumeAsync()
            => ApplyAsync("resume", null);

        /// <inheritdoc/>
        public Task FastForwardAsync(long ticks)
            => ApplyAsync("fastForward", ticks);

        /// <inheritdoc/>
        public Task FastForwardAsync(string ticks)
            => WrapAsync("fastForward", () => FastForwardAsync(ClockScript.ParseTicks(ticks)));

        /// <inheritdoc/>
        public Task RunForAsync(long ticks)
            => ApplyAsync("runFor", ticks);

        /// <inheritdoc/>
        public Task RunForAsync(double ticks)
            => ApplyRawAsync("runFor", ClockScript.FormatNumber(ticks));

        /// <inheritdoc/>
        public Task RunForAsync(string ticks)
            => WrapAsync("runFor", () => RunForAsync(ClockScript.ParseTicks(ticks)));

        /// <inheritdoc/>
        public Task SetFixedTimeAsync(long time)
            => ApplyAsync("setFixedTime", ClockScript.ParseTime(time));

        /// <inheritdoc/>
        public Task SetFixedTimeAsync(DateTime time)
            => SetFixedTimeAsync(ClockScript.ToUnixMilliseconds(time));

        /// <inheritdoc/>
        public Task SetFixedTimeAsync(string time)
            => WrapAsync("setFixedTime", () => SetFixedTimeAsync(ClockScript.ParseTime(time)));

        /// <inheritdoc/>
        public Task SetSystemTimeAsync(long time)
            => ApplyAsync("setSystemTime", ClockScript.ParseTime(time));

        /// <inheritdoc/>
        public Task SetSystemTimeAsync(DateTime time)
            => SetSystemTimeAsync(ClockScript.ToUnixMilliseconds(time));

        /// <inheritdoc/>
        public Task SetSystemTimeAsync(string time)
            => WrapAsync("setSystemTime", () => SetSystemTimeAsync(ClockScript.ParseTime(time)));

        private string Quote(string value)
            => JsonSerializer.Serialize(value);

        private Task WrapAsync(string method, Func<Task> action)
        {
            try
            {
                return action();
            }
            catch (PlaywrightNativeException ex)
            {
                throw new PlaywrightNativeException("clock." + method + ": " + ex.Message, ex);
            }
        }

        private Task ApplyAsync(string method, long? argument)
            => ApplyRawAsync(method, argument.HasValue ? ClockScript.FormatNumber(argument.Value) : null);

        private async Task ApplyRawAsync(string method, string argumentJs)
        {
            try
            {
                await EnsureInjectorAsync().ConfigureAwait(false);
                long wall = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                string log = argumentJs == null
                    ? "globalThis.__pwClock.controller.log(" + Quote(method) + ", " + ClockScript.FormatNumber(wall) + ")"
                    : "globalThis.__pwClock.controller.log(" + Quote(method) + ", " + ClockScript.FormatNumber(wall) + ", " + argumentJs + ")";
                await _context.AddInitScriptAsync(log).ConfigureAwait(false);
                string call = argumentJs == null
                    ? "globalThis.__pwClock.controller." + method + "()"
                    : "globalThis.__pwClock.controller." + method + "(" + argumentJs + ")";
                await EvaluateOnPagesAsync(call).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException ex)
            {
                if (ex.Message != null && ex.Message.StartsWith("clock.", StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightNativeException("clock." + method + ": " + ex.Message, ex);
            }
        }

        private async Task EnsureInjectorAsync()
        {
            bool install = false;
            lock (_lock)
            {
                if (!_injectorInstalled)
                {
                    _injectorInstalled = true;
                    install = true;
                }
            }

            if (!install)
            {
                return;
            }

            string injector = ClockScript.BuildInjector(BrowserName());
            await _context.AddInitScriptAsync(injector).ConfigureAwait(false);
            await EvaluateOnPagesAsync(injector).ConfigureAwait(false);
        }

        private string BrowserName()
        {
            try
            {
                return _context.Browser?.BrowserType?.Name;
            }
            catch (PlaywrightNativeException)
            {
                return null;
            }
        }

        private async Task EvaluateOnPagesAsync(string script)
        {
            IReadOnlyCollection<IPage> pages = _context.Pages;
            if (pages == null)
            {
                return;
            }

            foreach (IPage page in pages)
            {
                IReadOnlyCollection<IFrame> frames = page.Frames;
                if (frames == null || frames.Count == 0)
                {
                    await page.EvaluateAsync(script).ConfigureAwait(false);
                    continue;
                }

                foreach (IFrame frame in frames)
                {
                    await frame.EvaluateAsync(script).ConfigureAwait(false);
                }
            }
        }
    }
}
