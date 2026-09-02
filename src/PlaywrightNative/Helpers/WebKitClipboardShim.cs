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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright WebKit grants <c>clipboard-read</c> against a
    /// per-browser virtual clipboard. WebKit 2276's inspector grant does not
    /// enable <c>navigator.clipboard</c> on Linux, so the library installs the
    /// same isolated in-memory clipboard when that permission is granted.
    /// </summary>
    internal static class WebKitClipboardShim
    {
        /// <summary>
        /// Page init script that replaces <c>navigator.clipboard</c> with an
        /// isolated in-memory implementation.
        /// </summary>
        internal const string Source =
            "() => {\n" +
            "  let text = '';\n" +
            "  const clipboard = {\n" +
            "    writeText(value) { text = String(value ?? ''); return Promise.resolve(); },\n" +
            "    readText() { return Promise.resolve(text); },\n" +
            "  };\n" +
            "  const patch = target => {\n" +
            "    if (!target) return false;\n" +
            "    try { target.writeText = clipboard.writeText; target.readText = clipboard.readText; return true; } catch (e) { return false; }\n" +
            "  };\n" +
            "  if (patch(navigator.clipboard))\n" +
            "    return;\n" +
            "  try { delete navigator.clipboard; } catch (e) {}\n" +
            "  try {\n" +
            "    Object.defineProperty(navigator, 'clipboard', { configurable: true, writable: true, value: clipboard });\n" +
            "    return;\n" +
            "  } catch (e) {}\n" +
            "  try {\n" +
            "    Object.defineProperty(Navigator.prototype, 'clipboard', { configurable: true, get() { return clipboard; } });\n" +
            "  } catch (e) {}\n" +
            "}";
    }
}
