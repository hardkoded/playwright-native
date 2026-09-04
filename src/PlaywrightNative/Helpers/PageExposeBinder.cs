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
using System;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Wraps typed <c>exposeFunction</c> / <c>exposeBinding</c> callbacks as the
    /// <c>Func&lt;JsonElement[], Task&lt;object&gt;&gt;</c> handler installed by each browser.
    /// </summary>
    internal static class PageExposeBinder
    {
        /// <summary>
        /// Builds the caller <see cref="BindingSource"/> for a page-side binding.
        /// </summary>
        /// <param name="context">The owning context.</param>
        /// <param name="page">The owning page.</param>
        /// <returns>The source passed as the first binding argument.</returns>
        internal static BindingSource Source(IBrowserContext context, IPage page)
            => BindingSourceFactory.Create(context, page, page?.MainFrame);

        /// <summary>Wraps a no-argument <see cref="Action"/>.</summary>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap(Action callback)
            => _ =>
            {
                callback();
                return Task.FromResult<object>(null);
            };

        /// <summary>Wraps a one-argument <see cref="Action{T}"/>.</summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap<T>(Action<T> callback)
            => args =>
            {
                callback(ExposeFunctionBinder.Arg<T>(args, 0));
                return Task.FromResult<object>(null);
            };

        /// <summary>Wraps a no-argument function.</summary>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap<TResult>(Func<TResult> callback)
            => _ => ExposeFunctionBinder.InvokeAsync(callback());

        /// <summary>Wraps a one-argument function.</summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap<T, TResult>(Func<T, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(ExposeFunctionBinder.Arg<T>(args, 0)));

        /// <summary>Wraps a two-argument function.</summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap<T1, T2, TResult>(Func<T1, T2, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                ExposeFunctionBinder.Arg<T1>(args, 0),
                ExposeFunctionBinder.Arg<T2>(args, 1)));

        /// <summary>Wraps a three-argument function.</summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <typeparam name="T3">The third argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap<T1, T2, T3, TResult>(Func<T1, T2, T3, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                ExposeFunctionBinder.Arg<T1>(args, 0),
                ExposeFunctionBinder.Arg<T2>(args, 1),
                ExposeFunctionBinder.Arg<T3>(args, 2)));

        /// <summary>Wraps a four-argument function.</summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <typeparam name="T3">The third argument type.</typeparam>
        /// <typeparam name="T4">The fourth argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> Wrap<T1, T2, T3, T4, TResult>(Func<T1, T2, T3, T4, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                ExposeFunctionBinder.Arg<T1>(args, 0),
                ExposeFunctionBinder.Arg<T2>(args, 1),
                ExposeFunctionBinder.Arg<T3>(args, 2),
                ExposeFunctionBinder.Arg<T4>(args, 3)));

        /// <summary>Wraps a binding that receives only <see cref="BindingSource"/>.</summary>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="context">The owning context.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> WrapBinding<TResult>(
            IBrowserContext context,
            IPage page,
            Func<BindingSource, TResult> callback)
            => _ => ExposeFunctionBinder.InvokeAsync(callback(Source(context, page)));

        /// <summary>Wraps a binding that receives <see cref="BindingSource"/> and one argument.</summary>
        /// <typeparam name="T">The argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="context">The owning context.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> WrapBinding<T, TResult>(
            IBrowserContext context,
            IPage page,
            Func<BindingSource, T, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                Source(context, page),
                ExposeFunctionBinder.Arg<T>(args, 0)));

        /// <summary>Wraps a binding that receives <see cref="BindingSource"/> and two arguments.</summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="context">The owning context.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> WrapBinding<T1, T2, TResult>(
            IBrowserContext context,
            IPage page,
            Func<BindingSource, T1, T2, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                Source(context, page),
                ExposeFunctionBinder.Arg<T1>(args, 0),
                ExposeFunctionBinder.Arg<T2>(args, 1)));

        /// <summary>Wraps a binding that receives <see cref="BindingSource"/> and three arguments.</summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <typeparam name="T3">The third argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="context">The owning context.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> WrapBinding<T1, T2, T3, TResult>(
            IBrowserContext context,
            IPage page,
            Func<BindingSource, T1, T2, T3, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                Source(context, page),
                ExposeFunctionBinder.Arg<T1>(args, 0),
                ExposeFunctionBinder.Arg<T2>(args, 1),
                ExposeFunctionBinder.Arg<T3>(args, 2)));

        /// <summary>Wraps a binding that receives <see cref="BindingSource"/> and four arguments.</summary>
        /// <typeparam name="T1">The first argument type.</typeparam>
        /// <typeparam name="T2">The second argument type.</typeparam>
        /// <typeparam name="T3">The third argument type.</typeparam>
        /// <typeparam name="T4">The fourth argument type.</typeparam>
        /// <typeparam name="TResult">The return type.</typeparam>
        /// <param name="context">The owning context.</param>
        /// <param name="page">The owning page.</param>
        /// <param name="callback">The user callback.</param>
        /// <returns>The installed handler.</returns>
        internal static Func<JsonElement[], Task<object>> WrapBinding<T1, T2, T3, T4, TResult>(
            IBrowserContext context,
            IPage page,
            Func<BindingSource, T1, T2, T3, T4, TResult> callback)
            => args => ExposeFunctionBinder.InvokeAsync(callback(
                Source(context, page),
                ExposeFunctionBinder.Arg<T1>(args, 0),
                ExposeFunctionBinder.Arg<T2>(args, 1),
                ExposeFunctionBinder.Arg<T3>(args, 2),
                ExposeFunctionBinder.Arg<T4>(args, 3)));
    }
}
