/*
 * Copyright (c) 2020 Dario Kondratiuk
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
    /// A covered or uncovered span inside a script.
    /// </summary>
    public sealed class JSCoverageRange
    {
        /// <summary>
        /// Inclusive start offset in the script source.
        /// </summary>
        public int StartOffset { get; set; }

        /// <summary>
        /// Exclusive end offset in the script source.
        /// </summary>
        public int EndOffset { get; set; }

        /// <summary>
        /// How many times this span ran.
        /// </summary>
        public int Count { get; set; }
    }
}
