using System;
using System.Threading.Tasks;
using PlaywrightSharp.Transport.Protocol;

namespace PlaywrightSharp.Transport
{
    /// <summary>
    /// Transport interface for communicating with the Playwright driver process.
    /// </summary>
    public interface IConnectionTransport : IDisposable
    {
        /// <summary>
        /// Gets or sets the callback invoked when a protocol message is received.
        /// </summary>
        Action<ProtocolResponse> OnMessage { get; set; }

        /// <summary>
        /// Gets or sets the callback invoked when the transport connection is closed.
        /// </summary>
        Action<string> OnClose { get; set; }

        /// <summary>
        /// Sends a protocol request to the Playwright driver.
        /// </summary>
        /// <param name="request">The protocol request to send.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous send operation.</returns>
        Task SendAsync(ProtocolRequest request);

        /// <summary>
        /// Closes the transport connection.
        /// </summary>
        /// <returns>A <see cref="Task"/> representing the asynchronous close operation.</returns>
        Task CloseAsync();
    }
}
