/*
 * Copyright (c) Microsoft Corporation.
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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative
{
    /// <summary>
    /// Lists and calls the WebMCP tools a frame registers through
    /// <c>document.modelContext.registerTool</c>. Chromium needs the
    /// <c>--enable-features=WebMCP</c> launch argument. WebKit does not
    /// implement WebMCP.
    /// </summary>
    public interface IWebMCP
    {
        /// <summary>
        /// Returns the tools currently registered by the frame. Throws if the
        /// browser was launched without WebMCP support. The page object covers
        /// the main frame only; child frames list their tools through their own
        /// <c>frame.Webmcp()</c>.
        /// </summary>
        /// <param name="timeout">Maximum time in milliseconds. Defaults to the page default timeout. Pass <c>0</c> to disable it.</param>
        /// <returns>The registered tools.</returns>
        Task<IReadOnlyList<WebMCPTool>> ToolsAsync(float? timeout = default);

        /// <summary>
        /// Calls a tool registered by the frame and returns its result. The
        /// result is whatever the tool's <c>execute</c> function resolved to,
        /// typically an object with a <c>content</c> array. A result with
        /// <c>isError: true</c> is returned as is. Throws when the tool is not
        /// registered or its <c>execute</c> function throws.
        /// </summary>
        /// <param name="name">Name of the tool, as reported by <see cref="ToolsAsync"/>.</param>
        /// <param name="input">Input for the tool, matching its <c>inputSchema</c>. Defaults to an empty object.</param>
        /// <param name="timeout">Maximum time in milliseconds. Defaults to the page default timeout. Pass <c>0</c> to disable it.</param>
        /// <returns>The tool result, or <see langword="null"/> when the tool returned nothing.</returns>
        Task<JsonElement?> CallToolAsync(string name, object input = default, float? timeout = default);
    }
}
