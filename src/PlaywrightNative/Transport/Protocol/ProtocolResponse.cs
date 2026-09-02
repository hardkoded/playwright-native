using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightNative.Transport.Protocol
{
    /// <summary>
    /// Represents a protocol response received from the Playwright driver.
    /// </summary>
    public class ProtocolResponse
    {
        /// <summary>
        /// Gets or sets the request identifier this response corresponds to.
        /// </summary>
        [JsonPropertyName("id")]
        public int? Id { get; set; }

        /// <summary>
        /// Gets or sets the protocol method name for event messages.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// </summary>
        [JsonPropertyName("sessionId")]
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the error information, if the request failed.
        /// </summary>
        [JsonPropertyName("error")]
        public ProtocolError Error { get; set; }

        /// <summary>
        /// Gets or sets the event parameters.
        /// </summary>
        [JsonPropertyName("params")]
        public JsonElement? Params { get; set; }

        /// <summary>
        /// Gets or sets the result of a successful request.
        /// </summary>
        [JsonPropertyName("result")]
        public JsonElement? Result { get; set; }

        /// <summary>
        /// Gets or sets the page proxy identifier (WebKit-specific).
        /// </summary>
        [JsonPropertyName("pageProxyId")]
        public string PageProxyId { get; set; }

        /// <summary>
        /// Gets or sets the browser context identifier.
        /// </summary>
        [JsonPropertyName("browserContextId")]
        public string BrowserContextId { get; set; }
    }
}
