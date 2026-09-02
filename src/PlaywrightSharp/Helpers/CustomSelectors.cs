/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Text.Json;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Process-wide map of registered custom selector engines.
    /// </summary>
    internal static class CustomSelectors
    {
        /// <summary>
        /// Official <c>text=</c> engine: innermost element whose normalized
        /// text contains the query (case-insensitive).
        /// </summary>
        private const string TextEngineScript = @"(() => {
  const queryText = (root, selector) => {
    const skip = { SCRIPT: 1, STYLE: 1, HEAD: 1, NOSCRIPT: 1 };
    const dq = String.fromCharCode(34);
    const sq = String.fromCharCode(39);
    const normalize = (s) => String(s || '').replace(/\s+/g, ' ').trim();
    const s = String(selector || '');
    let exact = false;
    let body = s;
    if (s.length >= 2) {
      const q = s[0];
      if ((q === dq || q === sq) && s[s.length - 1] === q) {
        exact = true;
        body = s.slice(1, -1);
      }
    }
    const needle = normalize(body).toLowerCase();
    const found = [];
    const matches = (el) => {
      let raw = '';
      if (el.tagName === 'INPUT' && /^(submit|button|reset)$/i.test(el.type || ''))
        raw = el.value || '';
      else
        raw = el.textContent || el.innerText || '';
      const hay = normalize(raw).toLowerCase();
      return exact ? hay === needle : hay.indexOf(needle) !== -1;
    };
    const visit = (el) => {
      if (!el || skip[el.tagName]) return false;
      let childHit = false;
      const children = el.children || [];
      for (let i = 0; i < children.length; i++)
        childHit = visit(children[i]) || childHit;
      if (el.shadowRoot) {
        const shadowKids = el.shadowRoot.children || [];
        for (let i = 0; i < shadowKids.length; i++)
          childHit = visit(shadowKids[i]) || childHit;
      }
      if (el.nodeType === 1 && matches(el) && !childHit) {
        found.push(el);
        return true;
      }
      return childHit || (el.nodeType === 1 && matches(el));
    };
    if (!root) return found;
    if (root.nodeType === 9)
      visit(root.documentElement);
    else
      visit(root);
    return found;
  };
  return {
    query(root, selector) {
      const found = queryText(root, selector);
      return found.length ? found[0] : null;
    },
    queryAll(root, selector) {
      return queryText(root, selector);
    }
  };
})()";

        private static readonly HashSet<string> PredefinedNames = new HashSet<string>(StringComparer.Ordinal)
        {
            "css",
            "xpath",
            "text",
            "id",
            "role",
            "data-testid",
            "nth",
            "visible",
            "light",
        };

        private static readonly ConcurrentDictionary<string, string> Engines = new ConcurrentDictionary<string, string>(StringComparer.Ordinal);

        private static readonly ConcurrentDictionary<string, bool> ContentScript = new ConcurrentDictionary<string, bool>(StringComparer.Ordinal);

        private static readonly string[] ChainSeparators = [">>"];

        /// <summary>
        /// Stores <paramref name="script"/> under <paramref name="name"/>.
        /// </summary>
        /// <param name="name">Engine prefix. Must be unique.</param>
        /// <param name="script">Source that evaluates to <c>{ query, queryAll }</c>.</param>
        /// <param name="contentScript">Official isolated-world flag.</param>
        internal static void Register(string name, string script, bool contentScript = false)
        {
            if (PredefinedNames.Contains(name))
            {
                throw new PlaywrightSharpException("selectors.register: \"" + name + "\" is a predefined selector engine");
            }

            if (!Engines.TryAdd(name, script))
            {
                throw new PlaywrightSharpException("selectors.register: \"" + name + "\" selector engine has been already registered");
            }

            ContentScript[name] = contentScript;
        }

        /// <summary>
        /// Official isolated-world <c>$</c> when every custom engine is a
        /// <c>contentScript</c> engine and at least one is present.
        /// </summary>
        /// <param name="selector">The selector being queried.</param>
        /// <returns><see langword="true"/> when <c>page.$</c> should use the utility world.</returns>
        internal static bool ShouldQueryInIsolatedWorld(string selector)
        {
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            bool sawContentScript = false;
            string[] parts = selector.Split(ChainSeparators, StringSplitOptions.None);
            for (int i = 0; i < parts.Length; i++)
            {
                string part = parts[i].Trim();
                int equals = part.IndexOf('=');
                if (equals <= 0)
                {
                    continue;
                }

                string name = part.Substring(0, equals);
                if (!Engines.ContainsKey(name))
                {
                    continue;
                }

                if (!ContentScript.TryGetValue(name, out bool isolated) || !isolated)
                {
                    return false;
                }

                sawContentScript = true;
            }

            return sawContentScript;
        }

        /// <summary>
        /// Resolves a registered <c>name=body</c> selector into JS that calls
        /// the engine's <c>query</c> / <c>queryAll</c>.
        /// </summary>
        /// <param name="selector">A page, frame, or element selector.</param>
        /// <param name="call">The generated engine call, when registered.</param>
        /// <returns><see langword="true"/> when <paramref name="selector"/> uses a registered engine.</returns>
        internal static bool TryResolve(string selector, out CustomSelectorCall call)
        {
            call = default;
            if (string.IsNullOrEmpty(selector))
            {
                return false;
            }

            int chain = selector.IndexOf(">>", StringComparison.Ordinal);
            string first = (chain < 0 ? selector : selector.Substring(0, chain)).Trim();
            if (first.StartsWith("internal:has=", StringComparison.Ordinal)
                || string.Equals(first, "internal:has", StringComparison.Ordinal))
            {
                throw new PlaywrightSharpException("\"internal:has\" selector cannot be first");
            }

            if (SelectorQuery.NeedsChainEngine(selector))
            {
                call = new CustomSelectorCall(BuildChainEngineScript(), selector);
                return true;
            }

            int equals = selector.IndexOf('=');
            if (equals <= 0)
            {
                return false;
            }

            string name = selector.Substring(0, equals);
            if (string.Equals(name, "text", StringComparison.Ordinal))
            {
                call = new CustomSelectorCall(TextEngineScript, selector.Substring(equals + 1));
                return true;
            }

            if (!Engines.TryGetValue(name, out string script))
            {
                return false;
            }

            call = new CustomSelectorCall(script, selector.Substring(equals + 1));
            return true;
        }

        private static string BuildChainEngineScript()
        {
            StringBuilder map = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in Engines)
            {
                if (map.Length > 0)
                {
                    map.Append(',');
                }

                map.Append(JsonSerializer.Serialize(pair.Key));
                map.Append(':');
                map.Append("(() => { const raw = (" + pair.Value + "); return typeof raw === 'function' ? raw() : raw; })()");
            }

            return SelectorQuery.ChainEngineScript.Replace(
                "const customEngines = {};",
                "const customEngines = {" + map + "};",
                StringComparison.Ordinal);
        }
    }
}
