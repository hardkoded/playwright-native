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
using System.Diagnostics;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;

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
            catch (PlaywrightException ex)
            {
                throw new PlaywrightException("clock." + method + ": " + ex.Message, ex);
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

                // pauseAt/runFor/fastForward await embedder.setTimeout inside _runTo.
                // On Darwin WebKit that never fires while awaitPromise holds the
                // protocol evaluate — defer those calls off a microtask.
                bool deferAwaitPromise = string.Equals(method, "pauseAt", StringComparison.Ordinal)
                    || string.Equals(method, "runFor", StringComparison.Ordinal)
                    || string.Equals(method, "fastForward", StringComparison.Ordinal);
                await EvaluateOnPagesAsync(call, deferAwaitPromise).ConfigureAwait(false);
            }
            catch (PlaywrightException ex)
            {
                if (ex.Message != null && ex.Message.StartsWith("clock.", StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightException("clock." + method + ": " + ex.Message, ex);
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
            await EvaluateOnPagesAsync(injector, deferAwaitPromise: false).ConfigureAwait(false);
        }

        private string BrowserName()
        {
            try
            {
                return _context.Browser?.BrowserType?.Name;
            }
            catch (PlaywrightException)
            {
                return null;
            }
        }

        private async Task EvaluateOnPagesAsync(string script, bool deferAwaitPromise)
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
                    if (deferAwaitPromise)
                    {
                        await EvaluateClockScriptAsync(
                                expression => page.EvaluateAsync(expression),
                                expression => page.EvaluateAsync<string>(expression),
                                script)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await page.EvaluateAsync(script).ConfigureAwait(false);
                    }

                    continue;
                }

                foreach (IFrame frame in frames)
                {
                    if (deferAwaitPromise)
                    {
                        await EvaluateClockScriptAsync(
                                expression => frame.EvaluateAsync(expression),
                                expression => frame.EvaluateAsync<string>(expression),
                                script)
                            .ConfigureAwait(false);
                    }
                    else
                    {
                        await frame.EvaluateAsync(script).ConfigureAwait(false);
                    }
                }
            }
        }

        /// <summary>
        /// Runs a clock controller expression without holding WIP
        /// <c>awaitPromise</c> across embedder timers.
        /// </summary>
        /// <remarks>
        /// <c>pauseAt</c> / <c>runFor</c> / <c>_runTo</c> / <c>_callFirstTimer</c>
        /// await <c>embedder.setTimeout(0)</c>. On Darwin WebKit, native timers
        /// do not fire while a protocol evaluate is blocked on
        /// <c>awaitPromise</c>, so a direct <c>EvaluateAsync(controller.pauseAt(...))</c>
        /// deadlocks until the NUnit timeout. Kick the work off a microtask,
        /// return synchronously, then poll a completion marker so timers can run.
        /// </remarks>
        private async Task EvaluateClockScriptAsync(
            Func<string, Task<JsonElement?>> evaluateAsync,
            Func<string, Task<string>> evaluateStringAsync,
            string script)
        {
            string marker = "__pwClockDone_" + Guid.NewGuid().ToString("N");
            string markerJson = JsonSerializer.Serialize(marker);

            // Fire-and-forget: schedule the (possibly async) controller call,
            // then return synchronously so WebKit does not hold awaitPromise
            // across embedder.setTimeout yields.
            //
            // Critically, avoid the substrings Promise. / .then( / await / async
            // in this source. WKPage.CanWrapExpression treats those as thenables
            // and forces EvaluateHandle + SerializeAwaitedJs (awaitPromise),
            // which deadlocks Darwin WebKit when the deferred work itself waits
            // on embedder timers — PauseAtShouldJumpAndStayFrozen flake.
            //
            // Prefer builtins.setTimeout(0) (macrotask) over queueMicrotask.
            // Darwin WIP may flush microtasks before completing Runtime.evaluate,
            // so a microtask that awaits embedder.setTimeout deadlocks inside the
            // same evaluate (ClockInstallOptionsTests 30s hangs on mac shard2).
            string kickoff =
                "(() => {" +
                "  const __pwK = " + markerJson + ";" +
                "  try { delete globalThis[__pwK]; } catch (e) {}" +
                "  const __pwDone = (ok, err) => {" +
                "    globalThis[__pwK] = ok ? 'ok' : ('err:' + String(err && (err.stack || err)));" +
                "  };" +
                "  const __pwGo = () => {" +
                "    try {" +
                "      const __pwR = (" + script + ");" +
                "      const __pwThen = __pwR && __pwR['then'];" +
                "      if (typeof __pwThen === 'function') {" +
                "        __pwThen.call(__pwR, () => __pwDone(true), (e) => __pwDone(false, e));" +
                "      } else {" +
                "        __pwDone(true);" +
                "      }" +
                "    } catch (e) {" +
                "      __pwDone(false, e);" +
                "    }" +
                "  };" +
                "  const __pwEmbed = (globalThis.__pwClock && globalThis.__pwClock.builtins)" +
                "    ? globalThis.__pwClock.builtins.setTimeout" +
                "    : globalThis.setTimeout;" +
                "  __pwEmbed(__pwGo, 0);" +
                "  return 0;" +
                "})()";

            await evaluateAsync(kickoff).ConfigureAwait(false);

            // Parenthesize so CanWrapExpression takes the sync returnByValue path.
            string poll = "(globalThis[" + markerJson + "])";
            Stopwatch sw = Stopwatch.StartNew();
            while (sw.ElapsedMilliseconds < 60_000)
            {
                string status = await evaluateStringAsync(poll).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(status))
                {
                    if (status.StartsWith("err:", StringComparison.Ordinal))
                    {
                        throw new PlaywrightException(status.Substring(4));
                    }

                    return;
                }

                // Give Darwin WebKit time to drain embedder timers between polls;
                // a 5ms hammer can starve setTimeout while pauseAt/_runTo awaits it.
                await Task.Delay(20).ConfigureAwait(false);
            }

            throw new PlaywrightException("clock: timed out waiting for controller command");
        }
    }
}
