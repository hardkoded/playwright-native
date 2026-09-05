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
using PlaywrightNative.Helpers;

namespace PlaywrightNative.WebKit
{
    /// <summary>
    /// Provides JavaScript evaluation within a WebKit Inspector Protocol execution context.
    /// Sends <c>Runtime.evaluate</c> on the inner-target session and deserializes the
    /// returned <c>RemoteObject</c>. Unlike Chromium/Firefox, WebKit reports thrown errors
    /// via <c>wasThrown: true</c> rather than <c>exceptionDetails</c>.
    /// </summary>
    internal class WKExecutionContext
    {
        private readonly WKTargetSession _session;
        private readonly int? _contextId;
        private readonly TaskCompletionSource<bool> _destroyed =
            new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

        /// <summary>
        /// Initializes a new instance of the <see cref="WKExecutionContext"/> class.
        /// </summary>
        /// <param name="session">The inner-target session used to send protocol commands.</param>
        /// <param name="contextId">The WIP <c>ExecutionContextId</c> (numeric).</param>
        public WKExecutionContext(WKTargetSession session, int contextId)
            : this(session, (int?)contextId)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="WKExecutionContext"/> class without a
        /// context id (WebKit dedicated workers evaluate in the default worker world).
        /// </summary>
        /// <param name="session">The worker or page session used to send protocol commands.</param>
        /// <param name="contextId">Optional WIP execution context id. Omit for workers.</param>
        internal WKExecutionContext(WKTargetSession session, int? contextId)
        {
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _contextId = contextId;
        }

        /// <summary>
        /// Gets the execution context id, or 0 when the context is the default worker world.
        /// </summary>
        internal int ContextId => _contextId ?? 0;

        /// <summary>
        /// Completes when this context is destroyed (navigation, detach, or target swap).
        /// </summary>
        internal Task Destroyed => _destroyed.Task;

        /// <summary>
        /// Marks the context destroyed so in-flight evaluates fail with a navigation error
        /// instead of hanging on WebKit <c>Runtime.callFunctionOn</c>.
        /// </summary>
        internal void MarkDestroyed() => _destroyed.TrySetResult(true);

        /// <summary>
        /// Evaluates a JavaScript expression and returns the raw <c>RemoteObject</c>
        /// <em>without</em> serializing the value (<c>returnByValue: false</c>), so the
        /// remote reference (including <c>objectId</c> and <c>subtype</c>) is preserved.
        /// Mirrors upstream wkExecutionContext's <c>rawEvaluateHandle</c>.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The raw <c>result</c> <c>RemoteObject</c> element, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateHandleAsync(string expression)
        {
            JsonElement? response = await _session.SendAsync(
                "Runtime.evaluate",
                BuildEvaluateParams(expression, returnByValue: false)).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Evaluates a JavaScript function in this execution context with the given arguments
        /// via <c>Runtime.callFunctionOn</c>. JS handles from another world are rejected or
        /// adopted when they are DOM nodes.
        /// </summary>
        /// <typeparam name="T">The type to deserialize the result value to.</typeparam>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The deserialized result of the function call.</returns>
        internal async Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            JsonElement? remote = await EvaluateFunctionRemoteAsync(functionDeclaration, args).ConfigureAwait(false);
            return remote == null ? default : DeserializeValue<T>(remote.Value);
        }

