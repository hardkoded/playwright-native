namespace PlaywrightNative.Transport
{
    /// <summary>
    /// How <see cref="BrowserProcessManager"/> establishes a control channel with a launched
    /// browser process. Each browser engine speaks one of these dialects.
    /// </summary>
    internal enum TransportMode
    {
        /// <summary>
        /// The browser prints its DevTools WebSocket endpoint to stderr (e.g. Chromium's
        /// <c>DevTools listening on ws://...</c>). The caller then opens a
        /// <see cref="WebSocketTransport"/> against that URL.
        /// </summary>
        WebSocket,

        /// <summary>
        /// The browser communicates over a pipe wired to its <c>stdin</c>/<c>stdout</c>. The
        /// manager redirects both streams, builds a <see cref="PipeTransport"/> from them, and
        /// waits for a "ready" line on stderr (e.g. Firefox's
        /// <c>Juggler listening to the pipe</c>) before completing startup.
        /// </summary>
        PipeStdio,

        /// <summary>
        /// The browser communicates over a pipe wired to inherited file descriptors 3 (child
        /// reads) and 4 (child writes) — the convention WebKit's <c>--inspector-pipe</c>
        /// and Firefox Juggler's <c>-juggler-pipe</c> both require. The caller owns the
        /// <see cref="System.IO.Pipes.AnonymousPipeServerStream"/> pair and supplies it via
        /// <see cref="InheritablePipes"/>. On Unix the manager wraps the executable in a
        /// shell that remaps the inherited FDs onto 3/4; on Windows it uses STARTUPINFOEX
        /// + <c>lpReserved2</c> so the child CRT sees those descriptors. Startup completes as soon as the process is
        /// alive; Firefox additionally waits for <c>Juggler listening to the pipe</c> before
        /// sending protocol.
        /// </summary>
        PipeFd34,
    }
}
