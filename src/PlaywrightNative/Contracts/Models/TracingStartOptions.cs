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
    /// Official <c>tracing.start</c> options, plus PlaywrightNative screen/aria snapshot flags.
    /// </summary>
    public class TracingStartOptions : Microsoft.Playwright.TracingStartOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TracingStartOptions"/> class.
        /// </summary>
        public TracingStartOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracingStartOptions"/> class.
        /// </summary>
        /// <param name="clone">Options to copy.</param>
        public TracingStartOptions(TracingStartOptions clone)
            : base(clone)
        {
            if (clone == null)
            {
                return;
            }

            ScreenSnapshots = clone.ScreenSnapshots;
            AriaSnapshots = clone.AriaSnapshots;
        }

        /// <summary>
        /// Official <c>snapshots: { screen: true }</c>. Captures PNG action
        /// screenshots with before/action/after phases.
        /// </summary>
        public bool? ScreenSnapshots { get; set; }

        /// <summary>
        /// Official <c>snapshots: { aria: true }</c>. Captures aria snapshots
        /// with before/action/after phases.
        /// </summary>
        public bool? AriaSnapshots { get; set; }
    }
}
