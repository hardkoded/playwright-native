/*
 * Copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IWebError"/> payload.
    /// </summary>
    public sealed partial class WebError : IWebError
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="WebError"/> class.
        /// </summary>
        /// <param name="page">The page that produced the exception.</param>
        /// <param name="error">The unhandled error text.</param>
        /// <param name="location">Optional file, line, and column.</param>
        public WebError(IPage page, string error, WebErrorLocation location = null)
        {
            Page = page;
            Error = error ?? string.Empty;
            Location = location ?? new WebErrorLocation();
        }

        /// <inheritdoc/>
        public IPage Page { get; }

        /// <inheritdoc/>
        public string Error { get; }

        /// <inheritdoc/>
        public WebErrorLocation Location { get; }
    }
}
