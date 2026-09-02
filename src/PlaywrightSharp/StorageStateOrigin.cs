using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace PlaywrightSharp
{
    /// <summary>
    /// See <see cref="StorageState.Origins"/>.
    /// </summary>
    internal class StorageStateOrigin : IEquatable<StorageStateOrigin>
    {
        /// <summary>
        /// Origin.
        /// </summary>
        [JsonPropertyName("origin")]
        public string Origin { get; set; }

        /// <summary>
        /// A concrete URL on <see cref="Origin"/> used to restore localStorage
        /// when navigating to the origin itself fails (no document at <c>/</c>).
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; }

        /// <summary>
        /// Local storage.
        /// </summary>
        [JsonPropertyName("localStorage")]
        public ICollection<NameValueEntry> LocalStorage { get; set; } = new List<NameValueEntry>();

        /// <summary>
        /// IndexedDB databases for this origin, when collected with
        /// <see cref="IBrowserContext.StorageStateAsync(string, bool?, bool?)"/>.
        /// </summary>
        [JsonPropertyName("indexedDB")]
        [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingDefault)]
        public JsonElement IndexedDB { get; set; }

        /// <inheritdoc/>
        public bool Equals(StorageStateOrigin other)
        {
            if (other == null
                || Origin != other.Origin
                || !LocalStorage.SequenceEqual(other.LocalStorage))
            {
                return false;
            }

            if (IndexedDB.ValueKind == JsonValueKind.Undefined
                && other.IndexedDB.ValueKind == JsonValueKind.Undefined)
            {
                return true;
            }

            if (IndexedDB.ValueKind == JsonValueKind.Undefined
                || other.IndexedDB.ValueKind == JsonValueKind.Undefined)
            {
                return false;
            }

            return string.Equals(IndexedDB.GetRawText(), other.IndexedDB.GetRawText(), StringComparison.Ordinal);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
        {
            int indexed = IndexedDB.ValueKind == JsonValueKind.Undefined
                ? 0
                : StringComparer.Ordinal.GetHashCode(IndexedDB.GetRawText());
            return 412870874 +
                EqualityComparer<string>.Default.GetHashCode(Origin) +
                EqualityComparer<ICollection<NameValueEntry>>.Default.GetHashCode(LocalStorage) +
                indexed;
        }

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as StorageStateOrigin);
    }
}
