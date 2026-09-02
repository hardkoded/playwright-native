using System.IO.Pipes;

namespace PlaywrightSharp.Transport
{
    /// <summary>
    /// A pair of anonymous pipes with inheritable client handles, supplied to
    /// <see cref="BrowserProcessManager"/> when launching a browser in
    /// <see cref="TransportMode.PipeFd34"/> mode. The caller owns the streams; the manager
    /// only reads the client handles. On Unix the manager remaps them with a bash wrapper;
    /// on Windows it inherits them as CRT fds 3 and 4 via STARTUPINFOEX.
    /// </summary>
    /// <param name="ChildReads">
    /// Pipe the child reads from — opened as <see cref="PipeDirection.Out"/> on the parent
    /// side, mapped onto file descriptor 3 in the child.
    /// </param>
    /// <param name="ChildWrites">
    /// Pipe the child writes to — opened as <see cref="PipeDirection.In"/> on the parent
    /// side, mapped onto file descriptor 4 in the child.
    /// </param>
    internal sealed record InheritablePipes(
        AnonymousPipeServerStream ChildReads,
        AnonymousPipeServerStream ChildWrites);
}
