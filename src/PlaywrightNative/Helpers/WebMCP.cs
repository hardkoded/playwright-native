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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>server/webmcp.ts</c>: lists and calls the WebMCP tools of
    /// one frame by evaluating the injected <c>webMCP.ts</c> script in the
    /// frame utility world.
    /// </summary>
    internal sealed class WebMCP : IWebMCP
    {
        /// <summary>Official injected <c>packages/injected/src/webMCP.ts</c>.</summary>
        private const string ScriptSource = @"(() => {
  class WebMCPScript {
    constructor(global) {
      this._global = global;
      // Chromium exposes the entry point on `document`, Firefox on `navigator`.
      this._modelContext = global.document?.modelContext ?? global.navigator?.modelContext;
    }

    async tools() {
      return (await this._ownTools()).map(tool => this._describe(tool));
    }

    async callTool(name, input) {
      const modelContext = this._modelContext;
      if (!modelContext)
        throw new Error('WebMCP is not available on this page');
      if (modelContext.invokeTool)
        return await modelContext.invokeTool(name, input);
      const tool = (await this._ownTools()).find(tool => tool.name === name);
      if (!tool || !modelContext.executeTool)
        throw new Error(`WebMCP tool ""${name}"" is not registered in this frame`);
      return this._parseResult(await this._executeTool(modelContext, tool, input));
    }

    async _executeTool(modelContext, tool, input) {
      try {
        return await modelContext.executeTool(tool, input);
      } catch (e) {
        // Chromium before 155 takes the input as a JSON string. Chromium 151 (the
        // build this port ships) reports it as 'Failed to parse input string as JSON'.
        const message = String(e?.message);
        if (!message.includes('Failed to parse input arguments') && !message.includes('Failed to parse input string as JSON'))
          throw e;
        return await modelContext.executeTool(tool, JSON.stringify(input));
      }
    }

    _parseResult(result) {
      // Chromium hands the result back as a string.
      if (typeof result !== 'string')
        return result;
      if (result === 'undefined')
        return undefined;
      try {
        return JSON.parse(result);
      } catch {
        return result;
      }
    }

    async _ownTools() {
      const tools = await this._modelContext?.getTools?.() ?? [];
      // Chromium's getTools() aggregates same-origin descendant frames, Firefox's does not.
      return tools.filter(tool => !('window' in tool) || tool.window === this._global.window);
    }

    _describe(tool) {
      const annotations = tool.annotations;
      return {
        name: tool.name,
        description: tool.description ?? '',
        inputSchema: this._parseInputSchema(tool.inputSchema),
        annotations: annotations ? {
          readOnly: annotations.readOnlyHint,
          untrustedContent: annotations.untrustedContentHint,
          consequential: annotations.consequentialHint,
        } : undefined,
      };
    }

    _parseInputSchema(inputSchema) {
      // Chromium before 155 hands the schema back as a JSON string.
      if (typeof inputSchema !== 'string')
        return inputSchema;
      try {
        return JSON.parse(inputSchema);
      } catch {
        return undefined;
      }
    }
  }
  return new WebMCPScript(globalThis);
})()";

        private readonly IFrame _frame;

        internal WebMCP(IFrame frame)
        {
            _frame = frame ?? throw new ArgumentNullException(nameof(frame));
        }

        /// <inheritdoc/>
        public Task<IReadOnlyList<WebMCPTool>> ToolsAsync(float? timeout = default)
            => WithTimeoutAsync(ListToolsAsync(), timeout, "webMCP.tools");

        /// <inheritdoc/>
        public Task<JsonElement?> CallToolAsync(string name, object input = default, float? timeout = default)
            => WithTimeoutAsync(InvokeToolAsync(name, input), timeout, "webMCP.callTool");

        private static WebMCPTool Normalize(WebMCPTool tool)
        {
            WebMCPToolAnnotations annotations = new()
            {
                ReadOnly = tool.Annotations?.ReadOnly == true ? true : null,
                UntrustedContent = tool.Annotations?.UntrustedContent == true ? true : null,
                Consequential = tool.Annotations?.Consequential == true ? true : null,
            };
            bool hasAnnotations = annotations.ReadOnly.HasValue || annotations.UntrustedContent.HasValue || annotations.Consequential.HasValue;
            return new WebMCPTool
            {
                Name = tool.Name,
                Description = tool.Description,
                InputSchema = tool.InputSchema,
                Annotations = hasAnnotations ? annotations : null,
            };
        }

        private static IEnumerable<string> EnabledFeatures(IEnumerable<string> args)
            => args
                .Where(arg => arg.StartsWith("--enable-features=", StringComparison.Ordinal))
                .SelectMany(arg => arg["--enable-features=".Length..].Split(','));

        private async Task<IReadOnlyList<WebMCPTool>> ListToolsAsync()
        {
            ChromiumFrame frame = AssertBrowserSupport();
            List<WebMCPTool> tools = await frame
                .EvaluateInUtilityWorldAsync<List<WebMCPTool>>("() => " + ScriptSource + ".tools()")
                .ConfigureAwait(false);
            return (tools ?? []).Select(Normalize).ToList();
        }

        private async Task<JsonElement?> InvokeToolAsync(string name, object input)
        {
            IReadOnlyList<WebMCPTool> tools = await ListToolsAsync().ConfigureAwait(false);
            if (!tools.Any(tool => tool.Name == name))
            {
                throw new PlaywrightException($"No WebMCP tool named \"{name}\"." +
                    (tools.Count > 0
                        ? " Available tools: " + string.Join(", ", tools.Select(tool => tool.Name)) + "."
                        : " The frame does not register any WebMCP tools."));
            }

            return await ((ChromiumFrame)_frame)
                .EvaluateInUtilityWorldAsync<JsonElement?>(
                    "params => " + ScriptSource + ".callTool(params.name, params.input)",
                    new { name, input = input ?? new Dictionary<string, object>() })
                .ConfigureAwait(false);
        }

        private ChromiumFrame AssertBrowserSupport()
        {
            switch (_frame)
            {
                case WebKitFrame:
                    throw new PlaywrightException("WebMCP is not supported in WebKit.");
                case ChromiumFrame chromium:
                    // Only a browser launched by Playwright knows its launch options.
                    if (chromium.Page.Context.Browser is ChromiumBrowser { LaunchArgs: { } args }
                        && !EnabledFeatures(args).Contains("WebMCP"))
                    {
                        throw new PlaywrightException("WebMCP is not enabled. Launch the browser with the \"--enable-features=WebMCP\" argument.");
                    }

                    return chromium;
                default:
                    throw new NotSupportedException("WebMCP is not supported in this browser.");
            }
        }

        private Task<T> WithTimeoutAsync<T>(Task<T> task, float? timeout, string apiName)
        {
            int timeoutMs = TimeoutSettings.TimeoutMs(timeout ?? _frame.Page.DefaultTimeout());
            return task.WithTimeout(
                timeoutMs,
                _ => new TimeoutException(apiName + ": Timeout " + timeoutMs.ToString(CultureInfo.InvariantCulture) + "ms exceeded."));
        }
    }
}
