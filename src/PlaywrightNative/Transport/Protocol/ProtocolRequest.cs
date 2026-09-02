using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightNative.Transport.Protocol
{
    /// <summary>
    /// Represents a protocol request sent to the Playwright driver.
    /// </summary>
    public class ProtocolRequest
    {
        /// <summary>
        /// Gets or sets the request identifier.
        /// </summary>
        [JsonPropertyName("id")]
        public int Id { get; set; }

        /// <summary>
        /// Gets or sets the protocol method name.
        /// </summary>
        [JsonPropertyName("method")]
        public string Method { get; set; }

        /// <summary>
        /// Gets or sets the method parameters.
        /// Omitted when null so browsers that reject <c>"params":null</c> stay happy.
        /// </summary>
        [JsonPropertyName("params")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public JsonElement? Params { get; set; }

        /// <summary>
        /// Gets or sets the session identifier.
        /// Omitted from the serialized payload for the root session so Juggler
        /// (and other browsers) do not see an empty <c>sessionId</c>.
        /// </summary>
        [JsonPropertyName("sessionId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string SessionId { get; set; }

        /// <summary>
        /// Gets or sets the page proxy identifier (WebKit-specific).
        /// Omitted from the serialized payload when null so non-WebKit transports are unaffected.
        /// </summary>
        [JsonPropertyName("pageProxyId")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
        public string PageProxyId { get; set; }
    }
}
