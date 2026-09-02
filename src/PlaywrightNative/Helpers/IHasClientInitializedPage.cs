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
    /// Official <c>page.initializedOrUndefined()</c>. Dialogs that open
    /// before the first non-initial navigation report <c>dialog.page()</c>
    /// as <see langword="null"/>.
    /// </summary>
    internal interface IHasClientInitializedPage
    {
        /// <summary>
        /// True after the first non-initial main-frame navigation
        /// (official <c>reportAsNew</c>).
        /// </summary>
        bool IsClientInitialized { get; }
    }
}
