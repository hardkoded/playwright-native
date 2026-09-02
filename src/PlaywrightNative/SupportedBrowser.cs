/*
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
    /// Browsers that can be downloaded via BrowserFetcher.
    /// </summary>
    public enum SupportedBrowser
    {
        /// <summary>Chromium (open-source Chromium builds).</summary>
        Chromium,

        /// <summary>Mozilla Firefox.</summary>
        Firefox,

        /// <summary>Apple WebKit (used by Safari).</summary>
        Webkit,
    }
}
