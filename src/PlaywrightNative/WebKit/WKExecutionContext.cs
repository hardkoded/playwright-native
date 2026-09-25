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
using Microsoft.Playwright;
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

        private Task<string> _utilityScriptObjectId;

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
        /// Gets the WIP session this context evaluates on.
        /// </summary>
        internal WKTargetSession Session => _session;

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
        internal void MarkDestroyed()
        {
            _utilityScriptObjectId = null;
            _destroyed.TrySetResult(true);
        }

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
            if (_destroyed.Task.IsCompleted)
            {
                throw ClosedOrNavigationException();
            }

            JsonElement? response;
            try
            {
                response = await _session.SendAsync(
                    "Runtime.evaluate",
                    BuildEvaluateParams(expression, returnByValue: false)).ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
                // Page/browser close sets IsClosing before the session dies.
                // A navigation target swap disposes the session without that
                // flag; isVisible must see a destroyed-context error then.
                throw ClosedOrNavigationException();
            }

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
        /// Evaluates <paramref name="expression"/> via <c>Runtime.callFunctionOn</c> with
        /// <c>emulateUserGesture: true</c> and <c>awaitPromise: true</c>, bound to a
        /// page-world UtilityScript object — the same wire shape as upstream
        /// <c>wkExecutionContext.evaluateWithArguments</c> + <c>utilityScript.evaluate</c>.
        /// Darwin OOPIF <c>requestStorageAccess</c> also needs a page-proxy Input pulse
        /// into the iframe (cross-origin frames do not inherit parent transient
        /// activation); pulse after UtilityScript install and immediately before CFO
        /// so activation is still live. <paramref name="expression"/> should be the
        /// raw function form (e.g. <c>() =&gt; …</c>), matching <c>isFunction: true</c>.
        /// </summary>
        /// <param name="expression">The JavaScript function or expression to evaluate.</param>
        /// <param name="pulseTrustedGestureAsync">
        /// Optional page-proxy Input click invoked after UtilityScript install and
        /// immediately before <c>callFunctionOn</c>.
        /// </param>
        /// <returns>The raw <c>result</c> remote object, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> EvaluateHandleWithUserGestureAsync(
            string expression,
            Func<Task> pulseTrustedGestureAsync = null)
        {
            if (_destroyed.Task.IsCompleted)
            {
                throw ClosedOrNavigationException();
            }

            // Install first — Runtime.evaluate for UtilityScript is slow enough that
            // pulsing beforehand lets Darwin transient activation expire.
            string utilityId = await EnsureUtilityScriptObjectIdAsync().ConfigureAwait(false);

            if (pulseTrustedGestureAsync != null)
            {
                await pulseTrustedGestureAsync().ConfigureAwait(false);
            }

            // Exact upstream evaluateExpression → utilityScriptValues shape:
            // [isFunction, returnByValue, serialize, expression, argCount, ...args]
            const string functionDeclaration =
                "(utilityScript, ...args) => utilityScript.evaluate(...args)";

            JsonElement? response;
            try
            {
                response = await _session.SendAsync(
                    "Runtime.callFunctionOn",
                    new
                    {
                        objectId = utilityId,
                        functionDeclaration,
                        arguments = new object[]
                        {
                            new { objectId = utilityId },
                            new { value = true },
                            new { value = true },
                            new { value = (object)null },
                            new { value = expression },
                            new { value = 0 },
                        },
                        returnByValue = true,
                        emulateUserGesture = true,
                        awaitPromise = true,
                    }).ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
                throw ClosedOrNavigationException();
            }

            if (response == null)
            {
                return null;
            }

            ThrowIfThrown(response.Value);
            return response.Value.TryGetProperty("result", out JsonElement result)
                ? result
                : null;
        }

        /// <summary>
        /// Awaits a Promise previously stored on <c>window[promiseProperty]</c> via
        /// <c>Runtime.callFunctionOn</c> with <c>awaitPromise: true</c>. Used after a
        /// page-proxy Input pulse so Darwin <c>requestStorageAccess</c> can finish under
        /// the real gesture listener that armed the promise.
        /// </summary>
        /// <param name="promiseProperty">
        /// Property name on <c>window</c> holding the Promise (e.g. <c>__pw_rsa_promise</c>).
        /// </param>
        /// <returns>The settled remote <c>result</c>, or <see langword="null"/>.</returns>
        internal async Task<JsonElement?> AwaitWindowPromiseAsync(string promiseProperty)
        {
            if (string.IsNullOrEmpty(promiseProperty))
            {
                throw new ArgumentNullException(nameof(promiseProperty));
            }

            if (_destroyed.Task.IsCompleted)
            {
                throw ClosedOrNavigationException();
            }

            object anchorParams = _contextId.HasValue
                ? new { expression = "window", contextId = _contextId.Value, returnByValue = false }
                : (object)new { expression = "window", returnByValue = false };
            JsonElement? anchorResponse = await _session.SendAsync("Runtime.evaluate", anchorParams)
                .ConfigureAwait(false);
            if (anchorResponse == null)
            {
                return null;
            }

            ThrowIfThrown(anchorResponse.Value);
            if (!anchorResponse.Value.TryGetProperty("result", out JsonElement anchorResult))
            {
                return null;
            }

            string anchorId = RemoteObject.GetObjectId(anchorResult);
            if (string.IsNullOrEmpty(anchorId))
            {
                return null;
            }

            string functionDeclaration =
                "function () { return this[" + JsonSerializer.Serialize(promiseProperty) + "]; }";
            try
            {
                JsonElement? response = await _session.SendAsync(
                    "Runtime.callFunctionOn",
                    new
                    {
                        objectId = anchorId,
                        functionDeclaration,
                        returnByValue = true,
                        awaitPromise = true,
                    }).ConfigureAwait(false);
                if (response == null)
                {
                    return null;
                }

                ThrowIfThrown(response.Value);
                return response.Value.TryGetProperty("result", out JsonElement result)
                    ? result
                    : null;
            }
            catch (TargetClosedException)
            {
                throw ClosedOrNavigationException();
            }
            finally
            {
                await ReleaseHandleAsync(anchorId).ConfigureAwait(false);
            }
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
            catch (PlaywrightException ex)
            {
                if (HasForeignElementArgument(args)
                    && (ex.Message.Contains("adopt", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("different document", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("Cannot find context", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
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
            catch (PlaywrightException ex)
            {
                if (HasForeignElementArgument(args)
                    && (ex.Message.Contains("adopt", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("different document", StringComparison.OrdinalIgnoreCase)
                        || ex.Message.Contains("Cannot find context", StringComparison.OrdinalIgnoreCase)))
                {
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
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
            // Race awaitPromise against context destruction — Promise evaluates
            // (page-evaluate "nice error after navigation") hang on WebKit reload
            // if callFunctionOn is not aborted when the old world goes away.
            JsonElement? response = await RaceDestroyedAsync(_session.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = BuildHandleArguments(objectId, args),
                returnByValue = true,
                emulateUserGesture = true,
                awaitPromise = true,
            })).ConfigureAwait(false);

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
            JsonElement? response = await RaceDestroyedAsync(_session.SendAsync("Runtime.callFunctionOn", new
            {
                functionDeclaration,
                objectId,
                arguments = BuildHandleArguments(objectId, args),
                returnByValue = false,
                emulateUserGesture = true,
                awaitPromise = true,
            })).ConfigureAwait(false);

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
            catch (PlaywrightException)
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
        /// Evaluates a structured-clone wrapped expression. Prefers
        /// <c>returnByValue: true</c> so synchronous tagged results (including after a
        /// same-turn navigation) avoid a second protocol call. Thenables that lose their
        /// Promise shape under by-value are re-fetched as handles and awaited via
        /// <c>Runtime.callFunctionOn</c>. Abort when this context is destroyed.
        /// </summary>
        /// <remarks>
        /// WebKit has no <c>awaitPromise</c> on <c>Runtime.evaluate</c>. Returning an
        /// untagged empty by-value object for a Promise (common with exposeFunction)
        /// must not be treated as success — that deserializes as <c>default(T)</c> (0).
        /// A handle re-evaluate may re-run page side effects; <c>WKPage</c> coalesces
        /// binding invocations by frame+name+args (ignoring seq) so host callbacks
        /// still fire once.
        /// </remarks>
        /// <param name="expression">An expression that returns a tagged payload or a promise of one.</param>
        /// <returns>The remote object (<c>result</c>) for <see cref="EvaluateSerialization.ParseRemote{T}"/>.</returns>
        internal async Task<JsonElement?> EvaluateSerializedRemoteAsync(string expression)
        {
            // Prefer returnByValue:true so synchronous tagged completion values (including
            // after location.reload()) arrive in one round-trip without racing context
            // destruction on callFunctionOn. Stash the first-run value so a Promise handle
            // recover does not re-execute page side effects.
            string stashKey = "__pw_eval_" + Guid.NewGuid().ToString("N");
            string stashedExpression =
                "(() => { const __pw_r = (" + expression + "); globalThis[" +
                JsonSerializer.Serialize(stashKey) +
                "] = __pw_r; return __pw_r; })()";
            string recoverExpression =
                "(() => { const __pw_k = " + JsonSerializer.Serialize(stashKey) +
                "; const __pw_v = globalThis[__pw_k]; try { delete globalThis[__pw_k]; } catch (e) {} return __pw_v; })()";

            JsonElement? byValueResponse = await _session.SendAsync(
                "Runtime.evaluate",
                BuildEvaluateParams(stashedExpression, returnByValue: true)).ConfigureAwait(false);
            if (byValueResponse == null)
            {
                return null;
            }

            JsonElement response = byValueResponse.Value;
            ThrowIfThrown(response);
            if (!response.TryGetProperty("result", out JsonElement result))
            {
                return null;
            }

            // Finished structured-clone payloads (and undefined) — including sync
            // navigation returns. Do not treat empty/untagged objects as done: those
            // are how WebKit often serializes Promises under returnByValue:true.
            if (IsTaggedRemote(result))
            {
                return result;
            }

            string objectId = RemoteObject.GetObjectId(result);
            if (string.IsNullOrEmpty(objectId))
            {
                JsonElement? asHandle = await _session.SendAsync(
                    "Runtime.evaluate",
                    BuildEvaluateParams(recoverExpression, returnByValue: false)).ConfigureAwait(false);
                if (asHandle == null)
                {
                    return null;
                }

                ThrowIfThrown(asHandle.Value);
                if (!asHandle.Value.TryGetProperty("result", out JsonElement handleResult))
                {
                    return null;
                }

                if (IsTaggedRemote(handleResult))
                {
                    return handleResult;
                }

                objectId = RemoteObject.GetObjectId(handleResult);
                if (string.IsNullOrEmpty(objectId))
                {
                    return handleResult;
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
                : awaited.Value;
        }

        /// <summary>
        /// Returns an object id usable in this context, adopting ElementHandles
        /// from another same-origin world and rejecting foreign JSHandles.
        /// </summary>
        /// <param name="handle">A WebKit JS or element handle.</param>
        /// <returns>An object id owned by this execution context.</returns>
        internal async Task<string> ResolveHandleObjectIdAsync(WKJSHandle handle)
        {
            if (handle == null || string.IsNullOrEmpty(handle.ObjectId))
            {
                throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
            }

            if (handle.ExecutionContext != null && handle.ExecutionContext.ContextId == ContextId)
            {
                return handle.ObjectId;
            }

            if (handle.AsElement() == null)
            {
                throw new PlaywrightException(DispatchEventScript.DifferentContextMessage);
            }

            return await AdoptElementObjectIdAsync(handle.ObjectId).ConfigureAwait(false);
        }

        /// <summary>
        /// Adopts a DOM node object id into this execution context via
        /// <c>DOM.resolveNode</c> (official <c>wkPage.adoptElementHandle</c>).
        /// </summary>
        /// <param name="objectId">Source element object id from another context.</param>
        /// <returns>The adopted object id in this context.</returns>
        internal async Task<string> AdoptElementObjectIdAsync(string objectId)
        {
            if (string.IsNullOrEmpty(objectId) || !_contextId.HasValue)
            {
                throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
            }

            try
            {
                JsonElement? resolved = await _session.SendAsync("DOM.resolveNode", new
                {
                    objectId,
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
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
                }

                string adopted = adoptedId.GetString();
                if (string.IsNullOrEmpty(adopted))
                {
                    throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
                }

                return adopted;
            }
            catch (PlaywrightException ex)
            {
                if (string.Equals(ex.Message, EvaluateWithArg.UnableToAdoptMessage, StringComparison.Ordinal))
                {
                    throw;
                }

                throw new PlaywrightException(EvaluateWithArg.UnableToAdoptMessage);
            }
        }

        /// <summary>
        /// Returns whether a WIP remote object is a Promise (upstream
        /// <c>createHandle</c> checks <c>className === 'Promise'</c>).
        /// </summary>
        /// <param name="result">A <c>Runtime.RemoteObject</c>.</param>
        /// <returns><see langword="true"/> when the object is a Promise.</returns>
        private static bool IsPromiseRemote(JsonElement result)
        {
            if (result.TryGetProperty("className", out JsonElement className)
                && className.ValueKind == JsonValueKind.String
                && string.Equals(className.GetString(), "Promise", StringComparison.Ordinal))
            {
                return true;
            }

            return result.TryGetProperty("subtype", out JsonElement subtype)
                && subtype.ValueKind == JsonValueKind.String
                && string.Equals(subtype.GetString(), "promise", StringComparison.Ordinal);
        }

        /// <summary>
        /// Returns whether a by-value remote object is a finished
        /// <see cref="EvaluateSerialization"/> payload (or <c>undefined</c>),
        /// as opposed to an empty object shell left when WebKit serializes a Promise.
        /// </summary>
        /// <param name="result">A <c>Runtime.RemoteObject</c>.</param>
        /// <returns><see langword="true"/> when the value is a usable tagged payload.</returns>
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

            throw new PlaywrightException(EvaluateSerialization.RewriteError(message));
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

        private Task<string> EnsureUtilityScriptObjectIdAsync()
        {
            if (_utilityScriptObjectId == null)
            {
                _utilityScriptObjectId = InstallUtilityScriptObjectIdAsync();
            }

            return _utilityScriptObjectId;
        }

        private async Task<string> InstallUtilityScriptObjectIdAsync()
        {
            // Minimal UtilityScript.evaluate matching upstream injected/utilityScript.ts
            // + javascript.ts evaluateExpression values:
            // [isFunction, returnByValue, serialize, expression, argCount, ...args].
            // Use globalThis.eval so the page function closes over the frame global.
            // When returnByValue, serialize booleans/primitives like
            // utilityScriptSerializers so ParseRemote sees { b: true }.
            // Promise results use an async IIFE so WebKit's awaitPromise sees a
            // native Promise (upstream _promiseAwareJsonValueNoThrow).
            const string source =
                @"(() => {
  const global = globalThis;
  const serialize = (v) => {
    if (Object.is(v, undefined)) return { v: 'undefined' };
    if (Object.is(v, null)) return { v: 'null' };
    const type = typeof v;
    if (type === 'boolean') return { b: v };
    if (type === 'number') return { n: v };
    if (type === 'string') return { s: v };
    return v;
  };
  const promiseAware = (value, returnByValue) => {
    const wrap = (v) => returnByValue ? serialize(v) : v;
    if (value && typeof value === 'object' && typeof value.then === 'function') {
      return (async () => wrap(await value))();
    }
    return wrap(value);
  };
  return {
    evaluate(isFunction, returnByValue, _serialize, expression, argCount, ...argsAndHandles) {
      const args = argsAndHandles.slice(0, argCount || 0);
      let result = global.eval(expression);
      if (isFunction === true) {
        result = result(...args);
      } else if (isFunction === false) {
        result = result;
      } else if (typeof result === 'function') {
        result = result(...args);
      }
      return promiseAware(result, returnByValue);
    }
  };
})()";

            object evalParams = _contextId.HasValue
                ? new { expression = source, contextId = _contextId.Value, returnByValue = false, emulateUserGesture = false }
                : (object)new { expression = source, returnByValue = false, emulateUserGesture = false };

            JsonElement? response = await _session.SendAsync("Runtime.evaluate", evalParams)
                .ConfigureAwait(false);
            if (response == null)
            {
                throw new PlaywrightException("Failed to install UtilityScript");
            }

            ThrowIfThrown(response.Value);
            if (!response.Value.TryGetProperty("result", out JsonElement result))
            {
                throw new PlaywrightException("Failed to install UtilityScript");
            }

            string objectId = RemoteObject.GetObjectId(result);
            if (string.IsNullOrEmpty(objectId))
            {
                throw new PlaywrightException("Failed to install UtilityScript");
            }

            return objectId;
        }

        private async Task<JsonElement?> SendEvaluateAsync(string expression)
        {
            // WebKit Runtime.evaluate has no awaitPromise. Prefer returnByValue:true for
            // sync values (including after sync navigation). Untagged empty objects from
            // Promise by-value serialization must still be awaited via a handle.
            // Stash the first-run result so the handle recover does not re-execute page
            // side effects (exposeFunction / evaluate callbacks).
            if (_destroyed.Task.IsCompleted)
            {
                throw ClosedOrNavigationException();
            }

            string stashKey = "__pw_eval_" + Guid.NewGuid().ToString("N");
            string stashedExpression =
                "(() => { const __pw_r = (" + expression + "); globalThis[" +
                JsonSerializer.Serialize(stashKey) +
                "] = __pw_r; return __pw_r; })()";
            string recoverExpression =
                "(() => { const __pw_k = " + JsonSerializer.Serialize(stashKey) +
                "; const __pw_v = globalThis[__pw_k]; try { delete globalThis[__pw_k]; } catch (e) {} return __pw_v; })()";

            JsonElement? evalResponse;
            try
            {
                evalResponse = await _session.SendAsync(
                    "Runtime.evaluate",
                    BuildEvaluateParams(stashedExpression, returnByValue: true)).ConfigureAwait(false);
            }
            catch (TargetClosedException)
            {
                // Same as EvaluateHandleAsync: process-swap disposes the session without
                // IsClosing. Surface the navigation destroyed-context error so callers
                // (SetContent) can wait for the replacement world instead of failing as
                // "Target page, context or browser has been closed".
                throw ClosedOrNavigationException();
            }

            if (evalResponse == null)
            {
                return null;
            }

            JsonElement evalElement = evalResponse.Value;

            bool threw = evalElement.TryGetProperty("wasThrown", out JsonElement wasThrown)
                && wasThrown.ValueKind == JsonValueKind.True;
            if (threw || !evalElement.TryGetProperty("result", out JsonElement result))
            {
                return evalResponse;
            }

            if (IsTaggedRemote(result))
            {
                return evalResponse;
            }

            // Non-object primitives are final even when untagged.
            if (result.TryGetProperty("type", out JsonElement typeEl)
                && typeEl.ValueKind == JsonValueKind.String)
            {
                string typeName = typeEl.GetString();
                if (string.Equals(typeName, "number", StringComparison.Ordinal)
                    || string.Equals(typeName, "string", StringComparison.Ordinal)
                    || string.Equals(typeName, "boolean", StringComparison.Ordinal)
                    || string.Equals(typeName, "bigint", StringComparison.Ordinal)
                    || string.Equals(typeName, "undefined", StringComparison.Ordinal)
                    || string.Equals(typeName, "symbol", StringComparison.Ordinal))
                {
                    return evalResponse;
                }
            }

            string objectId = RemoteObject.GetObjectId(result);
            if (string.IsNullOrEmpty(objectId))
            {
                JsonElement? asHandle;
                try
                {
                    asHandle = await _session.SendAsync(
                        "Runtime.evaluate",
                        BuildEvaluateParams(recoverExpression, returnByValue: false)).ConfigureAwait(false);
                }
                catch (TargetClosedException)
                {
                    throw ClosedOrNavigationException();
                }

                if (asHandle == null)
                {
                    return null;
                }

                JsonElement handleElement = asHandle.Value;
                if (handleElement.TryGetProperty("wasThrown", out JsonElement handleThrown)
                    && handleThrown.ValueKind == JsonValueKind.True)
                {
                    return asHandle;
                }

                if (!handleElement.TryGetProperty("result", out JsonElement handleResult))
                {
                    return asHandle;
                }

                if (IsTaggedRemote(handleResult))
                {
                    return asHandle;
                }

                objectId = RemoteObject.GetObjectId(handleResult);
                if (string.IsNullOrEmpty(objectId))
                {
                    return asHandle;
                }
            }

            return await AwaitOrDestroyAsync(objectId).ConfigureAwait(false);
        }

        private async Task<JsonElement?> AwaitOrDestroyAsync(string resultId)
        {
            if (_destroyed.Task.IsCompleted)
            {
                throw ClosedOrNavigationException();
            }

            JsonElement? dummyResponse = await RaceDestroyedAsync(_session.SendAsync(
                "Runtime.evaluate",
                BuildEvaluateParams("({})", returnByValue: false))).ConfigureAwait(false);
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
                return await RaceDestroyedAsync(awaitTask).ConfigureAwait(false);
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

        /// <summary>
        /// Races a WIP evaluate against context destruction so navigations fail
        /// pending <c>awaitPromise</c> calls with the official navigation message.
        /// </summary>
        /// <typeparam name="T">The protocol response type.</typeparam>
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
                throw DestroyedEvaluateException();
            }

            await Task.WhenAny(task, _destroyed.Task).ConfigureAwait(false);

            // Prefer a completed protocol response when destroy races the same turn
            // (evaluate that navigates must return its value).
            if (task.IsCompleted)
            {
                try
                {
                    return await task.ConfigureAwait(false);
                }
                catch (TargetClosedException)
                {
                    throw;
                }
                catch (PlaywrightException ex) when (
                    ex.Message != null
                    && (ex.Message.Contains("Cannot find context with specified id", StringComparison.Ordinal)
                        || ex.Message.Contains("Cannot find object with given id", StringComparison.Ordinal)
                        || ex.Message.Contains("Execution context was destroyed", StringComparison.Ordinal)
                        || ex.Message.Contains("Inspected target navigated or closed", StringComparison.Ordinal)))
                {
                    throw DestroyedEvaluateException();
                }
            }

            throw DestroyedEvaluateException();
        }

        private Exception DestroyedEvaluateException()
            => ClosedOrNavigationException();

        private Exception ClosedOrNavigationException()
        {
            // Browser/page close marks the session closing before (or instead of)
            // surfacing TargetClosedException. A disposed session without that
            // flag is a navigation target swap — isVisible must not treat it as
            // "target closed".
            if (_session.IsClosing || _session.IsConnectionClosed)
            {
                return ClosedTarget.Exception(
                    DriverMessages.BrowserOrContextClosedExceptionMessage,
                    _session.CloseReason);
            }

            return new PlaywrightException(EvaluateSerialization.NavigationMessage);
        }

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
                throw new PlaywrightException(DispatchEventScript.DifferentContextMessage);
            }

            string adopted = await AdoptElementObjectIdAsync(handle.ObjectId).ConfigureAwait(false);
            return new { objectId = adopted };
        }
    }
}
