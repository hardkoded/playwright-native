/*
 * Copyright (c) 2020 Darío Kondratiuk
 * Copyright (c) 2020 Meir Blachman
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
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Provides JavaScript evaluation capabilities within a specific CDP execution context.
    /// Sends <c>Runtime.evaluate</c> commands to the Chromium DevTools Protocol and
    /// deserializes the results.
    /// </summary>
    internal class CRExecutionContext
    {
        private readonly CRSession _client;
        private readonly int _contextId;
        private readonly TaskCompletionSource<bool> _destroyed =
            new(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Initializes a new instance of the <see cref="CRExecutionContext"/> class.
        /// </summary>
        /// <param name="client">The CDP session used to send protocol commands.</param>
        /// <param name="contextId">The CDP execution context identifier.</param>
        public CRExecutionContext(CRSession client, int contextId)
        {
            _client = client ?? throw new ArgumentNullException(nameof(client));
            _contextId = contextId;
        }

        /// <summary>
        /// Gets the CDP execution context identifier.
        /// </summary>
        internal int ContextId => _contextId;

        /// <summary>
        /// Gets the CDP session that owns this execution context.
        /// </summary>
        internal CRSession Session => _client;

        /// <summary>
        /// Completes when this context is destroyed (navigation, detach, or target swap).
        /// </summary>
        internal Task Destroyed => _destroyed.Task;

        /// <summary>
        /// Marks the context destroyed so in-flight <c>awaitPromise</c> evaluates fail with a
        /// navigation error instead of hanging on CDP.
        /// </summary>
        internal void MarkDestroyed() => _destroyed.TrySetResult(true);

        /// <summary>
        /// Evaluates a JavaScript expression and deserializes the result to <typeparamref name="T"/>.
        /// The expression is evaluated with <c>returnByValue: true</c> so the full value is
        /// serialized over the protocol.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result value to.</typeparam>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The deserialized result of the evaluation.</returns>
        /// <exception cref="PlaywrightException">
        /// Thrown when the evaluation produces an exception in the browser context.
        /// </exception>
        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.evaluate", new
            {
                expression,
                returnByValue = true,
                awaitPromise = true,
                userGesture = true,
                contextId = _contextId,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return default;
            }

            if (!remoteObject.TryGetProperty("value", out JsonElement value))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        /// <summary>
        /// Evaluates a JavaScript expression and returns the result as a raw <see cref="JsonElement"/>.
        /// The expression is evaluated with <c>returnByValue: true</c>. This overload is useful
        /// when the caller does not know the result type at compile time.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>
        /// The <c>result</c> property from the CDP <c>Runtime.evaluate</c> response as a
        /// <see cref="JsonElement"/>, or <c>null</c> if no result was returned.
        /// </returns>
        /// <exception cref="PlaywrightException">
        /// Thrown when the evaluation produces an exception in the browser context.
        /// </exception>
        internal async Task<JsonElement?> EvaluateAsync(string expression)
        {
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.evaluate", new
            {
                expression,
                returnByValue = true,
                awaitPromise = true,
                userGesture = true,
                contextId = _contextId,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Evaluates a JavaScript expression and returns the full <c>RemoteObject</c> without
        /// serializing the value. The expression is evaluated with <c>returnByValue: false</c>,
        /// so object references are preserved. The caller can use the <c>objectId</c> property
        /// of the returned element for further operations such as property access or function calls.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>
        /// The full <c>RemoteObject</c> from the CDP response (including <c>objectId</c>,
        /// <c>type</c>, <c>subtype</c>, <c>value</c>, etc.), or <c>null</c> if no result
        /// was returned.
        /// </returns>
        /// <exception cref="PlaywrightException">
        /// Thrown when the evaluation produces an exception in the browser context.
        /// </exception>
        internal async Task<JsonElement?> EvaluateHandleAsync(string expression)
        {
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.evaluate", new
            {
                expression,
                returnByValue = false,
                awaitPromise = true,
                userGesture = true,
                contextId = _contextId,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Evaluates a JavaScript function with the given arguments using
        /// <c>Runtime.callFunctionOn</c>. Arguments are passed by value.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result value to.</typeparam>
        /// <param name="functionDeclaration">A JavaScript function declaration (e.g. "(a, b) => a + b").</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The deserialized result of the function call.</returns>
        /// <exception cref="PlaywrightException">
        /// Thrown when the evaluation produces an exception in the browser context.
        /// </exception>
        internal async Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            object[] prepared = await PrepareCallArgumentsAsync(args).ConfigureAwait(false);
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                arguments = prepared,
                returnByValue = true,
                awaitPromise = true,
                userGesture = true,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return default;
            }

            if (!remoteObject.TryGetProperty("value", out JsonElement value))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        /// <summary>
        /// Evaluates a JavaScript function with the given arguments, returning the raw result.
        /// </summary>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The raw result <see cref="JsonElement"/>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionAsync(string functionDeclaration, params object[] args)
        {
            object[] prepared = await PrepareCallArgumentsAsync(args).ConfigureAwait(false);
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                arguments = prepared,
                returnByValue = true,
                awaitPromise = true,
                userGesture = true,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Evaluates a JavaScript function with the execution context and arguments, returning
        /// the raw CDP <c>RemoteObject</c> (with <c>objectId</c>). Mirrors
        /// <see cref="EvaluateFunctionAsync(string, object[])"/> but with <c>returnByValue = false</c>
        /// so remote references are preserved — necessary for <c>document.querySelector</c>-style
        /// calls that return DOM nodes.
        /// </summary>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The raw <c>RemoteObject</c>, or <c>null</c> if no result was returned.</returns>
        internal async Task<JsonElement?> EvaluateFunctionHandleAsync(string functionDeclaration, params object[] args)
        {
            object[] prepared = await PrepareCallArgumentsAsync(args).ConfigureAwait(false);
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                executionContextId = _contextId,
                arguments = prepared,
                returnByValue = false,
                awaitPromise = true,
                userGesture = true,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Evaluates a JavaScript function with the given object as <c>this</c> (via <c>objectId</c>)
        /// and returns the deserialized result. Mirrors <see cref="EvaluateFunctionAsync{T}(string, object[])"/>
        /// but targets a specific remote object instead of the execution context.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result value to.</typeparam>
        /// <param name="objectId">The CDP remote object ID to call the function on.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration (e.g. "node => node.focus()").</param>
        /// <param name="args">Additional arguments beyond the implicit <c>this</c>.</param>
        /// <returns>The deserialized result.</returns>
        /// <exception cref="PlaywrightException">When the evaluation throws in the browser.</exception>
        internal async Task<T> EvaluateFunctionOnHandleAsync<T>(string objectId, string functionDeclaration, params object[] args)
        {
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = PrependHandleArgument(objectId, args),
                returnByValue = true,
                awaitPromise = true,
                userGesture = true,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return default;
            }

            if (!remoteObject.TryGetProperty("value", out JsonElement value))
            {
                return default;
            }

            return JsonSerializer.Deserialize<T>(value.GetRawText());
        }

        /// <summary>
        /// Same as <see cref="EvaluateFunctionOnHandleAsync{T}(string, string, object[])"/> but
        /// returns the raw <c>RemoteObject</c> without value serialization.
        /// </summary>
        /// <param name="objectId">The CDP remote object ID to call the function on.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Additional arguments beyond the implicit <c>this</c>.</param>
        /// <returns>The raw CDP <c>RemoteObject</c> as a <see cref="JsonElement"/>, or <c>null</c>.</returns>
        /// <exception cref="PlaywrightException">When the evaluation throws in the browser.</exception>
        internal async Task<JsonElement?> EvaluateFunctionOnHandleAsync(string objectId, string functionDeclaration, params object[] args)
        {
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = PrependHandleArgument(objectId, args),
                returnByValue = true,
                awaitPromise = true,
                userGesture = true,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Same as <see cref="EvaluateFunctionOnHandleAsync(string, string, object[])"/> but
        /// with <c>returnByValue: false</c> so the result keeps its <c>objectId</c>.
        /// </summary>
        /// <param name="objectId">The CDP remote object ID to call the function on.</param>
        /// <param name="functionDeclaration">The JavaScript function declaration.</param>
        /// <param name="args">Additional arguments beyond the handle.</param>
        /// <returns>The raw CDP <c>RemoteObject</c>, or <c>null</c>.</returns>
        internal async Task<JsonElement?> EvaluateHandleOnHandleAsync(string objectId, string functionDeclaration, params object[] args)
        {
            JsonElement? response = await RaceDestroyedAsync(_client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = PrependHandleArgument(objectId, args),
                returnByValue = false,
                awaitPromise = true,
                userGesture = true,
            })).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Reads <paramref name="propertyName"/> on <paramref name="objectId"/> without
        /// awaiting a thenable. Official <c>JSHandle.getProperty</c> must return the
        /// Promise object itself so <c>toString()</c> can preview as <c>Promise</c>.
        /// </summary>
        /// <param name="objectId">The CDP remote object ID.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <returns>The raw CDP remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> GetPropertyOnHandleAsync(string objectId, string propertyName)
        {
            JsonElement? response = await _client.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration = "(object, name) => object[name]",
                objectId,
                arguments = PrependHandleArgument(objectId, new object[] { propertyName }),
                returnByValue = false,
                awaitPromise = false,
                userGesture = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfExceptionDetails(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement remoteObject))
            {
                return null;
            }

            return remoteObject;
        }

        /// <summary>
        /// Releases a remote object by its <c>objectId</c>. Safe to call multiple times —
        /// swallows the "No object with given id" CDP error so handles can be disposed idempotently.
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
                await _client.SendAsync("Runtime.releaseObject", new { objectId }).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                // Best-effort disposal — session closed or object already released.
            }
        }

        private static object[] SerializeArguments(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return [];
            }

            object[] callArguments = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                callArguments[i] = SerializeArgument(args[i]);
            }

            return callArguments;
        }

        private static object[] PrependHandleArgument(string objectId, object[] args)
        {
            // CDP's Runtime.callFunctionOn with an objectId binds it as `this` inside the
            // function, but does NOT pass it as a positional argument. Playwright's handle
            // APIs expect the target node as the first parameter (e.g. `node => node.focus()`),
            // so we explicitly prepend the handle's objectId to the arguments array.
            int extraLength = args?.Length ?? 0;
            object[] callArguments = new object[1 + extraLength];
            callArguments[0] = new { objectId };
            for (int i = 0; i < extraLength; i++)
            {
                callArguments[1 + i] = SerializeArgument(args[i]);
            }

            return callArguments;
        }

        private static object SerializeArgument(object value)
        {
            EvaluateCallbacks.ThrowIfHasFunctions(value);
            if (value == null)
            {
                return new { value = (object)null };
            }

            if (value is ImmediateJSHandle immediate)
            {
                return immediate.ToCallArgument();
            }

            if (value is ChromiumJSHandle instance)
            {
                if (!string.IsNullOrEmpty(instance.ObjectId))
                {
                    return new { objectId = instance.ObjectId };
                }

                return instance.ToCallArgument();
            }

            if (value is CRJSHandle crHandle)
            {
                if (!string.IsNullOrEmpty(crHandle.ObjectId))
                {
                    return new { objectId = crHandle.ObjectId };
                }

                return crHandle.ToImmediateHandle() is ImmediateJSHandle primitive
                    ? primitive.ToCallArgument()
                    : new { value = (object)null };
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

        private static void ThrowIfExceptionDetails(JsonElement response)
        {
            if (!response.TryGetProperty("exceptionDetails", out JsonElement exceptionDetails))
            {
                return;
            }

            string message = "Evaluation failed";

            if (exceptionDetails.TryGetProperty("exception", out JsonElement exception))
            {
                if (exception.TryGetProperty("description", out JsonElement description)
                    && description.ValueKind == JsonValueKind.String
                    && !string.IsNullOrEmpty(description.GetString()))
                {
                    message = description.GetString();
                }

                if (exception.TryGetProperty("value", out JsonElement thrown)
                    && thrown.ValueKind != JsonValueKind.Undefined
                    && thrown.ValueKind != JsonValueKind.Null)
                {
                    string thrownText = thrown.ValueKind == JsonValueKind.String
                        ? thrown.GetString()
                        : thrown.GetRawText();
                    if (!string.IsNullOrEmpty(thrownText)
                        && (string.IsNullOrEmpty(message) || !message.Contains(thrownText, StringComparison.Ordinal)))
                    {
                        message = string.IsNullOrEmpty(message) || message == "Evaluation failed" || message == "Uncaught"
                            ? thrownText
                            : message + " " + thrownText;
                    }
                }
            }
            else if (exceptionDetails.TryGetProperty("text", out JsonElement text))
            {
                message = text.GetString();
            }

            throw new PlaywrightException(EvaluateSerialization.RewriteError(message));
        }

        /// <summary>
        /// Races a CDP evaluate against context destruction so navigations fail
        /// pending <c>awaitPromise</c> calls with the official navigation message.
        /// </summary>
        /// <typeparam name="T">The CDP response type.</typeparam>
        /// <param name="task">The in-flight protocol call.</param>
        /// <returns>The protocol response when the context survives.</returns>
        private async Task<T> RaceDestroyedAsync<T>(Task<T> task)
        {
            if (task == null)
            {
                throw new ArgumentNullException(nameof(task));
            }

            if (_destroyed.Task.IsCompleted && !task.IsCompleted)
            {
                throw new PlaywrightException(EvaluateSerialization.NavigationMessage);
            }

            await Task.WhenAny(task, _destroyed.Task).ConfigureAwait(false);

            // Prefer a completed CDP response even when the context was destroyed in
            // the same turn. Official "should not throw when evaluation does a
            // navigation" requires returning the script value when Runtime.evaluate
            // already finished (e.g. location.href = url). Task.WhenAny may pick
            // either completed task when both finish together.
            if (task.IsCompleted)
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                catch (PlaywrightException ex) when (
                    ex.Message != null
                    && (ex.Message.Contains("Cannot find context with specified id", StringComparison.Ordinal)
                        || ex.Message.Contains("Inspected target navigated or closed", StringComparison.Ordinal)
                        || ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)))
                {
                    throw new PlaywrightException(EvaluateSerialization.NavigationMessage);
                }
            }

            throw new PlaywrightException(EvaluateSerialization.NavigationMessage);
        }

        private async Task<object[]> PrepareCallArgumentsAsync(object[] args)
        {
            if (args == null || args.Length == 0)
            {
                return [];
            }

            object[] callArguments = new object[args.Length];
            for (int i = 0; i < args.Length; i++)
            {
                callArguments[i] = await PrepareArgumentAsync(args[i]).ConfigureAwait(false);
            }

            return callArguments;
        }

        private async Task<object> PrepareArgumentAsync(object value)
        {
            if (value is ImmediateJSHandle immediate)
            {
                return immediate.ToCallArgument();
            }

            if (value is ChromiumJSHandle instance)
            {
                if (string.IsNullOrEmpty(instance.ObjectId))
                {
                    return instance.ToCallArgument();
                }

                return await PrepareHandleArgumentAsync(
                    instance.ObjectId,
                    instance.ExecutionContext,
                    instance.AsElement() != null).ConfigureAwait(false);
            }

            if (value is CRJSHandle crHandle)
            {
                if (string.IsNullOrEmpty(crHandle.ObjectId))
                {
                    return crHandle.ToImmediateHandle() is ImmediateJSHandle primitive
                        ? primitive.ToCallArgument()
                        : new { value = (object)null };
                }

                return await PrepareHandleArgumentAsync(
                    crHandle.ObjectId,
                    crHandle.ExecutionContext,
                    crHandle is CRElementHandle).ConfigureAwait(false);
            }

            return SerializeArgument(value);
        }

        private async Task<object> PrepareHandleArgumentAsync(
            string objectId,
            CRExecutionContext source,
            bool isElement)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return new { value = (object)null };
            }

            if (source != null && source.ContextId == _contextId)
            {
                return new { objectId };
            }

            if (!isElement)
            {
                throw new PlaywrightException(DispatchEventScript.DifferentContextMessage);
            }

            try
            {
                JsonElement? described = await _client.SendAsync("DOM.describeNode", new { objectId })
                    .ConfigureAwait(false);
                if (described == null
                    || !described.Value.TryGetProperty("node", out JsonElement node)
                    || !node.TryGetProperty("backendNodeId", out JsonElement backendEl)
                    || !backendEl.TryGetInt32(out int backendNodeId))
                {
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
                }

                JsonElement? resolved = await _client.SendAsync("DOM.resolveNode", new
                {
                    backendNodeId,
                    executionContextId = _contextId,
                }).ConfigureAwait(false);

                if (resolved == null
                    || !resolved.Value.TryGetProperty("object", out JsonElement remote)
                    || !remote.TryGetProperty("objectId", out JsonElement adoptedId)
                    || adoptedId.ValueKind != JsonValueKind.String)
                {
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
                }

                string adopted = adoptedId.GetString();
                if (string.IsNullOrEmpty(adopted))
                {
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
                }

                return new { objectId = adopted };
            }
            catch (PlaywrightException ex)
            {
                if (string.Equals(ex.Message, DispatchEventScript.DifferentContextMessage, StringComparison.Ordinal)
                    || string.Equals(ex.Message, EvaluateWithArg.UnableToAdoptMessage, StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
            }
        }
    }
}
