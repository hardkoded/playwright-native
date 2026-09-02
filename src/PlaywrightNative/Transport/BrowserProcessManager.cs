using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Transport
{
    /// <summary>
    /// Manages a browser process lifecycle, including startup, endpoint detection,
    /// graceful shutdown, and cleanup. Generalizes the old Chromium-specific process
    /// manager to support any browser (Chromium, Firefox, WebKit).
    /// </summary>
    internal class BrowserProcessManager : IDisposable
    {
        private static int _processCount;

        private readonly string _tempUserDataDir;
        private readonly int _timeout;
        private readonly Func<Task> _gracefulCloseCallback;
        private readonly Func<string, string> _endpointExtractor;
        private readonly ILogger _logger;
        private readonly TransportMode _transportMode;
        private readonly bool _handleSIGINT;
        private readonly bool _handleSIGTERM;
        private readonly bool _handleSIGHUP;
        private readonly ConsoleCancelEventHandler _cancelKeyPressHandler;
#if NET
        private readonly PosixSignalRegistration _sigtermRegistration;
        private readonly PosixSignalRegistration _sighupRegistration;
#endif
        private readonly TaskCompletionSource<string> _startCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly TaskCompletionSource<bool> _exitCompletionSource = new(TaskCreationOptions.RunContinuationsAsynchronously);
        private readonly InheritablePipes _inheritablePipes;
        private State _currentState = State.Initial;
        private bool _gracefullyClosing;

        /// <summary>
        /// Initializes a new instance of the <see cref="BrowserProcessManager"/> class.
        /// </summary>
        /// <param name="executablePath">Path to the browser executable.</param>
        /// <param name="args">Command-line arguments for the browser process.</param>
        /// <param name="transportMode">
        /// Which control-channel dialect the browser speaks. See <see cref="TransportMode"/>.
        /// </param>
        /// <param name="tempUserDataDir">Optional temporary user data directory to clean up on exit.</param>
        /// <param name="timeout">Timeout in milliseconds for the browser to start and report its endpoint.</param>
        /// <param name="gracefulCloseCallback">Optional callback to attempt graceful shutdown before killing.</param>
        /// <param name="endpointExtractor">
        /// Optional function that receives a stderr line and returns the WebSocket endpoint if found,
        /// or <c>null</c> if the line does not contain the endpoint. When not provided, defaults to
        /// matching the Chromium pattern "DevTools listening on (ws://...)".
        /// </param>
        /// <param name="loggerFactory">Optional logger factory for diagnostic output.</param>
        /// <param name="inheritablePipes">
        /// The anonymous-pipe pair to wire onto file descriptors 3/4 in the child. Required when
        /// <paramref name="transportMode"/> is <see cref="TransportMode.PipeFd34"/>; ignored
        /// otherwise. The caller owns the streams and constructs the
        /// <see cref="PipeTransport"/> from them directly — this manager does not consume them.
        /// </param>
        /// <param name="environment">Optional extra environment variables for the browser process.</param>
        /// <param name="handleSIGINT">When <see langword="true"/>, close the browser on Ctrl-C.</param>
        /// <param name="handleSIGTERM">When <see langword="true"/>, close the browser on SIGTERM.</param>
        /// <param name="handleSIGHUP">When <see langword="true"/>, close the browser on SIGHUP.</param>
        public BrowserProcessManager(
            string executablePath,
            IEnumerable<string> args,
            TransportMode transportMode = TransportMode.WebSocket,
            string tempUserDataDir = null,
            int timeout = 30_000,
            Func<Task> gracefulCloseCallback = null,
            Func<string, string> endpointExtractor = null,
            ILoggerFactory loggerFactory = null,
            InheritablePipes inheritablePipes = null,
            IReadOnlyDictionary<string, string> environment = null,
            bool handleSIGINT = true,
            bool handleSIGTERM = true,
            bool handleSIGHUP = true)
        {
            _tempUserDataDir = tempUserDataDir;
            _timeout = timeout;
            _gracefulCloseCallback = gracefulCloseCallback ?? (() => Task.CompletedTask);
            _endpointExtractor = endpointExtractor ?? DefaultEndpointExtractor;
            _logger = loggerFactory?.CreateLogger<BrowserProcessManager>();
            _transportMode = transportMode;
            _inheritablePipes = inheritablePipes;
            _handleSIGINT = handleSIGINT;
            _handleSIGTERM = handleSIGTERM;
            _handleSIGHUP = handleSIGHUP;
            _cancelKeyPressHandler = OnCancelKeyPress;

            if (transportMode == TransportMode.PipeFd34 && inheritablePipes == null)
            {
                throw new ArgumentNullException(
                    nameof(inheritablePipes),
                    $"{nameof(InheritablePipes)} must be supplied when {nameof(transportMode)} is {nameof(TransportMode.PipeFd34)}.");
            }

            string finalExecutable = executablePath;
            List<string> argList = args == null ? new List<string>() : new List<string>(args);

            bool redirectStdio = transportMode == TransportMode.PipeStdio;

            Process = new Process
            {
                EnableRaisingEvents = true,
                StartInfo =
                {
                    UseShellExecute = false,
                    FileName = finalExecutable,
                    RedirectStandardError = true,
                    RedirectStandardInput = redirectStdio,
                    RedirectStandardOutput = redirectStdio,
                },
            };

            if (transportMode == TransportMode.PipeFd34 &&
                !RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                string joinedArguments = JoinProcessArguments(argList);
                (finalExecutable, joinedArguments) = BuildFdRemapShellInvocation(executablePath, joinedArguments, inheritablePipes);
                Process.StartInfo.FileName = finalExecutable;
                Process.StartInfo.Arguments = joinedArguments;
            }
            else
            {
                // Keep each token intact so `--user-agent=I am Foo` is one argv entry.
                // Windows PipeFd34 uses the real binary; STARTUPINFOEX maps fds 3/4.
                foreach (string arg in argList)
                {
                    Process.StartInfo.ArgumentList.Add(arg);
                }
            }

            if (redirectStdio && !string.IsNullOrEmpty(executablePath))
            {
                string workDir = Path.GetDirectoryName(executablePath);
                if (!string.IsNullOrEmpty(workDir))
                {
                    Process.StartInfo.WorkingDirectory = workDir;
                }
            }

            if (environment != null)
            {
                foreach (KeyValuePair<string, string> pair in environment)
                {
                    if (!string.IsNullOrEmpty(pair.Key))
                    {
                        if (pair.Value == null)
                        {
                            Process.StartInfo.Environment.Remove(pair.Key);
                        }
                        else
                        {
                            Process.StartInfo.Environment[pair.Key] = pair.Value;
                        }
                    }
                }
            }

            if (_handleSIGINT)
            {
                Console.CancelKeyPress += _cancelKeyPressHandler;
            }

            if (_handleSIGTERM)
            {
#if NET
                try
                {
                    _sigtermRegistration = PosixSignalRegistration.Create(PosixSignal.SIGTERM, OnPosixSIGTERM);
                }
                catch (PlatformNotSupportedException)
                {
                }
#endif
            }

            if (_handleSIGHUP)
            {
#if NET
                try
                {
                    _sighupRegistration = PosixSignalRegistration.Create(PosixSignal.SIGHUP, OnPosixSIGHUP);
                }
                catch (PlatformNotSupportedException)
                {
                }
#endif
            }
        }

        /// <summary>
        /// Finalizes an instance of the <see cref="BrowserProcessManager"/> class.
        /// </summary>
        ~BrowserProcessManager()
        {
            Dispose(false);
        }

        /// <summary>
        /// Gets the underlying browser process.
        /// </summary>
        public Process Process { get; }

        /// <summary>
        /// Whether this manager closes the browser on Ctrl-C.
        /// </summary>
        public bool HandlesSIGINT => _handleSIGINT;

        /// <summary>
        /// Whether this manager closes the browser on SIGTERM.
        /// </summary>
        public bool HandlesSIGTERM => _handleSIGTERM;

        /// <summary>
        /// Whether this manager closes the browser on SIGHUP.
        /// </summary>
        public bool HandlesSIGHUP => _handleSIGHUP;

        /// <summary>
        /// Gets the WebSocket endpoint reported by the browser process during startup.
        /// Returns <c>null</c> if the process has not yet reported an endpoint.
        /// </summary>
#pragma warning disable VSTHRD002 // Safe: only accessed after task is confirmed completed
        public string Endpoint => _startCompletionSource.Task.IsCompleted
            ? _startCompletionSource.Task.Result
            : null;
#pragma warning restore VSTHRD002

        /// <summary>
        /// Gets the <see cref="PipeTransport"/> created for pipe-based browsers (Firefox).
        /// Only non-<c>null</c> after <see cref="StartAsync"/> has completed successfully
        /// when the manager was constructed with <c>usePipeTransport: true</c>.
        /// </summary>
        public PipeTransport PipeTransport { get; private set; }

        /// <summary>
        /// Gets a task that completes when the browser process exits.
        /// </summary>
        public Task ExitCompletionSource => _exitCompletionSource.Task;

        /// <summary>
        /// Starts the browser process and waits for it to report its WebSocket endpoint.
        /// </summary>
        /// <returns>A <see cref="Task"/> that completes when the browser process has started and reported its endpoint.</returns>
        public Task StartAsync() => _currentState.StartAsync(this);

        /// <summary>
        /// Kills the browser process immediately.
        /// </summary>
        /// <returns>A <see cref="Task"/> that completes when the process has been killed.</returns>
        public Task KillAsync() => _currentState.KillAsync(this);

        /// <summary>
        /// Attempts graceful shutdown of the browser process. If the process is already
        /// in the process of closing gracefully (reentrant call), falls back to kill.
        /// </summary>
        /// <returns>A <see cref="Task"/> that completes when the process has exited.</returns>
        public async Task GracefullyCloseAsync()
        {
            // We keep listeners until we are done, to handle 'exit' and 'SIGINT' while
            // asynchronously closing to prevent zombie processes. This might introduce
            // reentrancy to this function, for example user sends SIGINT second time.
            // In this case, let's forcefully kill the process.
            if (_gracefullyClosing)
            {
                await KillAsync().ConfigureAwait(false);
                return;
            }

            _gracefullyClosing = true;

            try
            {
                await _gracefulCloseCallback().ConfigureAwait(false);
            }
            catch
            {
                await KillAsync().ConfigureAwait(false);
            }

            await _exitCompletionSource.Task.ConfigureAwait(false);
        }

        /// <summary>
        /// Waits for the browser process to exit within the given timeout,
        /// then kills it if it has not exited.
        /// </summary>
        /// <param name="timeout">The maximum time to wait for a graceful exit.</param>
        /// <returns>A <see cref="Task"/> that completes when the exit or kill action is done.</returns>
        public Task EnsureExitAsync(TimeSpan? timeout) => timeout.HasValue
            ? _currentState.ExitAsync(this, timeout.Value)
            : _currentState.KillAsync(this);

        /// <inheritdoc/>
        public void Dispose()
        {
            if (_handleSIGINT)
            {
                Console.CancelKeyPress -= _cancelKeyPressHandler;
            }

#if NET
            _sigtermRegistration?.Dispose();
            _sighupRegistration?.Dispose();
#endif

            GC.SuppressFinalize(this);
            Dispose(true);
        }

        /// <summary>
        /// Default endpoint extractor that matches the Chromium DevTools protocol pattern.
        /// Also matches Firefox/Juggler endpoint patterns.
        /// </summary>
        /// <param name="line">A line from stderr.</param>
        /// <returns>The WebSocket endpoint string if found; otherwise <c>null</c>.</returns>
        private static string DefaultEndpointExtractor(string line)
        {
            // Chromium: "DevTools listening on ws://..."
            Match match = Regex.Match(line, "^DevTools listening on (ws:\\/\\/.*)");
            if (match.Success)
            {
                return match.Groups[1].Value;
            }

            return null;
        }

        private static string JoinProcessArguments(IReadOnlyList<string> args)
        {
            if (args == null || args.Count == 0)
            {
                return string.Empty;
            }

            string[] quoted = new string[args.Count];
            for (int i = 0; i < args.Count; i++)
            {
                quoted[i] = QuoteProcessArgument(args[i]);
            }

            return string.Join(" ", quoted);
        }

        private static string QuoteProcessArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            bool needsQuotes = false;
            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (c == ' ' || c == '\t' || c == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
            {
                return arg;
            }

            return "\"" + arg.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        /// <summary>
        /// Wraps the browser executable in a <c>/bin/bash -c</c> invocation that remaps the
        /// inherited pipe handles onto file descriptors 3 (child reads) and 4 (child writes),
        /// then <c>exec</c>s the real binary. WebKit's <c>--inspector-pipe</c> expects this layout.
        /// </summary>
        /// <param name="executablePath">The real browser binary to launch.</param>
        /// <param name="joinedArguments">Arguments to pass to the real browser, already space-joined.</param>
        /// <param name="pipes">The anonymous-pipe pair whose client handles will be remapped.</param>
        /// <returns>The <c>(executable, arguments)</c> pair to feed to <see cref="ProcessStartInfo"/>.</returns>
        private static (string Executable, string Arguments) BuildFdRemapShellInvocation(
            string executablePath,
            string joinedArguments,
            InheritablePipes pipes)
        {
            if (!RuntimeInformation.IsOSPlatform(OSPlatform.OSX) &&
                !RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                throw new PlaywrightNativeException(
                    "The bash fd-3/4 remap is only used on macOS and Linux. Windows uses STARTUPINFOEX.");
            }

            string shell = File.Exists("/bin/bash") ? "/bin/bash" : "/usr/bin/bash";
            string readHandle = pipes.ChildReads.GetClientHandleAsString();
            string writeHandle = pipes.ChildWrites.GetClientHandleAsString();
            string escapedExe = executablePath.Replace("\"", "\\\"");
            string escapedArgs = joinedArguments.Replace("\"", "\\\"");

            // exec 3<&{r} 4>&{w}  — duplicate inherited FDs onto 3/4 in the child.
            // {r}<&- {w}>&-       — close the originals so they don't leak.
            // exec "<exe>" <args> — replace the shell with the real browser binary.
            string script = $"exec 3<&{readHandle} 4>&{writeHandle} {readHandle}<&- {writeHandle}>&-; exec \\\"{escapedExe}\\\" {escapedArgs}";
            return (shell, $"-c \"{script}\"");
        }

        /// <summary>
        /// Firefox writes <c>Juggler listening to the pipe</c> on stdout, then uses
        /// that same stream for null-delimited protocol JSON. Read the banner first.
        /// </summary>
        /// <param name="stdout">The process stdout stream.</param>
        /// <param name="cancellationToken">Cancels when launch times out.</param>
        /// <returns>A task that completes when the ready banner has been consumed.</returns>
        private static async Task ConsumeJugglerReadyAsync(Stream stdout, CancellationToken cancellationToken)
        {
            if (stdout == null)
            {
                throw new ArgumentNullException(nameof(stdout));
            }

            List<byte> line = new();
            byte[] one = new byte[1];
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                int read = await stdout.ReadAsync(one.AsMemory(0, 1), cancellationToken).ConfigureAwait(false);
                if (read == 0)
                {
                    throw new PlaywrightNativeException("Firefox stdout closed before Juggler reported ready.");
                }

                if (one[0] == (byte)'\n')
                {
                    string text = Encoding.UTF8.GetString(line.ToArray()).TrimEnd('\r');
                    line.Clear();
                    if (text.Contains("Juggler listening to the pipe", StringComparison.Ordinal))
                    {
                        return;
                    }

                    continue;
                }

                line.Add(one[0]);
            }
        }

        private void OnCancelKeyPress(object sender, ConsoleCancelEventArgs e)
        {
            e.Cancel = true;
            _ = GracefullyCloseAsync();
        }

#if NET
        private void OnPosixSIGTERM(PosixSignalContext context)
        {
            context.Cancel = true;
            _ = GracefullyCloseAsync();
        }

        private void OnPosixSIGHUP(PosixSignalContext context)
        {
            context.Cancel = true;
            _ = GracefullyCloseAsync();
        }
#endif

        /// <summary>
        /// Starts the OS process. Unix PipeFd34 uses the bash fd remap already stored on
        /// <see cref="ProcessStartInfo"/>; Windows PipeFd34 uses STARTUPINFOEX so the child
        /// CRT sees fds 3 and 4.
        /// </summary>
        private void StartProcess()
        {
            if (_transportMode == TransportMode.PipeFd34 && OperatingSystem.IsWindows())
            {
                WindowsFd34ProcessLauncher.Start(Process, _inheritablePipes);
                return;
            }

            Process.Start();
        }

        private void CleanupTempUserDataDir()
        {
            if (_tempUserDataDir != null)
            {
                try
                {
                    Directory.Delete(_tempUserDataDir, true);
                }
                catch (Exception ex)
                {
                    _logger?.LogWarning(ex, "Failed to delete temporary user data directory: {Path}", _tempUserDataDir);
                }
            }
        }

        /// <summary>
        /// Disposes the browser process and any temporary user directory.
        /// </summary>
        /// <param name="disposing">Indicates whether disposal was initiated by <see cref="Dispose()"/>.</param>
        private void Dispose(bool disposing) => _currentState.Dispose(this);

        /// <summary>
        /// Represents a state machine for browser process instances. The happy path runs along the
        /// following state transitions: <see cref="Initial"/>
        /// -> <see cref="_starting"/>
        /// -> <see cref="_started"/>
        /// -> <see cref="_exiting"/>
        /// -> <see cref="_exited"/>
        /// -> <see cref="_disposed"/>.
        /// </summary>
        /// <remarks>
        /// <para>
        /// This state machine implements the following state transitions:
        /// <code>
        /// State     Event              Target State Action
        /// ======== =================== ============ ==========================================================
        /// Initial  --StartAsync------> Starting     Start process and wait for endpoint
        /// Initial  --ExitAsync-------> Exited       Cleanup temp user data
        /// Initial  --KillAsync-------> Exited       Cleanup temp user data
        /// Initial  --Dispose---------> Disposed     Cleanup temp user data
        /// Starting --StartAsync------> Starting     -
        /// Starting --ExitAsync-------> Exiting      Wait for process exit
        /// Starting --KillAsync-------> Killing      Kill process
        /// Starting --Dispose---------> Disposed     Kill process; Cleanup temp user data; throw ObjectDisposedException
        /// Starting --endpoint ready--> Started      Complete StartAsync successfully
        /// Starting --process exit----> Exited       Complete StartAsync with exception; Cleanup temp user data
        /// Started  --StartAsync------> Started      -
        /// Started  --ExitAsync-------> Exiting      Start exit timer
        /// Started  --KillAsync-------> Killing      Kill process
        /// Started  --Dispose---------> Disposed     Kill process; Cleanup temp user data; throw ObjectDisposedException
        /// Started  --process exit----> Exited       Cleanup temp user data
        /// Exiting  --StartAsync------> Exiting      - (throws InvalidOperationException)
        /// Exiting  --ExitAsync-------> Exiting      -
        /// Exiting  --KillAsync-------> Killing      Kill process
        /// Exiting  --Dispose---------> Disposed     Kill process; Cleanup temp user data; throw ObjectDisposedException
        /// Exiting  --exit timeout----> Killing      Kill process
        /// Exiting  --process exit----> Exited       Cleanup temp user data
        /// Killing  --StartAsync------> Killing      - (throws InvalidOperationException)
        /// Killing  --KillAsync-------> Killing      -
        /// Killing  --Dispose---------> Disposed     Cleanup temp user data; throw ObjectDisposedException
        /// Killing  --process exit----> Exited       Cleanup temp user data
        /// Exited   --StartAsync------> Exited       - (throws InvalidOperationException)
        /// Exited   --KillAsync-------> Exited       -
        /// Exited   --Dispose---------> Disposed     -
        /// Disposed --StartAsync------> Disposed     -
        /// Disposed --KillAsync-------> Disposed     -
        /// Disposed --Dispose---------> Disposed     -
        /// </code>
        /// </para>
        /// </remarks>
        private abstract class State
        {
            public static readonly State Initial = new InitialState();
            private static readonly StartingState _starting = new();
            private static readonly StartedState _started = new();
            private static readonly ExitingState _exiting = new();
            private static readonly KillingState _killing = new();
            private static readonly ExitedState _exited = new();
            private static readonly DisposedState _disposed = new();

            public bool IsExiting => this == _killing || this == _exiting;

            public bool IsExited => this == _exited || this == _disposed;

            /// <summary>
            /// Handles process start request.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            /// <returns>A <see cref="Task"/> that completes when the start action is done.</returns>
            public virtual Task StartAsync(BrowserProcessManager p) => Task.FromException(InvalidOperation("start"));

            /// <summary>
            /// Handles process exit request.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            /// <param name="timeout">The maximum waiting time for a graceful process exit.</param>
            /// <returns>A <see cref="Task"/> that completes when the exit action is done.</returns>
            public virtual Task ExitAsync(BrowserProcessManager p, TimeSpan timeout) => Task.FromException(InvalidOperation("exit"));

            /// <summary>
            /// Handles process kill request.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            /// <returns>A <see cref="Task"/> that completes when the kill action is done.</returns>
            public virtual Task KillAsync(BrowserProcessManager p) => Task.FromException(InvalidOperation("kill"));

            /// <summary>
            /// Handles wait for process exit request.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            /// <returns>A <see cref="Task"/> that completes when the wait finishes.</returns>
            public virtual Task WaitForExitAsync(BrowserProcessManager p) => p._exitCompletionSource.Task;

            /// <summary>
            /// Handles disposal of process and temporary user directory.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            public virtual void Dispose(BrowserProcessManager p) => _disposed.EnterFrom(p, this);

            public override string ToString()
            {
                string name = GetType().Name;
                return name.Substring(0, name.Length - "State".Length);
            }

            /// <summary>
            /// Attempts thread-safe transition from <paramref name="fromState"/> to this state.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            /// <param name="fromState">The state from which the transition takes place.</param>
            /// <returns><c>true</c> if the transition succeeded; <c>false</c> if the current state
            /// no longer equals <paramref name="fromState"/>.</returns>
            protected bool TryEnter(BrowserProcessManager p, State fromState)
            {
                if (Interlocked.CompareExchange(ref p._currentState, this, fromState) == fromState)
                {
                    fromState.Leave(p);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// Notifies that the state machine is about to transition to another state.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            protected virtual void Leave(BrowserProcessManager p)
            {
            }

            /// <summary>
            /// Kills the process if it is still alive.
            /// </summary>
            /// <param name="p">The browser process manager.</param>
            private static void Kill(BrowserProcessManager p)
            {
                try
                {
                    if (!p.Process.HasExited)
                    {
                        p.Process.Kill();
                    }
                }
                catch (InvalidOperationException)
                {
                    // Ignore — process may have already exited
                }
            }

            private Exception InvalidOperation(string operationName)
                => new InvalidOperationException($"Cannot {operationName} in state {this}");

            private class InitialState : State
            {
                public override Task StartAsync(BrowserProcessManager p) => _starting.EnterFromAsync(p, this);

                public override Task ExitAsync(BrowserProcessManager p, TimeSpan timeout)
                {
                    _exited.EnterFrom(p, this);
                    return Task.CompletedTask;
                }

                public override Task KillAsync(BrowserProcessManager p)
                {
                    _exited.EnterFrom(p, this);
                    return Task.CompletedTask;
                }

                public override Task WaitForExitAsync(BrowserProcessManager p) => Task.FromException(InvalidOperation("wait for exit"));
            }

            private class StartingState : State
            {
                public Task EnterFromAsync(BrowserProcessManager p, State fromState)
                {
                    if (!TryEnter(p, fromState))
                    {
                        // Delegate StartAsync to current state, because it has already changed since
                        // transition to this state was initiated.
                        return p._currentState.StartAsync(p);
                    }

                    return StartCoreAsync(p);
                }

                public override Task StartAsync(BrowserProcessManager p) => p._startCompletionSource.Task;

                public override Task ExitAsync(BrowserProcessManager p, TimeSpan timeout) => _exiting.EnterFromAsync(p, this, timeout);

                public override Task KillAsync(BrowserProcessManager p) => _killing.EnterFromAsync(p, this);

                public override void Dispose(BrowserProcessManager p)
                {
                    p._startCompletionSource.TrySetException(new ObjectDisposedException(p.ToString()));
                    base.Dispose(p);
                }

                private static async Task StartCoreAsync(BrowserProcessManager p)
                {
                    StringBuilder output = new StringBuilder();

                    void OnProcessDataReceivedWhileStarting(object sender, DataReceivedEventArgs e)
                    {
                        if (e.Data != null)
                        {
                            output.AppendLine(e.Data);
                            string endpoint = p._endpointExtractor(e.Data);
                            if (endpoint != null)
                            {
                                p._startCompletionSource.TrySetResult(endpoint);
                            }
                        }
                    }

                    void OnProcessExitedWhileStarting(object sender, EventArgs e)
                        => p._startCompletionSource.TrySetException(new PlaywrightNativeException(
                            BrowserTypeLaunchGuard.RewriteStartupLog($"Failed to launch browser! {output}")));

                    void OnProcessExited(object sender, EventArgs e) => _exited.EnterFrom(p, p._currentState);

                    p.Process.ErrorDataReceived += OnProcessDataReceivedWhileStarting;
                    p.Process.Exited += OnProcessExitedWhileStarting;
                    p.Process.Exited += OnProcessExited;
                    CancellationTokenSource cts = null;
                    try
                    {
                        p.StartProcess();

                        int timeout = p._timeout;
                        if (timeout > 0)
                        {
                            cts = new CancellationTokenSource(timeout);
                            cts.Token.Register(() => p._startCompletionSource.TrySetException(
                                new PlaywrightNativeException($"Timed out after {timeout} ms while trying to connect to the browser!")));
                        }

                        // PipeStdio (Firefox): the ready banner is written on stdout, then
                        // the same stream becomes the Juggler protocol pipe. Consume the
                        // banner before wrapping stdout.
                        // PipeFd34 (WebKit): the caller owns the AnonymousPipeServerStream pair
                        // and builds the transport externally — nothing to do here.
                        if (p._transportMode == TransportMode.PipeStdio)
                        {
                            await ConsumeJugglerReadyAsync(
                                p.Process.StandardOutput.BaseStream,
                                cts == null ? CancellationToken.None : cts.Token).ConfigureAwait(false);
                            p.PipeTransport = new PipeTransport(
                                p.Process.StandardInput.BaseStream,
                                p.Process.StandardOutput.BaseStream);
                            p._startCompletionSource.TrySetResult("pipe://");
                        }

                        await _started.EnterFromAsync(p, _starting).ConfigureAwait(false);

                        p.Process.BeginErrorReadLine();

                        // PipeFd34 has no stderr "ready" line to wait for — the inspector pipe
                        // is usable as soon as the process is alive.
                        if (p._transportMode == TransportMode.PipeFd34)
                        {
                            p._startCompletionSource.TrySetResult("pipe://");
                        }

                        try
                        {
                            await p._startCompletionSource.Task.ConfigureAwait(false);
                            await _started.EnterFromAsync(p, _starting).ConfigureAwait(false);
                        }
                        catch
                        {
                            await _killing.EnterFromAsync(p, _starting).ConfigureAwait(false);
                            throw;
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new PlaywrightNativeException(
                            string.IsNullOrEmpty(ex.Message) ? "Failed to launch browser" : ex.Message,
                            ex);
                    }
                    finally
                    {
                        cts?.Dispose();
                        p.Process.Exited -= OnProcessExitedWhileStarting;
                        p.Process.ErrorDataReceived -= OnProcessDataReceivedWhileStarting;
                    }
                }
            }

            private class StartedState : State
            {
                public Task EnterFromAsync(BrowserProcessManager p, State fromState)
                {
                    if (TryEnter(p, fromState))
                    {
                        // Process has not exited or been killed since transition to this state was initiated.
                        LogProcessCount(p, Interlocked.Increment(ref _processCount));
                    }

                    return Task.CompletedTask;
                }

                public override Task StartAsync(BrowserProcessManager p) => Task.CompletedTask;

                public override Task ExitAsync(BrowserProcessManager p, TimeSpan timeout) => _exiting.EnterFromAsync(p, this, timeout);

                public override Task KillAsync(BrowserProcessManager p) => _killing.EnterFromAsync(p, this);

                protected override void Leave(BrowserProcessManager p)
                    => LogProcessCount(p, Interlocked.Decrement(ref _processCount));

                private static void LogProcessCount(BrowserProcessManager p, int processCount)
                {
                    try
                    {
                        p._logger?.LogInformation("Process Count: {ProcessCount}", processCount);
                    }
                    catch
                    {
                        // Prevent logging exception from causing havoc
                    }
                }
            }

            private class ExitingState : State
            {
                public Task EnterFromAsync(BrowserProcessManager p, State fromState, TimeSpan timeout)
                    => !TryEnter(p, fromState) ? p._currentState.ExitAsync(p, timeout) : ExitAsync(p, timeout);

                public override async Task ExitAsync(BrowserProcessManager p, TimeSpan timeout)
                {
                    Task waitForExitTask = WaitForExitAsync(p);
                    await waitForExitTask.WithTimeout(
                        async () =>
                        {
                            await _killing.EnterFromAsync(p, this).ConfigureAwait(false);
                            await waitForExitTask.ConfigureAwait(false);
                        },
                        timeout,
                        CancellationToken.None).ConfigureAwait(false);
                }

                public override Task KillAsync(BrowserProcessManager p) => _killing.EnterFromAsync(p, this);
            }

            private class KillingState : State
            {
                public async Task EnterFromAsync(BrowserProcessManager p, State fromState)
                {
                    if (!TryEnter(p, fromState))
                    {
                        // Delegate KillAsync to current state, because it has already changed since
                        // transition to this state was initiated.
                        await p._currentState.KillAsync(p).ConfigureAwait(false);
                    }

                    try
                    {
                        if (!p.Process.HasExited)
                        {
                            p.Process.Kill();
                        }
                    }
                    catch (InvalidOperationException)
                    {
                        // Ignore — process may have already exited
                        return;
                    }

                    await WaitForExitAsync(p).ConfigureAwait(false);
                }

                public override Task ExitAsync(BrowserProcessManager p, TimeSpan timeout) => WaitForExitAsync(p);

                public override Task KillAsync(BrowserProcessManager p) => WaitForExitAsync(p);
            }

            private class ExitedState : State
            {
                public void EnterFrom(BrowserProcessManager p, State fromState)
                {
                    while (!TryEnter(p, fromState))
                    {
                        // Current state has changed since transition to this state was requested.
                        // Retry transition from the current state to ensure Leave() is properly called.
                        fromState = p._currentState;
                        if (fromState == this)
                        {
                            return;
                        }
                    }

                    p._exitCompletionSource.TrySetResult(true);
                    p.CleanupTempUserDataDir();
                }

                public override Task ExitAsync(BrowserProcessManager p, TimeSpan timeout) => Task.CompletedTask;

                public override Task KillAsync(BrowserProcessManager p) => Task.CompletedTask;

                public override Task WaitForExitAsync(BrowserProcessManager p) => Task.CompletedTask;
            }

            private class DisposedState : State
            {
                public void EnterFrom(BrowserProcessManager p, State fromState)
                {
                    if (!TryEnter(p, fromState))
                    {
                        // Delegate Dispose to current state, because it has already changed since
                        // transition to this state was initiated.
                        p._currentState.Dispose(p);
                    }
                    else if (fromState != _exited)
                    {
                        Kill(p);

                        p._exitCompletionSource.TrySetException(new ObjectDisposedException(p.ToString()));
                        p.CleanupTempUserDataDir();
                    }
                }

                public override Task StartAsync(BrowserProcessManager p) => throw new ObjectDisposedException(p.ToString());

                public override Task ExitAsync(BrowserProcessManager p, TimeSpan timeout) => throw new ObjectDisposedException(p.ToString());

                public override Task KillAsync(BrowserProcessManager p) => throw new ObjectDisposedException(p.ToString());

                public override void Dispose(BrowserProcessManager p)
                {
                    // Nothing to do — already disposed
                }
            }
        }
    }
}
