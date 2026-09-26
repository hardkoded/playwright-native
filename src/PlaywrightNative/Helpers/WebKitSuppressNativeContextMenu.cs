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
    /// On macOS/Windows WebKit, a right click opens a native context menu that
    /// runs a nested modal event loop. That swallows the following synthetic
    /// mouseup / left click (see microsoft/playwright#39246). Official Playwright
    /// fixed this in the browser when <c>controlledByAutomation &amp;&amp;
    /// simulatingUserInput</c>; frozen mac14 WebKit (r2251) lacks that patch.
    /// Capture-phase <c>preventDefault</c> keeps the DOM <c>contextmenu</c>
    /// event while skipping the native menu UI.
    /// </summary>
    internal static class WebKitSuppressNativeContextMenu
    {
        /// <summary>
        /// Page init script that suppresses the native context menu under
        /// automation without blocking page <c>contextmenu</c> listeners.
        /// </summary>
        internal const string Source =
            @"(() => {
  const suppress = (event) => {
    try { event.preventDefault(); } catch (e) {}
  };
  const install = (target) => {
    if (!target || typeof target.addEventListener !== 'function') return;
    try {
      target.addEventListener('contextmenu', suppress, true);
    } catch (e) {}
  };
  if (!globalThis.__pw_suppress_native_context_menu__) {
    globalThis.__pw_suppress_native_context_menu__ = true;
    install(globalThis);
  }
  // document.open/write/close keeps Document identity but clears listeners.
  // Always re-bind on the current document when this script is replayed after
  // SetContent (WeakSet-by-identity would skip and leave the native menu open).
  if (globalThis.document) {
    install(globalThis.document);
  }
})()";
    }
}