        /// <summary>
        /// Same as <see cref="EvaluateFunctionAsync{T}"/> but returns the raw WIP
        /// <c>RemoteObject</c> (<c>returnByValue: true</c>) for structured-clone parsing.
        /// </summary>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The remote result object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionRemoteAsync(string functionDeclaration, params object[] args)
        {
            object[] prepared = await PrepareCallArgumentsAsync(args).ConfigureAwait(false);
            object payload = _contextId.HasValue
                ? (object)new
                {
                    functionDeclaration,
                    executionContextId = _contextId.Value,
                    arguments = prepared,
                    returnByValue = true,
                    emulateUserGesture = true,
                    awaitPromise = true,
                }
                : new
                {
                    functionDeclaration,
                    arguments = prepared,
                    returnByValue = true,
                    emulateUserGesture = true,
                    awaitPromise = true,
                };

            JsonElement? response;
            try
            {
                response = await _session.SendAsync("Runtime.callFunctionOn", payload)
                    .ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                if (HasForeignElementArgument(args))
                {
                    throw new PlaywrightNativeException(EvaluateWithArg.UnableToAdoptMessage);
                }

                throw;
            }

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            try
            {
                ThrowIfThrown(responseElement);
            }
            catch (PlaywrightNativeException)
            {
                if (HasForeignElementArgument(args))
                {
                    throw new PlaywrightNativeException(EvaluateWithArg.UnableToAdoptMessage);
                }

                throw;
            }

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Same as <see cref="EvaluateFunctionAsync{T}(string, object[])"/> but with
        /// <c>returnByValue: false</c> so the result keeps its <c>objectId</c>.
        /// </summary>
        /// <param name="functionDeclaration">A JavaScript function declaration.</param>
        /// <param name="args">Arguments to pass to the function.</param>
        /// <returns>The raw remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateFunctionHandleAsync(string functionDeclaration, params object[] args)
        {
            object[] prepared = await PrepareCallArgumentsAsync(args).ConfigureAwait(false);
            object payload = _contextId.HasValue
                ? (object)new
                {
                    functionDeclaration,
                    executionContextId = _contextId.Value,
                    arguments = prepared,
                    returnByValue = false,
                    emulateUserGesture = true,
                    awaitPromise = true,
                }
                : new
                {
                    functionDeclaration,
                    arguments = prepared,
                    returnByValue = false,
                    emulateUserGesture = true,
                    awaitPromise = true,
                };

            JsonElement? response = await _session.SendAsync("Runtime.callFunctionOn", payload)
                .ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Calls a JavaScript function with a remote object bound as the first argument,
        /// returning the deserialized result (<c>returnByValue: true</c>). Maps to upstream
        /// wkExecutionContext's <c>evaluateWithArguments</c>: <c>Runtime.callFunctionOn</c> with
        /// the handle's <c>objectId</c> as <c>this</c>, the same <c>objectId</c> prepended as the
        /// first positional argument, then any extra primitive arguments by value.
        /// </summary>
        /// <typeparam name="T">The target type for the result value.</typeparam>
        /// <param name="objectId">The WIP remote object id to call the function on.</param>
        /// <param name="functionDeclaration">A function declaration; the handle is passed as the first argument.</param>
        /// <param name="args">Additional primitive arguments beyond the handle.</param>
        /// <returns>The deserialized result.</returns>
        internal async Task<T> EvaluateFunctionOnHandleAsync<T>(string objectId, string functionDeclaration, params object[] args)
        {
            JsonElement? response = await _session.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = BuildHandleArguments(objectId, args),
                returnByValue = true,
                emulateUserGesture = true,
                awaitPromise = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return default;
            }

            return DeserializeValue<T>(result);
        }

        /// <summary>
        /// Same as <see cref="EvaluateFunctionOnHandleAsync{T}(string, string, object[])"/> but
        /// with <c>returnByValue: false</c> so the result keeps its <c>objectId</c>.
        /// </summary>
        /// <param name="objectId">The WIP remote object id.</param>
        /// <param name="functionDeclaration">A function declaration; the handle is the first argument.</param>
        /// <param name="args">Additional primitive arguments beyond the handle.</param>
        /// <returns>The raw remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateHandleOnHandleAsync(string objectId, string functionDeclaration, params object[] args)
        {
            JsonElement? response = await _session.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = BuildHandleArguments(objectId, args),
                returnByValue = false,
                emulateUserGesture = true,
                awaitPromise = true,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Reads <paramref name="propertyName"/> on <paramref name="objectId"/> without
        /// awaiting a thenable. Official <c>JSHandle.getProperty</c> must return the
        /// Promise object itself so <c>toString()</c> can preview as <c>Promise</c>.
        /// </summary>
        /// <param name="objectId">The WIP remote object id.</param>
        /// <param name="propertyName">The property to read.</param>
        /// <returns>The raw remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> GetPropertyOnHandleAsync(string objectId, string propertyName)
        {
            JsonElement? response = await _session.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration = "(object, name) => object[name]",
                objectId,
                arguments = BuildHandleArguments(objectId, new object[] { propertyName }),
                returnByValue = false,
                emulateUserGesture = true,
                awaitPromise = false,
            }).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Releases a remote object by its <c>objectId</c> via <c>Runtime.releaseObject</c>.
        /// Best-effort — swallows protocol errors so handles can be disposed idempotently
        /// even after the session has gone away. Mirrors upstream <c>releaseHandle</c>.
        /// </summary>
        /// <param name="objectId">The remote object id to release.</param>
        /// <returns>A task that completes when the release call finishes.</returns>
        internal async Task ReleaseHandleAsync(string objectId)
        {
            if (string.IsNullOrEmpty(objectId))
            {
                return;
            }

            try
            {
                await _session.SendAsync("Runtime.releaseObject", new { objectId }).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Best-effort disposal — session closed or object already released.
            }
        }

        /// <summary>
        /// Evaluates a JavaScript expression and deserializes the result to <typeparamref name="T"/>.
        /// </summary>
        /// <typeparam name="T">The target type for the result value.</typeparam>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The deserialized value.</returns>
        internal async Task<T> EvaluateAsync<T>(string expression)
        {
            JsonElement? response = await SendEvaluateAsync(expression).ConfigureAwait(false);

            if (response == null)
            {
                return default;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return default;
            }

            return DeserializeValue<T>(result);
        }

        /// <summary>
        /// Evaluates a JavaScript expression and returns the raw <c>RemoteObject</c> element.
        /// </summary>
        /// <param name="expression">The JavaScript expression to evaluate.</param>
        /// <returns>The raw <c>result</c> element, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateAsync(string expression)
        {
            JsonElement? response = await SendEvaluateAsync(expression).ConfigureAwait(false);

            if (response == null)
            {
                return null;
            }

            JsonElement responseElement = response.Value;
            ThrowIfThrown(responseElement);

            if (!responseElement.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            return result;
        }

        /// <summary>
        /// Evaluates a structured-clone wrapped expression. Uses
        /// <c>returnByValue: true</c> so a same-turn tagged payload survives
        /// navigation-during-return. Promises are awaited and abort when this
        /// context is destroyed.
        /// </summary>
        /// <param name="expression">An expression that returns a tagged payload or a promise of one.</param>
        /// <returns>The remote object (<c>result</c>) for <see cref="EvaluateSerialization.ParseRemote{T}"/>.</returns>
        internal async Task<JsonElement?> EvaluateSerializedRemoteAsync(string expression)
        {
            JsonElement? byValue = await _session.SendAsync(
                "Runtime.evaluate",
                BuildEvaluateParams(expression, returnByValue: true)).ConfigureAwait(false);
            if (byValue == null)
            {
                return null;
            }

            JsonElement response = byValue.Value;
            ThrowIfThrown(response);
            if (!response.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            if (IsTaggedRemote(result))
            {
                return result;
            }

            string objectId = RemoteObject.GetObjectId(result);
            if (string.IsNullOrEmpty(objectId))
            {
                JsonElement? handle = await EvaluateHandleAsync(expression).ConfigureAwait(false);
                objectId = handle == null ? null : RemoteObject.GetObjectId(handle.Value);
                if (string.IsNullOrEmpty(objectId))
                {
                    return result;
                }
            }

            JsonElement? awaited = await AwaitOrDestroyAsync(objectId).ConfigureAwait(false);
            if (awaited == null)
            {
                return null;
            }

            ThrowIfThrown(awaited.Value);
            return awaited.Value.TryGetProperty("result", out JsonElement awaitedResult)
                ? awaitedResult
                : awaited;
        }

        private static bool IsTaggedRemote(JsonElement result)
        {
            if (result.TryGetProperty("type", out JsonElement type)
                && type.ValueKind == JsonValueKind.String
                && string.Equals(type.GetString(), "undefined", StringComparison.Ordinal))
            {
                return true;
            }

            if (!result.TryGetProperty("value", out JsonElement value)
                || value.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return value.TryGetProperty("v", out _)
                || value.TryGetProperty("b", out _)
                || value.TryGetProperty("n", out _)
                || value.TryGetProperty("s", out _)
                || value.TryGetProperty("bi", out _)
                || value.TryGetProperty("d", out _)
                || value.TryGetProperty("u", out _)
                || value.TryGetProperty("r", out _)
                || value.TryGetProperty("e", out _)
                || value.TryGetProperty("ta", out _)
                || value.TryGetProperty("a", out _)
                || value.TryGetProperty("o", out _)
                || value.TryGetProperty("ref", out _);
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

            return default;
        }

        private static object[] BuildHandleArguments(string objectId, object[] args)
        {
            // WIP's Runtime.callFunctionOn binds objectId as `this`, but upstream
            // Playwright also passes the node as the first positional argument so handle
            // functions can be written as `node => ...`. Prepend { objectId } then any
            // extra primitives by value.
            int extraLength = args?.Length ?? 0;
            object[] callArguments = new object[1 + extraLength];
            callArguments[0] = new { objectId };
            for (int i = 0; i < extraLength; i++)
            {
                callArguments[1 + i] = SerializeHandleArgument(args[i]);
            }

            return callArguments;
        }

        private static object SerializeHandleArgument(object value)
        {
            if (value is ImmediateJSHandle immediate)
            {
                return immediate.ToCallArgument();
            }

            if (value is WKJSHandle handle && !string.IsNullOrEmpty(handle.ObjectId))
            {
                return new { objectId = handle.ObjectId };
            }

            return new { value };
        }

        private static void ThrowIfThrown(JsonElement response)
        {
            if (!response.TryGetProperty("wasThrown", out JsonElement thrownEl)
                || thrownEl.ValueKind != JsonValueKind.True)
            {
                return;
            }

            string message = "Evaluation failed";

            if (response.TryGetProperty("result", out JsonElement result))
            {
                // For thrown values, the RemoteObject's description usually carries
                // the toString() of the error (e.g. "Error: boom"). Fall back to value
                // when description is absent (e.g. throwing a primitive).
                if (result.TryGetProperty("value", out JsonElement value)
                    && value.ValueKind != JsonValueKind.Undefined
                    && value.ValueKind != JsonValueKind.Null)
                {
                    string thrownText = value.ValueKind == JsonValueKind.String
                        ? value.GetString()
                        : value.GetRawText();
                    if (!string.IsNullOrEmpty(thrownText))
                    {
                        message = thrownText;
                    }
                }
                else if (result.TryGetProperty("description", out JsonElement description)
                    && description.ValueKind == JsonValueKind.String)
                {
                    message = description.GetString() ?? message;
                }
            }

            throw new PlaywrightNativeException(EvaluateSerialization.RewriteError(message));
        }

        private object BuildEvaluateParams(string expression, bool returnByValue)
        {
            if (_contextId.HasValue)
            {
                return new
                {
                    expression,
                    contextId = _contextId.Value,
                    returnByValue,
                    emulateUserGesture = true,
                };
            }

            return new
            {
                expression,
                returnByValue,
                emulateUserGesture = true,
            };
        }

        private async Task<JsonElement?> SendEvaluateAsync(string expression)
        {
            // WebKit's Runtime.evaluate has no awaitPromise parameter (it is only honored by
            // Runtime.callFunctionOn), so a promise-returning expression comes back as an
            // unresolved Promise handle. Evaluate the program first as a handle — this
            // preserves multi-statement programs and their completion value — then unwrap
            // via callFunctionOn. Do not call function() { return this; } on the result:
            // WebKit throws "TypeError: Type error" when `this` is a Promise and
            // returnByValue is true (structured-clone of a Promise). Await the value as
            // an argument on a dummy receiver instead.
            JsonElement? evalResponse = await _session.SendAsync(
                "Runtime.evaluate",
                BuildEvaluateParams(expression, returnByValue: false)).ConfigureAwait(false);

            if (evalResponse == null)
            {
                return null;
            }

            JsonElement evalElement = evalResponse.Value;

            bool threw = evalElement.TryGetProperty("wasThrown", out JsonElement wasThrown)
                && wasThrown.ValueKind == JsonValueKind.True;
            if (threw
                || !evalElement.TryGetProperty("result", out JsonElement result)
                || !result.TryGetProperty("objectId", out JsonElement objectId)
                || objectId.ValueKind != JsonValueKind.String)
            {
                return evalResponse;
            }

            return await AwaitOrDestroyAsync(objectId.GetString()).ConfigureAwait(false);
        }

        private async Task<JsonElement?> AwaitOrDestroyAsync(string resultId)
        {
            if (_destroyed.Task.IsCompleted)
            {
                throw new PlaywrightNativeException(EvaluateSerialization.NavigationMessage);
            }

            JsonElement? dummyResponse = await _session.SendAsync(
                "Runtime.evaluate",
                BuildEvaluateParams("({})", returnByValue: false)).ConfigureAwait(false);
            string dummyId = null;
            if (dummyResponse.HasValue
                && dummyResponse.Value.TryGetProperty("result", out JsonElement dummyResult)
                && dummyResult.TryGetProperty("objectId", out JsonElement dummyObjectId)
                && dummyObjectId.ValueKind == JsonValueKind.String)
            {
                dummyId = dummyObjectId.GetString();
            }

            try
            {
                Task<JsonElement?> awaitTask = string.IsNullOrEmpty(dummyId)
                    ? AwaitRemoteValueAsync(resultId)
                    : _session.SendAsync("Runtime.callFunctionOn", new
                    {
                        objectId = dummyId,
                        functionDeclaration = "async function(value) { return await value; }",
                        arguments = new object[] { new { objectId = resultId } },
                        returnByValue = true,
                        emulateUserGesture = true,
                        awaitPromise = true,
                    });
                Task completed = await Task.WhenAny(awaitTask, _destroyed.Task).ConfigureAwait(false);
                if (completed == _destroyed.Task)
                {
                    throw new PlaywrightNativeException(EvaluateSerialization.NavigationMessage);
                }

                return await awaitTask.ConfigureAwait(false);
            }
            finally
            {
                if (!string.IsNullOrEmpty(dummyId))
                {
                    await ReleaseHandleAsync(dummyId).ConfigureAwait(false);
                }
            }
        }

        private Task<JsonElement?> AwaitRemoteValueAsync(string objectId)
            => _session.SendAsync("Runtime.callFunctionOn", new
            {
                objectId,
                functionDeclaration = "async function(value) { return await value; }",
                arguments = new object[] { new { objectId } },
                returnByValue = true,
                emulateUserGesture = true,
                awaitPromise = true,
            });

        private bool HasForeignElementArgument(object[] args)
        {
            if (args == null)
            {
                return false;
            }

            foreach (object arg in args)
            {
                if (arg is WKJSHandle handle
                    && handle.AsElement() != null
                    && handle.ExecutionContext != null
                    && handle.ExecutionContext.ContextId != ContextId)
                {
                    return true;
                }
            }

            return false;
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

            if (value is WKJSHandle handle)
            {
                return await PrepareHandleArgumentAsync(handle).ConfigureAwait(false);
            }

            return SerializeHandleArgument(value);
        }

        private async Task<object> PrepareHandleArgumentAsync(WKJSHandle handle)
        {
            if (handle == null || string.IsNullOrEmpty(handle.ObjectId))
            {
                return new { value = (object)null };
            }

            if (handle.ExecutionContext != null && handle.ExecutionContext.ContextId == ContextId)
            {
                return new { objectId = handle.ObjectId };
            }

            if (handle.AsElement() == null)
            {
                throw new PlaywrightNativeException(DispatchEventScript.DifferentContextMessage);
            }

            if (!_contextId.HasValue)
            {
                throw new PlaywrightNativeException(EvaluateWithArg.UnableToAdoptMessage);
            }

            try
            {
                // Official wkPage.adoptElementHandle: DOM.resolveNode into the target world.
                JsonElement? resolved = await _session.SendAsync("DOM.resolveNode", new
                {
                    objectId = handle.ObjectId,
                    executionContextId = _contextId.Value,
                }).ConfigureAwait(false);

                if (resolved == null
                    || !resolved.Value.TryGetProperty("object", out JsonElement remote)
                    || (remote.TryGetProperty("subtype", out JsonElement subtype)
                        && subtype.ValueKind == JsonValueKind.String
                        && string.Equals(subtype.GetString(), "null", StringComparison.Ordinal))
                    || !remote.TryGetProperty("objectId", out JsonElement adoptedId)
                    || adoptedId.ValueKind != JsonValueKind.String)
                {
                    throw new PlaywrightNativeException(EvaluateWithArg.UnableToAdoptMessage);
                }

                string adopted = adoptedId.GetString();
                if (string.IsNullOrEmpty(adopted))
                {
                    throw new PlaywrightNativeException(EvaluateWithArg.UnableToAdoptMessage);
                }

                return new { objectId = adopted };
            }
            catch (PlaywrightNativeException ex)
            {
                if (string.Equals(ex.Message, DispatchEventScript.DifferentContextMessage, StringComparison.Ordinal)
                    || string.Equals(ex.Message, EvaluateWithArg.UnableToAdoptMessage, StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightNativeException(EvaluateWithArg.UnableToAdoptMessage);
            }
        }
    }
}
