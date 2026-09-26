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
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Deserializes page-side binding arguments into typed .NET values.
    /// </summary>
    internal static class ExposeFunctionBinder
    {
        /// <summary>
        /// Reads argument <paramref name="index"/> as <paramref name="type"/>.
        /// </summary>
        /// <param name="args">Binding arguments.</param>
        /// <param name="index">Zero-based argument index.</param>
        /// <param name="type">The target CLR type.</param>
        /// <returns>The deserialized value.</returns>
        internal static object Arg(JsonElement[] args, int index, Type type)
        {
            MethodInfo method = typeof(ExposeFunctionBinder)
                .GetMethod(nameof(Arg), 1, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static, binder: null, new[] { typeof(JsonElement[]), typeof(int) }, modifiers: null)
                ?? throw new InvalidOperationException("ExposeFunctionBinder.Arg<T> was not found.");
            try
            {
                return method.MakeGenericMethod(type).Invoke(null, new object[] { args, index });
            }
            catch (TargetInvocationException ex)
            {
                throw ex.InnerException ?? ex;
            }
        }

        /// <summary>
        /// Reads argument <paramref name="index"/> as <typeparamref name="T"/>.
        /// Missing or null arguments become <see langword="default"/>.
        /// </summary>
        /// <typeparam name="T">The target type.</typeparam>
        /// <param name="args">Binding arguments.</param>
        /// <param name="index">Zero-based argument index.</param>
        /// <returns>The deserialized value.</returns>
        internal static T Arg<T>(JsonElement[] args, int index)
        {
            if (args == null || index < 0 || index >= args.Length)
            {
                return default;
            }

            JsonElement element = args[index];
            if (element.ValueKind == JsonValueKind.Undefined || element.ValueKind == JsonValueKind.Null)
            {
                return default;
            }

            if (IsTaggedValue(element))
            {
                return JsonValueHelper.Parse<T>(element);
            }

            // System.Text.Json deserializes numbers/strings into JsonElement when T is
            // object — convert primitives to CLR values so exposeFunction callbacks
            // receive boxed ints/longs/strings like official Playwright.
            if (typeof(T) == typeof(object))
            {
                return (T)JsonElementToObject(element);
            }

            return JsonSerializer.Deserialize<T>(element.GetRawText());
        }

        /// <summary>
        /// Awaits a callback result when it is a <see cref="Task"/>, matching official
        /// Playwright's "if the callback returns a Promise, it is awaited".
        /// </summary>
        /// <param name="result">The raw callback return value.</param>
        /// <returns>The awaited value, or <paramref name="result"/> when it is not a task.</returns>
        internal static async Task<object> InvokeAsync(object result)
        {
            if (result is not Task task)
            {
                return result;
            }

            await task.ConfigureAwait(false);
            Type type = task.GetType();
            if (!type.IsGenericType)
            {
                return null;
            }

            object value = type.GetProperty("Result", BindingFlags.Instance | BindingFlags.Public)?.GetValue(task);
            return await InvokeAsync(value).ConfigureAwait(false);
        }

        private static object JsonElementToObject(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.String:
                    return element.GetString();
                case JsonValueKind.True:
                    return true;
                case JsonValueKind.False:
                    return false;
                case JsonValueKind.Number:
                    if (element.TryGetInt64(out long longValue))
                    {
                        if (longValue >= int.MinValue && longValue <= int.MaxValue)
                        {
                            return (int)longValue;
                        }

                        return longValue;
                    }

                    if (element.TryGetDouble(out double doubleValue))
                    {
                        return doubleValue;
                    }

                    return element.GetRawText();
                case JsonValueKind.Null:
                case JsonValueKind.Undefined:
                    return null;
                default:
                    return element.Clone();
            }
        }

        private static bool IsTaggedValue(JsonElement element)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            return element.TryGetProperty("n", out _)
                || element.TryGetProperty("s", out _)
                || element.TryGetProperty("b", out _)
                || element.TryGetProperty("v", out _)
                || element.TryGetProperty("o", out _)
                || element.TryGetProperty("a", out _)
                || element.TryGetProperty("ref", out _)
                || element.TryGetProperty("d", out _)
                || element.TryGetProperty("u", out _)
                || element.TryGetProperty("bi", out _)
                || element.TryGetProperty("r", out _);
        }
    }
}
