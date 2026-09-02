/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Pipes;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Helpers;
using PlaywrightNative.Transport;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Launches a WebKit browser process and establishes a pipe transport for the
    /// WebKit Inspector Protocol. WebKit's <c>--inspector-pipe</c> flag expects the
    /// browser to communicate over file descriptors 3 (read) and 4 (write) — not
    /// stdin/stdout. We use <see cref="AnonymousPipeServerStream"/> with inheritable
    /// handles. Unix remaps those handles onto fds 3/4 with a shell wrapper;
    /// Windows uses STARTUPINFOEX + the MSVC CRT <c>lpReserved2</c> buffer.
    /// </summary>
    /// <remarks>
    /// The inheritable-pipe + Unix shell-wrapper approach is borrowed from the
    /// webdriverbidi-net project. Windows handle inheritance follows Node/libuv
    /// (<c>STARTUPINFOEX</c> / <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c>).
    /// </remarks>
    internal static class WebkitBrowserType
    {
        /// <summary>
        /// Builds the command-line arguments for launching WebKit. Mirrors upstream
        /// <c>webkit.ts</c>: <c>--inspector-pipe</c>, headless mode flag, and a Win32-only
        /// <c>--disable-accelerated-compositing</c> workaround.
        /// </summary>
        /// <param name="headless">Whether to launch in headless mode.</param>
        /// <param name="additionalArgs">Optional extra arguments.</param>
        /// <param name="userDataDir">Persistent user data directory, or <see langword="null"/>.</param>
        /// <returns>The argument list.</returns>
        internal static List<string> GetDefaultArgs(bool headless = true, string[] additionalArgs = null, string userDataDir = null)
        {
            List<string> args = new()
            {
                "--inspector-pipe",
            };

            if (string.IsNullOrEmpty(userDataDir))
            {
                args.Add("--no-startup-window");
            }

            if (headless)
            {
                args.Add("--headless");
            }

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                args.Add("--disable-accelerated-compositing");
            }

            if (!string.IsNullOrEmpty(userDataDir))
            {
                args.Add("--user-data-dir=" + userDataDir);
            }

            if (additionalArgs != null)
            {
                args.AddRange(additionalArgs);
            }

            if (!string.IsNullOrEmpty(userDataDir))
            {
                args.Add("about:blank");
            }

            return args;
        }

        /// <summary>
        /// Launches a WebKit process and connects via pipe transport.
        /// </summary>
        /// <param name="executablePath">Path to the WebKit launcher (e.g. <c>pw_run.sh</c> on Unix).</param>
        /// <param name="headless">Whether to launch in headless mode. Defaults to <c>true</c>.</param>
        /// <param name="args">Additional command-line arguments.</param>
        /// <param name="proxy">Optional process-level proxy (<c>--proxy</c>).</param>
        /// <param name="timeout">Launch timeout in milliseconds.</param>
        /// <param name="environment">Optional extra environment variables for the browser process.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        /// <param name="userDataDir">Optional persistent user data directory.</param>
        /// <param name="persistent">When <see langword="true"/>, attach a default context.</param>
        /// <param name="deleteUserDataDirOnClose">When <see langword="true"/>, delete <paramref name="userDataDir"/> on exit.</param>
        /// <param name="handleSIGINT">When <see langword="true"/>, close the browser on Ctrl-C.</param>
        /// <param name="handleSIGTERM">When <see langword="true"/>, close the browser on SIGTERM.</param>
        /// <param name="handleSIGHUP">When <see langword="true"/>, close the browser on SIGHUP.</param>
        /// <returns>A connected <see cref="WKBrowser"/>.</returns>
        internal static async Task<WKBrowser> LaunchAsync(
            string executablePath,
            bool headless = true,
            string[] args = null,
            Proxy proxy = null,
            int timeout = 30_000,
            IReadOnlyDictionary<string, string> environment = null,
            ILoggerFactory loggerFactory = null,
            string userDataDir = null,
            bool persistent = false,
            bool deleteUserDataDirOnClose = false,
            bool handleSIGINT = true,
            bool handleSIGTERM = true,
            bool handleSIGHUP = true)
        {
            if (string.IsNullOrEmpty(executablePath))
            {
                throw new ArgumentException("WebKit executable path is required.", nameof(executablePath));
            }

            List<string> launchArgs = GetDefaultArgs(headless, args, userDataDir);
            string proxyServer = ProxySettings.FormatServer(proxy, includeCredentials: true);
            if (!string.IsNullOrEmpty(proxyServer))
            {
                // Official webkit.ts launch args: macOS --proxy-bypass-list,
                // Linux one --ignore-host per token, Windows --curl-noproxy.
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                {
                    launchArgs.Add("--proxy=" + proxyServer);
                    if (!string.IsNullOrEmpty(proxy.Bypass))
                    {
                        launchArgs.Add("--proxy-bypass-list=" + proxy.Bypass);
                    }
                }
                else if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                {
                    string curlProxy = proxyServer.StartsWith("socks5://", StringComparison.OrdinalIgnoreCase)
                        ? string.Concat("socks5h://", proxyServer.AsSpan("socks5://".Length))
                        : proxyServer;
                    launchArgs.Add("--curl-proxy=" + curlProxy);
                    if (!string.IsNullOrEmpty(proxy.Bypass))
                    {
                        launchArgs.Add("--curl-noproxy=" + proxy.Bypass);
                    }
                }
                else
                {
                    launchArgs.Add("--proxy=" + proxyServer);
                    if (!string.IsNullOrEmpty(proxy.Bypass))
                    {
                        foreach (string token in proxy.Bypass.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
                        {
                            launchArgs.Add("--ignore-host=" + token);
                        }
                    }
                }
            }

            string tempUserDataDir = deleteUserDataDirOnClose ? userDataDir : null;

            // AnonymousPipeServerStream pair:
            //   childReads  — direction Out (we write, child reads at FD 3)
            //   childWrites — direction In  (we read, child writes at FD 4)
            // Inheritable handles let the bash shell wrapper map them onto FD 3/4.
            AnonymousPipeServerStream childReads = new(PipeDirection.Out, HandleInheritability.Inheritable);
            AnonymousPipeServerStream childWrites = new(PipeDirection.In, HandleInheritability.Inheritable);

            PipeTransport transport = null;
            WKConnection connection = null;
            BrowserProcessManager processManager = null;

            // Buffer stderr from the browser process so a startup failure can surface what
            // the binary actually said. The BrowserProcessManager only captures stderr during
            // its own state-machine startup window — we want a wider net for diagnostics.
            StringBuilder stderrBuffer = new();

            try
            {
                processManager = new BrowserProcessManager(
                    executablePath,
                    launchArgs,
                    transportMode: TransportMode.PipeFd34,
                    timeout: timeout,
                    loggerFactory: loggerFactory,
                    inheritablePipes: new InheritablePipes(childReads, childWrites),
                    environment: environment,
                    tempUserDataDir: tempUserDataDir,
                    handleSIGINT: handleSIGINT,
                    handleSIGTERM: handleSIGTERM,
                    handleSIGHUP: handleSIGHUP);

                processManager.Process.ErrorDataReceived += (_, e) =>
                {
                    if (e.Data == null)
                    {
                        return;
                    }

                    lock (stderrBuffer)
                    {
                        stderrBuffer.AppendLine(e.Data);
                    }
                };

                await processManager.StartAsync().ConfigureAwait(false);

                // The child has inherited the handles via fork(). Dropping our local copies
                // ensures only the child holds the read/write ends, so the pipes close cleanly
                // when the child exits.
                childReads.DisposeLocalCopyOfClientHandle();
                childWrites.DisposeLocalCopyOfClientHandle();

                transport = new PipeTransport(childReads, childWrites, loggerFactory);
                connection = new WKConnection(transport, loggerFactory);

                WKBrowser browser;
                try
                {
                    browser = await WKBrowser
                        .ConnectAsync(connection, processManager, loggerFactory, persistent)
                        .ConfigureAwait(false);
                }
                catch (TargetClosedException ex)
                {
                    // Give stderr a beat to drain anything still in flight, then build a
                    // diagnostic message that names the process exit state and the captured
                    // log. This is the only signal we have when the inspector pipe shuts
                    // before our first command lands.
                    await Task.Delay(100).ConfigureAwait(false);

                    string stderr;
                    lock (stderrBuffer)
                    {
                        stderr = stderrBuffer.ToString();
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
                        $"Failed to launch WebKit (processExited={exited}, exitCode={exitCode?.ToString() ?? "<n/a>"}).\n" +
                        $"Executable: {executablePath}\n" +
                        $"Args: {string.Join(" ", launchArgs)}\n" +
                        $"Stderr:\n{(stderr.Length == 0 ? "<empty>" : stderr)}",
                        ex);
                }

                // Ownership transferred. The browser now owns the connection (and via it the
                // transport + process manager); leave the streams alive — closing them would
                // tear down the protocol channel.
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
    }
}
