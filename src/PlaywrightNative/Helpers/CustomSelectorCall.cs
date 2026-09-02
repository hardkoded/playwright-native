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
