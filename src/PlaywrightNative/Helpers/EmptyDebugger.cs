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
using System;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// No-op <see cref="IDebugger"/> until browser-context debugging is wired.
    /// </summary>
    internal sealed class EmptyDebugger : IDebugger
    {
        /// <inheritdoc/>
        public event EventHandler PausedStateChanged;

        /// <inheritdoc/>
        public Microsoft.Playwright.DebuggerPausedDetails PausedDetails => null;

        /// <inheritdoc/>
        public Task RequestPauseAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task ResumeAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task NextAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task RunToAsync(Location location) => Task.CompletedTask;
    }
}
