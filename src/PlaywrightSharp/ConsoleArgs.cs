/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Text.Json;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp
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
