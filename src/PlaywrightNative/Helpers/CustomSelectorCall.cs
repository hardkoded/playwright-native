/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// JavaScript snippets that invoke a registered custom selector engine.
    /// </summary>
    internal readonly struct CustomSelectorCall
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="CustomSelectorCall"/> struct.
        /// </summary>
        /// <param name="script">Engine source that evaluates to <c>{ query, queryAll }</c>.</param>
        /// <param name="body">The selector body after the first <c>=</c>.</param>
        internal CustomSelectorCall(string script, string body)
        {
            string bodyJson = JsonSerializer.Serialize(body);
            string engine = "(() => { const raw = (" + script + "); return typeof raw === 'function' ? raw() : raw; })()";
            string asNode = @"(value) => {
    if (value == null || value === undefined)
      return null;
    if (value.nodeType)
      return value;
    const tag = Object.prototype.toString.call(value);
    throw new Error('Expected a Node but got ' + tag);
  }";
            DocumentQueryExpression = "(() => { const engine = " + engine + "; const asNode = " + asNode + "; return asNode(engine.query(document, " + bodyJson + ")); })()";
            DocumentQueryAllExpression = "(() => { const engine = " + engine + "; return Array.from(engine.queryAll(document, " + bodyJson + ") || []); })()";
            ElementQueryFunction = "(el) => { const engine = " + engine + "; const asNode = " + asNode + "; return asNode(engine.query(el, " + bodyJson + ")); }";
            ElementQueryAllFunction = "(el) => { const engine = " + engine + "; return Array.from(engine.queryAll(el, " + bodyJson + ") || []); }";
        }

        /// <summary>IIFE that queries <c>document</c> and returns the first match.</summary>
        internal string DocumentQueryExpression { get; }

        /// <summary>IIFE that queries <c>document</c> and returns every match.</summary>
        internal string DocumentQueryAllExpression { get; }

        /// <summary>Function <c>(el) =&gt; …</c> that queries a root element.</summary>
        internal string ElementQueryFunction { get; }

        /// <summary>Function <c>(el) =&gt; …</c> that queries every match under a root.</summary>
        internal string ElementQueryAllFunction { get; }
    }
}
