/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official Playwright <c>BrowserContext._permissions</c> map: grants
    /// accumulate per resolved origin.
    /// </summary>
    internal sealed class GrantedPermissionSet
    {
        private readonly Dictionary<string, List<string>> _byOrigin = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        /// <summary>
        /// Origin → accumulated Playwright permission names.
        /// </summary>
        internal IEnumerable<KeyValuePair<string, IReadOnlyList<string>>> Entries
        {
            get
            {
                foreach (KeyValuePair<string, List<string>> entry in _byOrigin)
                {
                    yield return new KeyValuePair<string, IReadOnlyList<string>>(entry.Key, entry.Value);
                }
            }
        }

        /// <summary>
        /// Whether any origin has been granted or denied.
        /// </summary>
        internal bool IsEmpty => _byOrigin.Count == 0;

        /// <summary>
        /// Resolves a grant origin the way official Playwright does
        /// (<c>new URL(origin).origin</c>, or <c>*</c>).
        /// </summary>
        /// <param name="origin">URL, origin, or <see langword="null"/>.</param>
        /// <returns>The stored origin key.</returns>
        internal static string ResolveOrigin(string origin)
        {
            if (string.IsNullOrEmpty(origin) || string.Equals(origin, "*", StringComparison.Ordinal))
            {
                return "*";
            }

            if (Uri.TryCreate(origin, UriKind.Absolute, out Uri uri)
                && (string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)))
            {
                return uri.GetLeftPart(UriPartial.Authority);
            }

            return origin;
        }

        /// <summary>
        /// Adds <paramref name="permissions"/> to the origin bucket without
        /// removing earlier grants.
        /// </summary>
        /// <param name="permissions">New Playwright permission names.</param>
        /// <param name="origin">URL, origin, or <see langword="null"/> for all.</param>
        /// <returns>The accumulated list for that origin.</returns>
        internal IReadOnlyList<string> Accumulate(IEnumerable<string> permissions, string origin)
        {
            string resolved = ResolveOrigin(origin);
            if (!_byOrigin.TryGetValue(resolved, out List<string> list))
            {
                list = new List<string>();
                _byOrigin[resolved] = list;
            }

            if (permissions == null)
            {
                return list;
            }

            foreach (string permission in permissions)
            {
                if (string.IsNullOrEmpty(permission))
                {
                    continue;
                }

                if (!list.Exists(existing => string.Equals(existing, permission, StringComparison.Ordinal)))
                {
                    list.Add(permission);
                }
            }

            return list;
        }

        /// <summary>
        /// Seeds context-option permissions for every origin.
        /// </summary>
        /// <param name="permissions">Playwright permission names, or <see langword="null"/>.</param>
        internal void SeedAllOrigins(IEnumerable<string> permissions)
        {
            Clear();
            if (permissions == null)
            {
                return;
            }

            Accumulate(permissions, "*");
        }

        /// <summary>
        /// Drops every stored grant.
        /// </summary>
        internal void Clear() => _byOrigin.Clear();
    }
}
