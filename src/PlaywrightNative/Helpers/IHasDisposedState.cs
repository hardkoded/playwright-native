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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Handles that track whether <c>DisposeAsync</c> has already run, so an
    /// evaluate call that embeds one as an argument can reject it with the
    /// official message instead of leaking a raw protocol error.
    /// </summary>
    internal interface IHasDisposedState
    {
        /// <summary>Gets whether <c>DisposeAsync</c> has already run.</summary>
        bool IsDisposed { get; }
    }
}
