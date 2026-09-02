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
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Firefox
{
    /// <summary>
    /// Provides JavaScript evaluation within a Firefox Juggler execution context.
    /// Uses <c>Runtime.evaluate</c> and <c>Runtime.callFunction</c> protocol commands.
    /// Unlike the Chromium equivalent, the context ID is a <see langword="string"/>.
    /// </summary>
    internal class FFExecutionContext
    {
        private readonly FFSession _client;
        private readonly string _contextId;

        /// <summary>
        /// Initializes a new instance of the <see cref="FFExecutionContext"/> class.
        /// </summary>
        /// <param name="client">The Juggler session used to send protocol commands.</param>
        /// <param name="contextId">The Juggler execution context identifier (string).</param>
        public FFExecutionContext(FFSession client, string contextId)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _contextId = contextId ?? throw new ArgumentNullException(nameof(contextId));
        }

        /// <summary>
        /// Gets the execution context ID.
        /// </summary>
        internal string ContextId => _contextId;

        /// <summary>
        /// Evaluates a JavaScript expression and deserializes the result to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result value to.</typeparam>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The deserialized result of the evaluation.</returns>
        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            JsonElement? response = await _client.SendAsync("Runtime.evaluate", new
            {
                expression,
                executionContextId = _contextId,
                returnByValue = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return default;
            }

            return DeserializeValue<T>(result);
        }

        /// <summary>
        /// Evaluates a JavaScript expression and returns the raw result element.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The result as a raw <see cref="JsonElement"/>, or <c>null</c>.</returns>
        internal async Task<JsonElement?> EvaluateAsync(string expression)
        {
            JsonElement? response = await _client.SendAsync("Runtime.evaluate", new
            {
                expression,
                executionContextId = _contextId,
                returnByValue = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Evaluates a JavaScript expression without serializing the value (returns handle).
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The remote object as a <see cref="JsonElement"/>, or <c>null</c>.</returns>
        internal async Task<JsonElement?> EvaluateHandleAsync(string expression)
        {
            JsonElement? response = await _client.SendAsync("Runtime.evaluate", new
            {
                expression,
                executionContextId = _contextId,
                returnByValue = false,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Calls a JavaScript function with the given arguments in this execution context.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result to.</typeparam>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The deserialized result.</returns>
        internal async Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            JsonElement? response = await _client.SendAsync("Runtime.callFunction", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                args = SerializeArguments(args),
                returnByValue = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return default;
            }

            return DeserializeValue<T>(result);
        }

        /// <summary>
        /// Calls a JavaScript function and returns the raw result element.
        /// </summary>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The raw result <see cref="JsonElement"/>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionAsync(string functionDeclaration, params object[] args)
        {
            JsonElement? response = await _client.SendAsync("Runtime.callFunction", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                args = SerializeArguments(args),
                returnByValue = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Calls a JavaScript function and returns the result as a remote handle (not serialized).
        /// </summary>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The remote object handle as a <see cref="JsonElement"/>, or <c>null</c>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionHandleAsync(string functionDeclaration, params object[] args)
        {
            JsonElement? response = await _client.SendAsync("Runtime.callFunction", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                args = SerializeArguments(args),
                returnByValue = false,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Calls a function on an existing remote object (by objectId).
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result to.</typeparam>
        /// <param name="objectId">The remote object ID to call the function on.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Additional arguments.</param>
        /// <returns>The deserialized result.</returns>
        internal async Task<T> EvaluateFunctionOnHandleAsync<T>(string objectId, string functionDeclaration, params object[] args)
        {
            int extraLength = args?.Length ?? 0;
            object[] callArgs = new object[1 + extraLength];
            callArgs[0] = new { objectId };
            for (int i = 0; i < extraLength; i++)
            {
                callArgs[1 + i] = SerializeArgument(args[i]);
            }

            JsonElement? response = await _client.SendAsync("Runtime.callFunction", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                args = callArgs,
                returnByValue = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return default;
            }

            return DeserializeValue<T>(result);
        }

        /// <summary>
        /// Calls a function on an existing remote object and returns the raw handle.
        /// </summary>
        /// <param name="objectId">The remote object ID.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Additional arguments.</param>
        /// <returns>The raw result <see cref="JsonElement"/>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionOnHandleAsync(string objectId, string functionDeclaration, params object[] args)
        {
            int extraLength = args?.Length ?? 0;
            object[] callArgs = new object[1 + extraLength];
            callArgs[0] = new { objectId };
            for (int i = 0; i < extraLength; i++)
            {
                callArgs[1 + i] = SerializeArgument(args[i]);
            }

            JsonElement? response = await _client.SendAsync("Runtime.callFunction", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                args = callArgs,
                returnByValue = false,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfException(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Releases a remote object reference by its objectId.
        /// </summary>
        /// <param name="objectId">The remote object ID to release.</param>
        /// <returns>A task that completes when the release call finishes.</returns>
        internal async Task ReleaseHandleAsync(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return;
            }

            try
            {
                await _client.SendAsync("Runtime.disposeObject", new { objectId, executionContextId = _contextId })
                    .ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Best-effort — session may be closed or object already released.
            }
        }

        private static object[] SerializeArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return [];
            }

            object[] result = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                result[i] = SerializeArgument(args[i]);
            }

            return result;
        }

        private static object SerializeArgument(object value)
        {
            if (value == null)
            {
                return new { value = (object)null };
            }

            if (value is FFJSHandle handle && !string.IsNullOrEmpty(handle.ObjectId))
            {
                return new { objectId = handle.ObjectId };
            }

            return value switch
            {
                double d when double.IsPositiveInfinity(d) => new { unserializableValue = "Infinity" },
                double d when double.IsNegativeInfinity(d) => new { unserializableValue = "-Infinity" },
                double d when double.IsNaN(d) => new { unserializableValue = "NaN" },
                float f when float.IsPositiveInfinity(f) => new { unserializableValue = "Infinity" },
                float f when float.IsNegativeInfinity(f) => new { unserializableValue = "-Infinity" },
                float f when float.IsNaN(f) => new { unserializableValue = "NaN" },
                _ => new { value },
            };
        }

        private static T DeserializeValue<T>(JsonElement remoteObject)
        {
            if (remoteObject.ValueKind == JsonValueKind.Null ||
                remoteObject.ValueKind == JsonValueKind.Undefined)
            {
                return default;
            }

            if (remoteObject.TryGetProperty("value", out JsonElement value))
            {
                if (value.ValueKind == JsonValueKind.Null ||
                    value.ValueKind == JsonValueKind.Undefined)
                {
                    return default;
                }

                return JsonSerializer.Deserialize<T>(value.GetRawText());
            }

            return JsonSerializer.Deserialize<T>(remoteObject.GetRawText());
        }

        private static void ThrowIfException(JsonElement response)
        {
            if (!response.TryGetProperty("exceptionDetails", out JsonElement exceptionDetails))
            {
                return;
            }

            string message = "Evaluation failed";

            if (exceptionDetails.TryGetProperty("value", out JsonElement value) &&
                value.ValueKind != JsonValueKind.Null)
            {
                if (value.TryGetProperty("message", out JsonElement msgEl))
                {
                    message = msgEl.GetString() ?? message;
                }
            }
            else if (exceptionDetails.TryGetProperty("text", out JsonElement text))
            {
                message = text.GetString() ?? message;
            }

            throw new PlaywrightNativeException(message);
        }
    }
}
