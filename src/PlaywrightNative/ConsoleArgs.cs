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
using System.Collections.Generic;
using System.Text.Json;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Builds <see cref="IConsoleMessage.Args"/> from a protocol console-argument array.
    /// </summary>
    internal static class ConsoleArgs
    {
        /// <summary>
        /// Wraps each argument as a handle. Remote objects use <paramref name="wrapRemote"/>;
        /// primitives become <see cref="ImmediateJSHandle"/>.
        /// </summary>
        /// <param name="args">Protocol <c>args</c> or <c>parameters</c> array.</param>
        /// <param name="wrapRemote">Creates a handle for a remote object with an id.</param>
        /// <returns>The argument handles, or empty.</returns>
        internal static IReadOnlyCollection<IJSHandle> Wrap(JsonElement? args, Func<JsonElement, IJSHandle> wrapRemote)
        {
            if (args == null || args.Value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<IJSHandle>();
            }

            List<IJSHandle> handles = new List<IJSHandle>();
            foreach (JsonElement arg in args.Value.EnumerateArray())
            {
                if (RemoteObject.GetObjectId(arg) != null && wrapRemote != null)
                {
                    IJSHandle remote = wrapRemote(arg);
                    if (remote != null)
                    {
                        handles.Add(remote);
                        continue;
                    }
                }

                handles.Add(new ImmediateJSHandle(CloneJsonValue(arg)));
            }

            return handles;
        }

        /// <summary>
        /// Wraps a single string as one argument handle.
        /// </summary>
        /// <param name="text">Console text.</param>
        /// <returns>A one-item collection, or empty when <paramref name="text"/> is empty.</returns>
        internal static IReadOnlyCollection<IJSHandle> FromText(string text)
        {
            if (string.IsNullOrEmpty(text))
            {
                return Array.Empty<IJSHandle>();
            }

            return new IJSHandle[] { new ImmediateJSHandle(JsonSerializer.SerializeToElement(text)) };
        }

        private static JsonElement CloneJsonValue(JsonElement remoteObject)
        {
            if (remoteObject.TryGetProperty("value", out JsonElement value))
            {
                return value.Clone();
            }

            return JsonSerializer.SerializeToElement(RemoteObject.Preview(remoteObject));
        }
    }
}
