/*
 * Copyright (c) 2020 Darío Kondratiuk
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
using System.Globalization;
using System.IO;
using System.IO.Pipes;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Transport;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Handles launching Firefox browsers with correct default arguments,
    /// establishing a pipe connection to the Juggler endpoint, and
    /// creating an <see cref="FFBrowser"/> instance.
    /// Firefox <c>-juggler-pipe</c> talks on inherited file descriptors 3 (read)
    /// and 4 (write) — the same convention as WebKit's inspector pipe — not
    /// stdin/stdout.
    /// </summary>
    internal static class FirefoxBrowserType
    {
        private static readonly string[] DefaultArgs =
        [
            "-no-remote",
        ];

        /// <summary>
        /// Builds the list of command-line arguments for launching Firefox.
        /// </summary>
        /// <param name="headless">Whether to launch in headless mode.</param>
        /// <param name="additionalArgs">Optional additional arguments to append.</param>
        /// <returns>The complete list of arguments.</returns>
        internal static List<string> GetDefaultArgs(bool headless = true, string[] additionalArgs = null)
        {
            List<string> args = new(DefaultArgs);

            if (headless)
            {
                args.Add("-headless");
            }
            else
            {
                args.Add("-wait-for-browser");
                args.Add("-foreground");
            }

            // Juggler pipe transport — protocol JSON on inherited FDs 3/4.
            args.Add("-juggler-pipe");
            args.Add("-silent");

            if (additionalArgs != null)
            {
                args.AddRange(additionalArgs);
            }

            return args;
        }

        /// <summary>
        /// Writes official Playwright <c>firefoxUserPrefs</c> into <c>user.js</c> so
        /// Firefox copies them into <c>prefs.js</c> on startup.
        /// </summary>
        /// <param name="profileDir">Firefox profile directory.</param>
        /// <param name="firefoxUserPrefs">Preference name/value pairs, or <see langword="null"/>.</param>
        internal static void WriteUserPrefs(string profileDir, IEnumerable<KeyValuePair<string, object>> firefoxUserPrefs)
        {
            if (string.IsNullOrEmpty(profileDir) || firefoxUserPrefs == null)
            {
                return;
            }

            Directory.CreateDirectory(profileDir);
            List<string> lines = new();
            foreach (KeyValuePair<string, object> pref in firefoxUserPrefs)
            {
                if (string.IsNullOrEmpty(pref.Key))
                {
                    continue;
                }

                string keyJson = JsonSerializer.Serialize(pref.Key);
                string valueJson = SerializePrefValue(pref.Value);
                lines.Add("user_pref(" + keyJson + ", " + valueJson + ");");
            }

            File.WriteAllText(Path.Combine(profileDir, "user.js"), string.Join("\n", lines));
        }

        /// <summary>
        /// Launches a Firefox browser process and connects to it via pipe transport.
        /// </summary>
        /// <param name="executablePath">Path to the Firefox executable.</param>
        /// <param name="headless">Whether to launch in headless mode. Defaults to <c>true</c>.</param>
        /// <param name="args">Optional additional command-line arguments.</param>
        /// <param name="timeout">Launch timeout in milliseconds. Defaults to 30000.</param>
        /// <param name="environment">Optional extra environment variables for the browser process.</param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
        /// <param name="handleSIGINT">When <see langword="true"/>, close the browser on Ctrl-C.</param>
        /// <param name="handleSIGTERM">When <see langword="true"/>, close the browser on SIGTERM.</param>
        /// <param name="handleSIGHUP">When <see langword="true"/>, close the browser on SIGHUP.</param>
        /// <param name="userDataDir">Optional persistent Firefox profile directory.</param>
        /// <param name="persistent">When <see langword="true"/>, attach the default profile context.</param>
        /// <param name="deleteUserDataDirOnClose">When <see langword="true"/>, delete <paramref name="userDataDir"/> on exit.</param>
        /// <param name="firefoxUserPrefs">Optional Firefox <c>about:config</c> preferences written to <c>user.js</c>.</param>
        /// <returns>A connected <see cref="FFBrowser"/> instance.</returns>
        internal static async Task<FFBrowser> LaunchAsync(
            string executablePath,
            bool headless = true,
            string[] args = null,
            int timeout = 30_000,
            IReadOnlyDictionary<string, string> environment = null,
            ILoggerFactory loggerFactory = null,
            bool handleSIGINT = true,
            bool handleSIGTERM = true,
            bool handleSIGHUP = true,
            string userDataDir = null,
            bool persistent = false,
            bool deleteUserDataDirOnClose = false,
            IEnumerable<KeyValuePair<string, object>> firefoxUserPrefs = null)
        {
            List<string> launchArgs = GetDefaultArgs(headless, args);

            // Use the caller-provided profile for LaunchPersistentContext, otherwise
            // a temporary directory that BrowserProcessManager deletes on exit.
            string tempProfileDir = null;
            string profileDir;
            if (!string.IsNullOrEmpty(userDataDir))
            {
                Directory.CreateDirectory(userDataDir);
                profileDir = userDataDir;
                if (deleteUserDataDirOnClose)
                {
                    tempProfileDir = userDataDir;
                }
            }
            else
            {
                tempProfileDir = Path.Combine(Path.GetTempPath(), "playwright_firefox_" + Path.GetRandomFileName());
                Directory.CreateDirectory(tempProfileDir);
                profileDir = tempProfileDir;
            }

            launchArgs.Add("-profile");
            launchArgs.Add(profileDir);
            WriteUserPrefs(profileDir, firefoxUserPrefs);

            // nsRemoteDebuggingPipe reads FD 3 and writes FD 4. Same inheritable
            // pair + shell remap as WebKit. Talking on stdin/stdout instead
            // closes the Juggler session (TargetClosedException: Session disposed).
            AnonymousPipeServerStream childReads = new(PipeDirection.Out, HandleInheritability.Inheritable);
            AnonymousPipeServerStream childWrites = new(PipeDirection.In, HandleInheritability.Inheritable);

            PipeTransport transport = null;
            FFConnection connection = null;
            BrowserProcessManager processManager = null;
            StringBuilder logBuffer = new();
            TaskCompletionSource<bool> readyTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);

            try
            {
                processManager = new BrowserProcessManager(
                    executablePath,
                    launchArgs,
                    transportMode: TransportMode.PipeFd34,
                    tempUserDataDir: tempProfileDir,
                    timeout: timeout,
                    loggerFactory: loggerFactory,
                    inheritablePipes: new InheritablePipes(childReads, childWrites),
                    environment: environment,
                    handleSIGINT: handleSIGINT,
                    handleSIGTERM: handleSIGTERM,
                    handleSIGHUP: handleSIGHUP);

                // dump() of the ready banner can land on stdout or stderr.
                processManager.Process.StartInfo.RedirectStandardOutput = true;
                void OnLogLine(string line)
                {
                    if (string.IsNullOrEmpty(line))
                    {
                        return;
                    }

                    lock (logBuffer)
                    {
                        logBuffer.AppendLine(line);
                    }

                    if (line.Contains("Juggler listening to the pipe", StringComparison.Ordinal))
                    {
                        readyTcs.TrySetResult(true);
                    }
                }

                processManager.Process.ErrorDataReceived += (_, e) => OnLogLine(e.Data);
                processManager.Process.OutputDataReceived += (_, e) => OnLogLine(e.Data);
                processManager.Process.Exited += (_, _) =>
                    readyTcs.TrySetException(new PlaywrightNativeException("Firefox exited before Juggler reported ready."));

                await processManager.StartAsync().ConfigureAwait(false);

                childReads.DisposeLocalCopyOfClientHandle();
                childWrites.DisposeLocalCopyOfClientHandle();

                processManager.Process.BeginOutputReadLine();

                using CancellationTokenSource readyCts = new(timeout);
                readyCts.Token.Register(
                    () => readyTcs.TrySetException(
                        new PlaywrightNativeException(
                            $"Timed out after {timeout} ms waiting for Juggler to listen on the pipe.")));
                await readyTcs.Task.ConfigureAwait(false);

                transport = new PipeTransport(childReads, childWrites, loggerFactory);
                connection = new FFConnection(transport, loggerFactory);

                FFBrowser browser;
                try
                {
                    browser = await FFBrowser.ConnectAsync(connection, transport, processManager, loggerFactory, persistent)
                        .ConfigureAwait(false);
                }
                catch (TargetClosedException ex)
                {
                    await Task.Delay(100).ConfigureAwait(false);

                    string logs;
                    lock (logBuffer)
                    {
                        logs = logBuffer.ToString();
                    }

                    bool exited;
                    int? exitCode = null;
                    try
                    {
                        exited = processManager.Process.HasExited;
                        if (exited)
                        {
                            exitCode = processManager.Process.ExitCode;
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        exited = false;
                    }

                    throw new PlaywrightNativeException(
                        $"Firefox Juggler failed to stay connected (processExited={exited}, exitCode={exitCode?.ToString() ?? "<n/a>"}).\n" +
                        $"Executable: {executablePath}\n" +
                        $"Args: {string.Join(" ", launchArgs)}\n" +
                        $"Logs:\n{(logs.Length == 0 ? "<empty>" : logs)}",
                        ex);
                }

                processManager = null;
                connection = null;
                transport = null;
                childReads = null;
                childWrites = null;

                return browser;
            }
            finally
            {
                connection?.Dispose();

                if (transport != null)
                {
                    await transport.CloseAsync().ConfigureAwait(false);
                    transport.Dispose();
                }

                if (childReads != null)
                {
                    await childReads.DisposeAsync().ConfigureAwait(false);
                }

                if (childWrites != null)
                {
                    await childWrites.DisposeAsync().ConfigureAwait(false);
                }

                if (processManager != null)
                {
                    try
                    {
                        await processManager.KillAsync().ConfigureAwait(false);
                    }
                    finally
                    {
                        processManager.Dispose();
                    }
                }
            }
        }

        /// <summary>
        /// Sends a <c>Browser.close</c> command on the transport using the special
        /// <see cref="FFConnection.KBrowserCloseMessageId"/> so the connection ignores
        /// the response. Used as the graceful-close callback for the browser process.
        /// </summary>
        /// <param name="transport">The connection transport to send the close message on.</param>
        internal static Task AttemptToGracefullyCloseBrowserAsync(IConnectionTransport transport)
        {
            var request = new ProtocolRequest
            {
                Id = FFConnection.KBrowserCloseMessageId,
                Method = "Browser.close",
            };

            return transport.SendAsync(request);
        }

        private static string SerializePrefValue(object value)
        {
            if (value is bool flag)
            {
                return flag ? "true" : "false";
            }

            if (value is string text)
            {
                return JsonSerializer.Serialize(text);
            }

            if (value is IFormattable formattable)
            {
                return formattable.ToString(null, CultureInfo.InvariantCulture);
            }

            return JsonSerializer.Serialize(value);
        }
    }
}
