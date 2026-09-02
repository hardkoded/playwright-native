/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Serializes <see cref="IPage.EvaluateAsync{T}(string, object)"/> / <see cref="IPage.EvaluateHandleAsync(string, object)"/>
    /// arguments that contain <see cref="IJSHandle"/> values (including nested objects).
    /// </summary>
    internal static class EvaluateHandleArg
    {
        /// <summary>
        /// Parks a live handle on <c>globalThis.__pw_eh[idx]</c> so evaluate can
        /// revive it without passing <c>objectId</c> through <c>callFunctionOn</c>
        /// (WebKit rejects mixed value/objectId argument lists).
        /// </summary>
        internal const string StashFunction =
            "(h, key) => { const g = globalThis; (g.__pw_eh || (g.__pw_eh = Object.create(null)))[key] = h; return true; }";

        /// <summary>
        /// Builds a <c>Runtime.callFunctionOn</c> function plus arguments that revive
        /// nested <see cref="IJSHandle"/> placeholders before invoking
        /// <paramref name="expression"/>.
        /// </summary>
        /// <param name="expression">The page function or expression.</param>
        /// <param name="arg">The evaluate argument, possibly containing handles.</param>
        /// <param name="functionDeclaration">The wrapper function declaration.</param>
        /// <param name="callArgs">JSON tree string followed by live remote handles.</param>
        /// <returns><see langword="true"/> when <paramref name="arg"/> contains handles.</returns>
        internal static bool TryPrepareHandleCall(
            string expression,
            object arg,
            out string functionDeclaration,
            out object[] callArgs)
        {
            functionDeclaration = null;
            callArgs = null;
            EvaluateCallbacks.ThrowIfHasFunctions(arg);
            if (arg == null
                || !PageBindingResult.TryExtractHandles(arg, out object tree, out List<object> handles)
                || handles.Count == 0)
            {
                return false;
            }

            List<object> remote = new List<object>();
            object rewritten = RewriteTree(tree, handles, remote);
            functionDeclaration = WrapWithHandles(expression);
            callArgs = new object[1 + remote.Count];
            callArgs[0] = JsonSerializer.Serialize(rewritten);
            for (int i = 0; i < remote.Count; i++)
            {
                callArgs[1 + i] = remote[i];
            }

            return true;
        }

        /// <summary>
        /// Parks remote handles at <c>globalThis.__pw_eh</c> in placeholder order.
        /// </summary>
        /// <param name="callArgs">Output of <see cref="TryPrepareHandleCall"/>.</param>
        /// <returns>A task that completes when every remote handle is stashed.</returns>
        internal static async Task StashRemoteHandlesAsync(object[] callArgs)
        {
            if (callArgs == null)
            {
                return;
            }

            for (int i = 1; i < callArgs.Length; i++)
            {
                if (callArgs[i] is StashSlot slot)
                {
                    await slot.Handle.EvaluateAsync<bool>(StashFunction, slot.Key).ConfigureAwait(false);
                }
                else if (callArgs[i] is IJSHandle handle)
                {
                    await handle.EvaluateAsync<bool>(StashFunction, i.ToString(System.Globalization.CultureInfo.InvariantCulture)).ConfigureAwait(false);
                }
            }
        }

        /// <summary>
        /// Returns the JSON tree string from a <see cref="TryPrepareHandleCall"/> result.
        /// </summary>
        /// <param name="callArgs">Output of <see cref="TryPrepareHandleCall"/>.</param>
        /// <returns>The tree JSON, or <see langword="null"/>.</returns>
        internal static object TreeArgument(object[] callArgs)
            => callArgs != null && callArgs.Length > 0 ? callArgs[0] : null;

        /// <summary>
        /// Builds a <c>Runtime.evaluate</c> expression that applies the prepared
        /// handle wrapper to the JSON tree. Avoids WebKit
        /// <c>Runtime.callFunctionOn</c> argument rejection.
        /// </summary>
        /// <param name="handleFn">Output of <see cref="TryPrepareHandleCall"/>.</param>
        /// <param name="callArgs">Output of <see cref="TryPrepareHandleCall"/>.</param>
        /// <returns>An evaluable JavaScript expression.</returns>
        internal static string PreparedExpression(string handleFn, object[] callArgs)
        {
            string treeJson = Convert.ToString(TreeArgument(callArgs), System.Globalization.CultureInfo.InvariantCulture);
            return "(" + handleFn + ")(" + JsonSerializer.Serialize(treeJson) + ")";
        }

        /// <summary>
        /// Wraps <paramref name="expression"/> so evaluate can pass a JSON tree
        /// string and revive stashed handles plus inlined Infinity / -0 tokens.
        /// Primitive Infinity / -0 / NaN are inlined as <c>{ __pw_u }</c> tokens so
        /// WebKit does not have to accept CDP <c>unserializableValue</c> arguments.
        /// </summary>
        /// <param name="expression">The page function or expression.</param>
        /// <returns>A function declaration that revives handles then calls the page function.</returns>
        internal static string WrapWithHandles(string expression)
        {
            string fn = EvaluateWithArg.AsFunction(expression);
            return
                "function (treeJson) {" +
                "  const handles = globalThis.__pw_eh || Object.create(null);" +
                "  const tree = JSON.parse(treeJson);" +
                "  const revive = (v) => {" +
                "    if (v && typeof v === 'object' && typeof v.__pw_h === 'string') {" +
                "      const h = handles[v.__pw_h];" +
                "      delete handles[v.__pw_h];" +
                "      return h;" +
                "    }" +
                "    if (v && typeof v === 'object' && typeof v.__pw_u === 'string') {" +
                "      if (v.__pw_u === 'Infinity') return Infinity;" +
                "      if (v.__pw_u === '-Infinity') return -Infinity;" +
                "      if (v.__pw_u === 'NaN') return NaN;" +
                "      if (v.__pw_u === '-0') return -0;" +
                "    }" +
                "    if (Array.isArray(v)) {" +
                "      const a = [];" +
                "      for (let i = 0; i < v.length; i++) a[i] = revive(v[i]);" +
                "      return a;" +
                "    }" +
                "    if (v && typeof v === 'object') {" +
                "      const o = {};" +
                "      const keys = Object.keys(v);" +
                "      for (let i = 0; i < keys.length; i++) o[keys[i]] = revive(v[keys[i]]);" +
                "      return o;" +
                "    }" +
                "    return v;" +
                "  };" +
                "  return (" + fn + ")(revive(tree));" +
                "}";
        }

        private static object RewriteTree(object node, IReadOnlyList<object> handles, IList<object> remote)
        {
            if (node is IDictionary<string, int> placeholder
                && placeholder.Count == 1
                && placeholder.TryGetValue("__pw_h", out int index)
                && index >= 0
                && index < handles.Count)
            {
                object handle = handles[index];
                if (handle is ImmediateJSHandle immediate)
                {
                    return immediate.ToTreeValue();
                }

                StashSlot slot = new StashSlot((IJSHandle)handle, Guid.NewGuid().ToString("N"));
                remote.Add(slot);
                return new Dictionary<string, string>(StringComparer.Ordinal) { ["__pw_h"] = slot.Key };
            }

            if (node is IDictionary<string, object> map)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(System.StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> pair in map)
                {
                    copy[pair.Key] = RewriteTree(pair.Value, handles, remote);
                }

                return copy;
            }

            if (node is IDictionary dictionary)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(System.StringComparer.Ordinal);
                foreach (DictionaryEntry entry in dictionary)
                {
                    copy[System.Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture)] =
                        RewriteTree(entry.Value, handles, remote);
                }

                return copy;
            }

            if (node is IList list && node is not string)
            {
                List<object> copy = new List<object>(list.Count);
                foreach (object item in list)
                {
                    copy.Add(RewriteTree(item, handles, remote));
                }

                return copy;
            }

            return node;
        }

        private sealed class StashSlot
        {
            internal StashSlot(IJSHandle handle, string key)
            {
                Handle = handle;
                Key = key;
            }

            internal IJSHandle Handle { get; }

            internal string Key { get; }
        }
    }
}
