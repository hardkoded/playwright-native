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
using System.Text.Json.Serialization;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Describes a WebKit target reported under a page proxy.
    /// </summary>
    internal sealed class WKTargetInfo
    {
        /// <summary>
        /// Gets or sets the target identifier.
        /// </summary>
        [JsonPropertyName("targetId")]
        public string TargetId { get; set; }

        /// <summary>
        /// Gets or sets the target type — <c>page</c> or <c>frame</c>.
        /// </summary>
        [JsonPropertyName("type")]
        public string Type { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether this is a provisional target spawned
        /// for a cross-process navigation that has not yet committed.
        /// </summary>
        [JsonPropertyName("isProvisional")]
        public bool IsProvisional { get; set; }

        /// <summary>
        /// Gets or sets a value indicating whether the target was created paused and
        /// must be resumed via <c>Target.resume</c>.
        /// </summary>
        [JsonPropertyName("isPaused")]
        public bool IsPaused { get; set; }

        /// <summary>
        /// Gets or sets the target URL when the protocol reports one (workers).
        /// </summary>
        [JsonPropertyName("url")]
        public string Url { get; set; }
    }
}
