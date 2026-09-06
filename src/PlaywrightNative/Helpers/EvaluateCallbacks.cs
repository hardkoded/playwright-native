// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Chromium;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>evaluate(..., { exposeFunctions: true })</c>: walk the argument,
    /// expose delegates as hidden page bindings, and revive them in the page.
    /// </summary>
    internal static class EvaluateCallbacks
    {
        internal const string UnexpectedFunctionPrefix = "Attempting to serialize unexpected value at position \"";

        internal const string InitScriptRequiresFunction =
            "Passing functions requires the init script to be a function";

        private const string InstallNamesFunction = @"const installNames = (names) => {
  if (!names) return;
  if (typeof globalThis.__pw_install_eval_fn__ === 'function') {
    for (let i = 0; i < names.length; i++) globalThis.__pw_install_eval_fn__(names[i]);
    return;
  }
  if (!globalThis.__pw_install_binding__) return;
  const map = globalThis.__pw_eval_fns__ || (globalThis.__pw_eval_fns__ = new Map());
  for (let i = 0; i < names.length; i++) {
    const name = names[i];
    globalThis.__pw_install_binding__(name);
    const fn = globalThis[name];
    try { delete globalThis[name]; } catch (e) { globalThis[name] = undefined; }
    if (typeof fn === 'function') map.set(name, fn);
  }
};";

        private const string ReviveFunction = @"const revive = (v) => {
  if (!v || typeof v !== 'object') return v;
  if (typeof v.__pw_fn === 'string') {
    const m = globalThis.__pw_eval_fns__;
    return m && m.get(v.__pw_fn);
  }
  if (Array.isArray(v)) {
    const a = [];
    for (let i = 0; i < v.length; i++) a[i] = revive(v[i]);
    return a;
  }
  const o = {};
  const keys = Object.keys(v);
  for (let i = 0; i < keys.length; i++) o[keys[i]] = revive(v[keys[i]]);
  return o;
};";

        /// <summary>
        /// Official <c>addInitScript(..., { exposeFunctions })</c>.
        /// </summary>
        /// <param name="page">The page.</param>
        /// <param name="script">Init script or function.</param>
        /// <param name="arg">Init-script argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <param name="runOnCurrentDocument">
        /// When <see langword="true"/>, also evaluate the wrapped script on the
        /// current document (context popup / about:blank).
        /// </param>
        /// <returns>A disposable that removes the script and its callbacks.</returns>
        internal static async Task<IAsyncDisposable> AddInitScriptTargetAsync(
            IPage page,
            string script,
            object arg,
            bool exposeFunctions,
            bool runOnCurrentDocument = false)
        {
            if (page == null)
            {
                throw new PlaywrightNativeException("Passing a function is not supported as an argument here");
            }

            if (!exposeFunctions)
            {
                await page.AddInitScriptAsync(script, DropFunctions(AddInitScriptHelper.UnwrapInitScriptArg(arg))).ConfigureAwait(false);
                return AddInitScriptHelper.CreateDisposable(() => Task.CompletedTask);
            }

            if (!EvaluateWithArg.IsFunction(script))
            {
                throw new PlaywrightNativeException(InitScriptRequiresFunction);
            }

            List<string> names = new List<string>();
            List<Func<Task>> cleanups = new List<Func<Task>>();
            object rewritten = await RewriteAndRegisterPersistentAsync(page, arg, names, cleanups)
                .ConfigureAwait(false);
            string wrapped = PrepareInitScript(script, rewritten, names);
            IAsyncDisposable installed = await page.AddInitScriptAsync(wrapped).ConfigureAwait(false);
            if (runOnCurrentDocument)
            {
                try
                {
                    await page.EvaluateAsync(wrapped).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            cleanups.Add(() => installed.DisposeAsync().AsTask());
            return AddInitScriptHelper.CreateDisposable(async () =>
            {
                for (int i = cleanups.Count - 1; i >= 0; i--)
                {
                    await cleanups[i]().ConfigureAwait(false);
                }
            });
        }

        /// <summary>
        /// Official addInitScript without <c>exposeFunctions</c>: functions are dropped.
        /// </summary>
        /// <param name="arg">The init-script argument.</param>
        /// <returns>A copy with delegates removed.</returns>
        internal static object DropFunctions(object arg)
            => DropFunctions(arg, new Dictionary<object, object>(IdentityComparer.Instance));

        /// <summary>
        /// Throws the official serialize error when <paramref name="arg"/> contains a delegate.
        /// </summary>
        /// <param name="arg">The evaluate argument.</param>
        internal static void ThrowIfHasFunctions(object arg)
        {
            if (TryFindFunction(arg, string.Empty, out string path))
            {
                if (string.IsNullOrEmpty(path))
                {
                    throw new PlaywrightNativeException("Attempting to serialize unexpected value: () => {}");
                }

                throw new PlaywrightNativeException(
                    UnexpectedFunctionPrefix + path + "\": () => {}");
            }
        }

        /// <summary>
        /// Page/frame evaluate with optional function exposure.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="target">Page or frame.</param>
        /// <param name="expression">Page function.</param>
        /// <param name="arg">Evaluate argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <returns>The evaluation result.</returns>
        internal static async Task<T> EvaluateTargetAsync<T>(object target, string expression, object arg, bool exposeFunctions)
        {
            if (!exposeFunctions)
            {
                ThrowIfHasFunctions(arg);
                return await EvaluateExistingAsync<T>(target, expression, arg).ConfigureAwait(false);
            }

            IPage page = PageOf(target);
            string script = await PreparePageScriptAsync(page, expression, arg).ConfigureAwait(false);
            return await EvaluateExistingAsync<T>(target, script, null).ConfigureAwait(false);
        }

        /// <summary>
        /// Page/frame evaluateHandle with optional function exposure.
        /// </summary>
        /// <param name="target">Page or frame.</param>
        /// <param name="expression">Page function.</param>
        /// <param name="arg">Evaluate argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <returns>The result handle.</returns>
        internal static async Task<IJSHandle> EvaluateHandleTargetAsync(object target, string expression, object arg, bool exposeFunctions)
        {
            if (!exposeFunctions)
            {
                ThrowIfHasFunctions(arg);
                return await EvaluateHandleExistingAsync(target, expression, arg).ConfigureAwait(false);
            }

            IPage page = PageOf(target);
            string script = await PreparePageScriptAsync(page, expression, arg).ConfigureAwait(false);
            IJSHandle handle = await EvaluateHandleExistingAsync(target, script, null).ConfigureAwait(false);
            return await AwaitThenableHandleAsync(handle).ConfigureAwait(false);
        }

        /// <summary>
        /// JSHandle/element evaluate with optional function exposure.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="handle">The handle.</param>
        /// <param name="expression">Page function.</param>
        /// <param name="arg">Evaluate argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <returns>The evaluation result.</returns>
        internal static async Task<T> EvaluateOnHandleAsync<T>(IJSHandle handle, string expression, object arg, bool exposeFunctions)
        {
            if (!exposeFunctions)
            {
                ThrowIfHasFunctions(arg);
                return await handle.EvaluateAsync<T>(expression, arg).ConfigureAwait(false);
            }

            IPage page = await PageOfHandleAsync(handle).ConfigureAwait(false);
            List<string> names = new List<string>();
            object rewritten = await RewriteAndRegisterAsync(page, arg, names).ConfigureAwait(false);
            string wrapped = WrapHandleExpression(expression, names);
            return await handle.EvaluateAsync<T>(wrapped, rewritten).ConfigureAwait(false);
        }

        /// <summary>
        /// Locator evaluate with optional function exposure.
        /// </summary>
        /// <typeparam name="T">Result type.</typeparam>
        /// <param name="locator">The locator.</param>
        /// <param name="expression">Page function.</param>
        /// <param name="arg">Evaluate argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <param name="timeout">Locator timeout.</param>
        /// <returns>The evaluation result.</returns>
        internal static async Task<T> EvaluateOnLocatorAsync<T>(
            ILocator locator,
            string expression,
            object arg,
            bool exposeFunctions,
            float? timeout)
        {
            IJSHandle handle = await locator.ElementHandleAsync(timeout).ConfigureAwait(false);
            return await EvaluateOnHandleAsync<T>(handle, expression, arg, exposeFunctions).ConfigureAwait(false);
        }

        /// <summary>
        /// Locator evaluateHandle with optional function exposure.
        /// </summary>
        /// <param name="locator">The locator.</param>
        /// <param name="expression">Page function.</param>
        /// <param name="arg">Evaluate argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <param name="timeout">Locator timeout.</param>
        /// <returns>The result handle.</returns>
        internal static async Task<IJSHandle> EvaluateHandleOnLocatorAsync(
            ILocator locator,
            string expression,
            object arg,
            bool exposeFunctions,
            float? timeout)
        {
            IJSHandle handle = await locator.ElementHandleAsync(timeout).ConfigureAwait(false);
            return await EvaluateHandleOnHandleAsync(handle, expression, arg, exposeFunctions).ConfigureAwait(false);
        }

        /// <summary>
        /// JSHandle evaluateHandle with optional function exposure.
        /// </summary>
        /// <param name="handle">The handle.</param>
        /// <param name="expression">Page function.</param>
        /// <param name="arg">Evaluate argument.</param>
        /// <param name="exposeFunctions">Official <c>exposeFunctions</c>.</param>
        /// <returns>The result handle.</returns>
        internal static async Task<IJSHandle> EvaluateHandleOnHandleAsync(IJSHandle handle, string expression, object arg, bool exposeFunctions)
        {
            if (!exposeFunctions)
            {
                ThrowIfHasFunctions(arg);
                return await handle.EvaluateHandleAsync(expression, arg).ConfigureAwait(false);
            }

            IPage page = await PageOfHandleAsync(handle).ConfigureAwait(false);
            List<string> names = new List<string>();
            object rewritten = await RewriteAndRegisterAsync(page, arg, names).ConfigureAwait(false);
            string wrapped = WrapHandleExpression(expression, names);
            IJSHandle result = await handle.EvaluateHandleAsync(wrapped, rewritten).ConfigureAwait(false);
            return await AwaitThenableHandleAsync(result).ConfigureAwait(false);
        }

        private static async Task<string> PreparePageScriptAsync(IPage page, string expression, object arg)
        {
            List<string> names = new List<string>();
            object rewritten = await RewriteAndRegisterAsync(page, arg, names).ConfigureAwait(false);
            string argJson = JsonSerializer.Serialize(rewritten);
            string namesJson = JsonSerializer.Serialize(names);
            return "(async () => { " + InstallNamesFunction + " installNames(" + namesJson + "); " + ReviveFunction + " return await (" + expression + ")(revive(" + argJson + ")); })()";
        }

        private static string WrapHandleExpression(string expression, IReadOnlyList<string> names)
        {
            string namesJson = JsonSerializer.Serialize(names);
            return "async function(el, arg) { " + InstallNamesFunction + " installNames(" + namesJson + "); " + ReviveFunction + " return await (" + expression + ")(el, revive(arg)); }";
        }

        private static async Task<object> RewriteAndRegisterAsync(IPage page, object arg)
            => await RewriteAndRegisterAsync(page, arg, new List<string>()).ConfigureAwait(false);

        private static string PrepareInitScript(string expression, object arg, IReadOnlyList<string> names)
        {
            string argJson = JsonSerializer.Serialize(arg);
            string namesJson = JsonSerializer.Serialize(names);
            return "(() => { " + InstallNamesFunction + " installNames(" + namesJson + "); " + ReviveFunction + " try { (" + expression + ")(revive(" + argJson + ")); } catch (e) {} })()";
        }

        private static async Task<object> RewriteAndRegisterAsync(IPage page, object arg, List<string> installed)
        {
            if (page == null)
            {
                throw new PlaywrightNativeException("Passing a function is not supported as an argument here");
            }

            Dictionary<Delegate, string> names = new Dictionary<Delegate, string>();
            object rewritten = Rewrite(arg, names, new Dictionary<object, object>(IdentityComparer.Instance));
            foreach (KeyValuePair<Delegate, string> pair in names)
            {
                Delegate callback = pair.Key;
                await RegisterAsync(page, pair.Value, args => InvokeDelegateAsync(callback, args)).ConfigureAwait(false);
                installed.Add(pair.Value);
            }

            return rewritten;
        }

        private static async Task<object> RewriteAndRegisterPersistentAsync(
            IPage page,
            object arg,
            List<string> installed,
            List<Func<Task>> cleanups)
        {
            Dictionary<Delegate, string> names = new Dictionary<Delegate, string>();
            object rewritten = Rewrite(arg, names, new Dictionary<object, object>(IdentityComparer.Instance));
            foreach (KeyValuePair<Delegate, string> pair in names)
            {
                Delegate callback = pair.Key;
                string identifier = await RegisterPersistentAsync(
                    page,
                    pair.Value,
                    args => InvokeDelegateAsync(callback, args)).ConfigureAwait(false);
                installed.Add(pair.Value);
                string name = pair.Value;
                cleanups.Add(() => UnregisterPersistentAsync(page, name, identifier));
            }

            return rewritten;
        }

        private static object DropFunctions(object value, IDictionary<object, object> seen)
        {
            if (value == null || value is Delegate)
            {
                return null;
            }

            if (value is IJSHandle || value is string || value.GetType().IsPrimitive
                || value is decimal || value is DateTime || value is DateTimeOffset || value is Guid || value is Uri
                || value is JsonElement)
            {
                return value;
            }

            if (seen.TryGetValue(value, out object existing))
            {
                return existing;
            }

            if (value is IDictionary dictionary)
            {
                Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.Ordinal);
                seen[value] = map;
                foreach (DictionaryEntry entry in dictionary)
                {
                    if (entry.Value is Delegate)
                    {
                        continue;
                    }

                    map[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] =
                        DropFunctions(entry.Value, seen);
                }

                return map;
            }

            if (value is IEnumerable enumerable)
            {
                List<object> list = new List<object>();
                seen[value] = list;
                foreach (object item in enumerable)
                {
                    list.Add(item is Delegate ? null : DropFunctions(item, seen));
                }

                return list;
            }

            Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.Ordinal);
            seen[value] = obj;
            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                object propertyValue = property.GetValue(value);
                if (propertyValue is Delegate)
                {
                    continue;
                }

                obj[property.Name] = DropFunctions(propertyValue, seen);
            }

            return obj;
        }

        private static object Rewrite(object value, IDictionary<Delegate, string> names, IDictionary<object, object> seen)
        {
            if (value == null)
            {
                return null;
            }

            if (value is Delegate callback)
            {
                if (!names.TryGetValue(callback, out string name))
                {
                    name = "pw_fn_" + Guid.NewGuid().ToString("N", CultureInfo.InvariantCulture);
                    names[callback] = name;
                }

                return new Dictionary<string, string>(StringComparer.Ordinal) { ["__pw_fn"] = name };
            }

            if (value is IJSHandle || value is string || value.GetType().IsPrimitive
                || value is decimal || value is DateTime || value is DateTimeOffset || value is Guid || value is Uri
                || value is JsonElement)
            {
                return value;
            }

            if (seen.TryGetValue(value, out object existing))
            {
                return existing;
            }

            if (value is IDictionary dictionary)
            {
                Dictionary<string, object> map = new Dictionary<string, object>(StringComparer.Ordinal);
                seen[value] = map;
                foreach (DictionaryEntry entry in dictionary)
                {
                    map[Convert.ToString(entry.Key, CultureInfo.InvariantCulture)] =
                        Rewrite(entry.Value, names, seen);
                }

                return map;
            }

            if (value is IEnumerable enumerable)
            {
                List<object> list = new List<object>();
                seen[value] = list;
                foreach (object item in enumerable)
                {
                    list.Add(Rewrite(item, names, seen));
                }

                return list;
            }

            Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.Ordinal);
            seen[value] = obj;
            foreach (PropertyInfo property in value.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                obj[property.Name] = Rewrite(property.GetValue(value), names, seen);
            }

            return obj;
        }

        private static async Task<object> InvokeDelegateAsync(Delegate callback, JsonElement[] args)
        {
            ParameterInfo[] parameters = callback.Method.GetParameters();
            object[] invokeArgs = new object[parameters.Length];
            for (int i = 0; i < parameters.Length; i++)
            {
                invokeArgs[i] = ExposeFunctionBinder.Arg(args, i, parameters[i].ParameterType);
            }

            object result;
            try
            {
                result = callback.DynamicInvoke(invokeArgs);
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }

            return await ExposeFunctionBinder.InvokeAsync(result).ConfigureAwait(false);
        }

        private static bool TryFindFunction(object value, string path, out string found)
        {
            found = null;
            if (value == null || value is IJSHandle || value is string || value is JsonElement)
            {
                return false;
            }

            if (value is Delegate)
            {
                found = path;
                return true;
            }

            Type type = value.GetType();
            if (type.IsPrimitive || value is decimal || value is DateTime || value is DateTimeOffset || value is Guid || value is Uri)
            {
                return false;
            }

            if (value is IDictionary dictionary)
            {
                foreach (DictionaryEntry entry in dictionary)
                {
                    string child = Combine(path, Convert.ToString(entry.Key, CultureInfo.InvariantCulture));
                    if (TryFindFunction(entry.Value, child, out found))
                    {
                        return true;
                    }
                }

                return false;
            }

            if (value is IEnumerable enumerable)
            {
                int index = 0;
                foreach (object item in enumerable)
                {
                    if (TryFindFunction(item, path + "[" + index.ToString(CultureInfo.InvariantCulture) + "]", out found))
                    {
                        return true;
                    }

                    index++;
                }

                return false;
            }

            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                if (TryFindFunction(property.GetValue(value), Combine(path, property.Name), out found))
                {
                    return true;
                }
            }

            return false;
        }

        private static string Combine(string path, string name)
            => string.IsNullOrEmpty(path) ? name ?? string.Empty : path + "." + name;

        private static async Task RegisterAsync(IPage page, string name, Func<JsonElement[], Task<object>> handler)
        {
            if (page is Page chromium)
            {
                await chromium.CrPage.RegisterEvaluateCallbackAsync(name, handler).ConfigureAwait(false);
                return;
            }

            if (page is WKPage webkit)
            {
                await webkit.RegisterEvaluateCallbackAsync(name, handler).ConfigureAwait(false);
                return;
            }

            throw new PlaywrightNativeException("Passing a function is not supported as an argument here");
        }

        private static Task<string> RegisterPersistentAsync(IPage page, string name, Func<JsonElement[], Task<object>> handler)
        {
            if (page is Page chromium)
            {
                return chromium.CrPage.RegisterPersistentEvalFnAsync(name, handler);
            }

            if (page is WKPage webkit)
            {
                return webkit.RegisterPersistentEvalFnAsync(name, handler);
            }

            throw new PlaywrightNativeException("Passing a function is not supported as an argument here");
        }

        private static Task UnregisterPersistentAsync(IPage page, string name, string identifier)
        {
            if (page is Page chromium)
            {
                return chromium.CrPage.UnregisterPersistentEvalFnAsync(name, identifier);
            }

            if (page is WKPage webkit)
            {
                return webkit.UnregisterPersistentEvalFnAsync(name, identifier);
            }

            return Task.CompletedTask;
        }

        private static IPage PageOf(object target)
        {
            if (target is IPage page)
            {
                return page;
            }

            if (target is IFrame frame)
            {
                return frame.Page;
            }

            return null;
        }

        private static async Task<IPage> PageOfHandleAsync(IJSHandle handle)
        {
            if (handle is IElementHandle element)
            {
                IFrame frame = await element.OwnerFrameAsync().ConfigureAwait(false);
                return frame?.Page;
            }

            if (handle is ChromiumJSHandle chromium)
            {
                return chromium.CrPage?.PublicPage;
            }

            if (handle is WKJSHandle webkit)
            {
                return webkit.OwnerPage;
            }

            return null;
        }

        private static Task<T> EvaluateExistingAsync<T>(object target, string expression, object arg)
        {
            if (target is IPage page)
            {
                return page.EvaluateAsync<T>(expression, arg);
            }

            if (target is IFrame frame)
            {
                return frame.EvaluateAsync<T>(expression, arg);
            }

            throw new PlaywrightNativeException("Cannot evaluate on this target.");
        }

        private static async Task<IJSHandle> AwaitThenableHandleAsync(IJSHandle handle)
        {
            if (handle == null || handle is ImmediateJSHandle)
            {
                return handle;
            }

            return await handle.EvaluateHandleAsync("async v => await v").ConfigureAwait(false);
        }

        private static Task<IJSHandle> EvaluateHandleExistingAsync(object target, string expression, object arg)
        {
            if (target is IPage page)
            {
                return page.EvaluateHandleAsync(expression, arg);
            }

            if (target is IFrame frame)
            {
                return frame.EvaluateHandleAsync(expression, arg);
            }

            throw new PlaywrightNativeException("Cannot evaluate on this target.");
        }

        private sealed class IdentityComparer : IEqualityComparer<object>
        {
            internal static readonly IdentityComparer Instance = new IdentityComparer();

            bool IEqualityComparer<object>.Equals(object x, object y) => ReferenceEquals(x, y);

            int IEqualityComparer<object>.GetHashCode(object obj)
                => System.Runtime.CompilerServices.RuntimeHelpers.GetHashCode(obj);
        }
    }
}
