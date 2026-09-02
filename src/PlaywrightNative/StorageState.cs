using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;

namespace PlaywrightNative
{
    internal class StorageState : IEquatable<StorageState>
    {
        /// <summary>
        /// Cookie list.
        /// </summary>
        [JsonPropertyName("cookies")]
        public ICollection<Cookie> Cookies { get; set; } = new List<Cookie>();

        /// <summary>
        /// List of local storage per origin.
        /// </summary>
        [JsonPropertyName("origins")]
        public ICollection<StorageStateOrigin> Origins { get; set; } = new List<StorageStateOrigin>();

        /// <summary>
        /// Virtual WebAuthn passkeys, when collected with
        /// <see cref="IBrowserContext.StorageStateAsync(string, bool?, bool?)"/>.
        /// Omitted from JSON when <see langword="null"/>.
        /// </summary>
        [JsonPropertyName("credentials")]
        public ICollection<VirtualCredential> Credentials { get; set; }

        /// <inheritdoc/>
        public bool Equals(StorageState other)
        {
            if (other == null
                || !Cookies.SequenceEqual(other.Cookies)
                || !Origins.SequenceEqual(other.Origins))
            {
                return false;
            }

            if (Credentials == null && other.Credentials == null)
            {
                return true;
            }

            return Credentials != null
                && other.Credentials != null
                && Credentials.SequenceEqual(other.Credentials);
        }

        /// <inheritdoc/>
        public override int GetHashCode()
            => 412870874 +
                EqualityComparer<ICollection<Cookie>>.Default.GetHashCode(Cookies) +
                EqualityComparer<ICollection<StorageStateOrigin>>.Default.GetHashCode(Origins) +
                EqualityComparer<ICollection<VirtualCredential>>.Default.GetHashCode(Credentials);

        /// <inheritdoc/>
        public override bool Equals(object obj) => Equals(obj as StorageState);
    }
}
