using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightSharp.Transport.Protocol
{
    /// <summary>
    /// Represents an error returned in a protocol response.
    /// </summary>
    public class ProtocolError
    {
        /// <summary>
        /// Gets or sets the error message.
        /// </summary>
        [JsonPropertyName("message")]
        public string Message { get; set; }

        /// <summary>
        /// Gets or sets additional error data.
        /// </summary>
        [JsonPropertyName("data")]
        public JsonElement? Data { get; set; }

        /// <summary>
        /// Gets or sets the error code.
        /// </summary>
        [JsonPropertyName("code")]
        public int? Code { get; set; }
    }
}
