/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Formats exposeFunction / exposeBinding results and errors for page-side delivery.
    /// </summary>
    internal static class PageBindingResult
    {
        /// <summary>
        /// Walks <paramref name="value"/> and replaces <see cref="IJSHandle"/> instances
        /// with <c>{ __pw_h: index }</c> placeholders so the page can revive them.
        /// </summary>
        /// <param name="value">The C# callback return value.</param>
        /// <param name="jsonTree">A JSON-serializable tree.</param>
        /// <param name="handles">Collected handles in placeholder order.</param>
        /// <returns><see langword="true"/> when at least one handle was found.</returns>
        internal static bool TryExtractHandles(object value, out object jsonTree, out List<object> handles)
        {
            handles = new List<object>();
            jsonTree = Visit(value, handles, new Dictionary<object, object>(IdentityComparer.Instance));
            return handles.Count > 0;
        }

        /// <summary>
        /// Inlines primitive <see cref="ImmediateJSHandle"/> placeholders so binding
        /// results can deliver numbers/strings without a remote <c>objectId</c>.
        /// Remote handles keep <c>{ __pw_h }</c> indexes remapped to
        /// <paramref name="remoteHandles"/>.
        /// </summary>
        /// <param name="tree">Output of <see cref="TryExtractHandles"/>.</param>
        /// <param name="handles">Collected handles in placeholder order.</param>
        /// <param name="remoteHandles">Remote handles that still need page-side revival.</param>
        /// <returns>The rewritten tree.</returns>
        internal static object InlineImmediateHandles(object tree, List<object> handles, out List<object> remoteHandles)
        {
            remoteHandles = new List<object>();
            if (handles == null || handles.Count == 0)
            {
                return tree;
            }

            return RewriteImmediates(tree, handles, remoteHandles);
        }

        /// <summary>
        /// Serializes a thrown exception for <c>__pw_binding_deliver__</c>.
        /// <c>throw null</c> in C# becomes <see cref="NullReferenceException"/> and is
        /// delivered as JS <c>null</c> so the page can catch <c>null</c>.
        /// </summary>
        /// <param name="error">The exception thrown by the binding callback.</param>
        /// <returns>A JSON-serializable error payload, or <see langword="null"/>.</returns>
        internal static object FormatError(Exception error)
        {
            if (error == null || IsThrownNull(error))
            {
                return null;
            }

            return new
            {
                message = error.Message,
                stack = error.ToString(),
            };
        }

        private static bool IsThrownNull(Exception error)
        {
            if (error is not NullReferenceException nre)
            {
                return false;
            }

            return nre.InnerException == null
                && string.Equals(
                    nre.Message,
                    "Object reference not set to an instance of an object.",
                    StringComparison.Ordinal);
        }

        private static object RewriteImmediates(object node, IReadOnlyList<object> handles, List<object> remoteHandles)
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

                int remoteIndex = remoteHandles.Count;
                remoteHandles.Add(handle);
                return new Dictionary<string, int>(StringComparer.Ordinal) { ["__pw_h"] = remoteIndex };
            }

            if (node is IDictionary<string, object> map)
            {
                Dictionary<string, object> copy = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, object> pair in map)
                {
                    copy[pair.Key] = RewriteImmediates(pair.Value, handles, remoteHandles);
                }

                return copy;
            }

            if (node is IList list && node is not string)
            {
                List<object> copy = new List<object>(list.Count);
                foreach (object item in list)
                {
                    copy.Add(RewriteImmediates(item, handles, remoteHandles));
                }

                return copy;
            }

            return node;
        }

        private static object Visit(object value, List<object> handles, IDictionary<object, object> seen)
        {
            if (value == null)
            {
                return null;
            }

            if (value is IJSHandle)
            {
                int index = handles.Count;
                handles.Add(value);
                return new Dictionary<string, int>(StringComparer.Ordinal) { ["__pw_h"] = index };
            }

            Type type = value.GetType();
            if (type.IsPrimitive
                || value is string
                || value is decimal
                || value is DateTime
                || value is DateTimeOffset
                || value is Guid
                || value is Uri)
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
                    map[Convert.ToString(entry.Key, System.Globalization.CultureInfo.InvariantCulture)] =
                        Visit(entry.Value, handles, seen);
                }

                return map;
            }

            if (value is IEnumerable enumerable && value is not string)
            {
                List<object> list = new List<object>();
                seen[value] = list;
                foreach (object item in enumerable)
                {
                    list.Add(Visit(item, handles, seen));
                }

                return list;
            }

            Dictionary<string, object> obj = new Dictionary<string, object>(StringComparer.Ordinal);
            seen[value] = obj;
            foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
            {
                if (!property.CanRead || property.GetIndexParameters().Length > 0)
                {
                    continue;
                }

                obj[property.Name] = Visit(property.GetValue(value), handles, seen);
            }

            return obj;
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
