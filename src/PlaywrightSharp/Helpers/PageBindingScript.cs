/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Text.Json;

namespace PlaywrightSharp
{
    /// <summary>
    /// The page-side contract shared by every browser's binding implementation. A single raw
    /// binding (named <see cref="ChannelName"/>) is installed once per page; the
    /// <see cref="InitScript"/> JS bridge multiplexes every exposed function over it using a
    /// per-name promise protocol. Both Chromium (<c>CRPage</c>) and WebKit (<c>WKPage</c>) reuse
    /// this exact contract — only the protocol plumbing that registers the binding and routes
    /// the call/deliver events differs per browser.
    /// </summary>
    internal static class PageBindingScript
    {
        /// <summary>
        /// The single binding channel installed via the browser's <c>addBinding</c> command.
        /// Every exposed function multiplexes over this raw binding; the JS bridge routes calls
        /// by name.
        /// </summary>
        internal const string ChannelName = "__pw_binding__";

        /// <summary>
        /// Official error when <c>JSON.stringify</c> of binding arguments is corrupted by a
        /// broken <c>Array.prototype.toJSON</c>.
        /// </summary>
        internal const string SerializedArgsError =
            "serializedArgs is not an array. This can happen when Array.prototype.toJSON is defined incorrectly";

        /// <summary>
        /// JS bridge installed once per document. It wraps the raw binding
        /// (<c>globalThis['__pw_binding__']</c>) with a per-name promise protocol: each
        /// <c>window[name]</c> call serializes <c>{ name, seq, serializedArgs }</c>; the .NET
        /// side delivers <c>{ seq, result|error }</c> back through <c>__pw_binding_deliver__</c>.
        /// Argument serialization aliases Window/Document/Node and preserves cycles, matching
        /// official <c>serializeAsCallArgument</c>. Uses index assignment instead of
        /// <c>Array.prototype.push/map</c> so busted prototypes still work.
        /// </summary>
        internal const string InitScript = @"(() => {
    if (globalThis.__pw_binding_installed__) return;

    const raw = globalThis['__pw_binding__'];
    if (!raw) return;
    globalThis.__pw_binding_installed__ = true;

    const pending = new Map();
    let nextSeq = 1;

    const serialize = (value) => {
        const seen = new Map();
        let nextId = 1;
        const visit = (v) => {
            if (Object.is(v, undefined)) return { v: 'undefined' };
            if (Object.is(v, null)) return { v: 'null' };
            if (typeof globalThis.Window === 'function' && v instanceof globalThis.Window) return { s: 'ref: <Window>' };
            if (typeof globalThis.Document === 'function' && v instanceof globalThis.Document) return { s: 'ref: <Document>' };
            if (typeof globalThis.Node === 'function' && v instanceof globalThis.Node) return { s: 'ref: <Node>' };
            const type = typeof v;
            if (type === 'boolean') return { b: v };
            if (type === 'number') {
                if (Object.is(v, -0)) return { v: '-0' };
                if (Object.is(v, NaN)) return { v: 'NaN' };
                if (v === Infinity) return { v: 'Infinity' };
                if (v === -Infinity) return { v: '-Infinity' };
                return { n: v };
            }
            if (type === 'string') return { s: v };
            if (type === 'bigint') return { bi: String(v) };
            if (type === 'symbol' || type === 'function') return { v: 'undefined' };
            if (v instanceof Date) return { d: v.toJSON() };
            if (typeof URL === 'function' && v instanceof URL) return { u: String(v) };
            if (v instanceof RegExp) return { r: { p: v.source, f: v.flags } };
            if (seen.has(v)) return { ref: seen.get(v) };
            const id = nextId++;
            seen.set(v, id);
            if (Array.isArray(v)) {
                const a = [];
                for (let i = 0; i < v.length; i++) a[i] = visit(v[i]);
                return { a: a, id: id };
            }
            const o = [];
            const keys = Object.keys(v);
            for (let i = 0; i < keys.length; i++) {
                const k = keys[i];
                o[o.length] = { k: k, v: visit(v[k]) };
            }
            return { o: o, id: id };
        };
        return visit(value);
    };

    globalThis.__pw_binding_deliver__ = (envelope) => {
        const entry = pending.get(envelope.seq);
        if (!entry) return;
        pending.delete(envelope.seq);
        if (Object.prototype.hasOwnProperty.call(envelope, 'error')) {
            const error = envelope.error;
            if (error == null) {
                entry.reject(null);
                return;
            }
            if (typeof error === 'object') {
                const err = new Error(error.message);
                if (error.stack) err.stack = error.stack;
                entry.reject(err);
                return;
            }
            entry.reject(new Error(String(error)));
            return;
        }
        entry.resolve(envelope.result);
    };

    globalThis.__pw_install_binding__ = (name) => {
        globalThis[name] = function() {
            const seq = nextSeq++;
            const promise = new Promise((resolve, reject) => pending.set(seq, { resolve, reject }));
            const serializedArgs = [];
            for (let i = 0; i < arguments.length; i++) {
                serializedArgs[i] = serialize(arguments[i]);
            }
            raw(JSON.stringify({ name: name, seq: seq, serializedArgs: serializedArgs }));
            return promise;
        };
    };

    globalThis.__pw_remove_binding__ = (name) => {
        try { delete globalThis[name]; } catch (e) { globalThis[name] = undefined; }
    };

    globalThis.__pw_eval_fns__ = globalThis.__pw_eval_fns__ || new Map();
    globalThis.__pw_install_eval_fn__ = (name) => {
        const fn = function() {
            const seq = nextSeq++;
            const promise = new Promise((resolve, reject) => pending.set(seq, { resolve, reject }));
            const serializedArgs = [];
            for (let i = 0; i < arguments.length; i++) {
                serializedArgs[i] = serialize(arguments[i]);
            }
            raw(JSON.stringify({ name: name, seq: seq, serializedArgs: serializedArgs }));
            return promise;
        };
        globalThis.__pw_eval_fns__.set(name, fn);
    };
    globalThis.__pw_remove_eval_fn__ = (name) => {
        try { if (globalThis.__pw_eval_fns__) globalThis.__pw_eval_fns__.delete(name); } catch (e) {}
    };

    globalThis.__pw_install_binding_handle__ = (name) => {
        globalThis[name] = function(arg) {
            if (arguments.length > 1) {
                return Promise.reject(new Error('exposeBindingHandle supports a single argument, ' + arguments.length + ' received'));
            }
            const seq = nextSeq++;
            const promise = new Promise((resolve, reject) => pending.set(seq, { resolve, reject }));
            if (!globalThis.__pw_binding_handles__) globalThis.__pw_binding_handles__ = new Map();
            globalThis.__pw_binding_handles__.set(seq, arg);
            raw(JSON.stringify({ name: name, seq: seq, handle: true }));
            return promise;
        };
    };
})();";

        /// <summary>
        /// Parks a returned handle at <c>globalThis.__pw_result_handles__[index]</c>
        /// so WebKit can deliver mixed handle results without packing objectId + JSON
        /// in one <c>Runtime.callFunctionOn</c> (which rejects some argument mixes).
        /// </summary>
        internal const string ParkHandleFunction =
            "function (handle, index) { var a = globalThis.__pw_result_handles__ || (globalThis.__pw_result_handles__ = []); a[index] = handle; }";

        /// <summary>
        /// Reconstructs a binding result that embeds live JS handles, then delivers it.
        /// <c>seq</c> and a JSON tree are the first two arguments; remaining arguments are
        /// the handles referenced by <c>{ __pw_h: index }</c> placeholders.
        /// </summary>
        internal const string DeliverWithHandlesFunction = @"function (seq, tree) {
    if (typeof tree === 'string') {
        try { tree = JSON.parse(tree); } catch (e) {}
    }
    const handles = [];
    for (let i = 2; i < arguments.length; i++) handles[i - 2] = arguments[i];
    const revive = (v) => {
        if (v && typeof v === 'object' && typeof v.__pw_h === 'number') return handles[v.__pw_h];
        if (Array.isArray(v)) {
            const a = [];
            for (let i = 0; i < v.length; i++) a[i] = revive(v[i]);
            return a;
        }
        if (v && typeof v === 'object') {
            const o = {};
            const keys = Object.keys(v);
            for (let i = 0; i < keys.length; i++) o[keys[i]] = revive(v[keys[i]]);
            return o;
        }
        return v;
    };
    globalThis.__pw_binding_deliver__({ seq: seq, result: revive(tree) });
}";

        /// <summary>
        /// Official duplicate-registration message (without the <c>page.exposeFunction:</c> prefix).
        /// </summary>
        /// <param name="name">The conflicting function name.</param>
        /// <returns>The exception message.</returns>
        internal static string AlreadyRegistered(string name)
            => "Function \"" + name + "\" has been already registered";

        /// <summary>
        /// Official <c>page.exposeFunction</c> duplicate-registration message.
        /// </summary>
        /// <param name="name">The conflicting function name.</param>
        /// <returns>The exception message.</returns>
        internal static string AlreadyRegisteredFunction(string name)
            => "page.exposeFunction: " + AlreadyRegistered(name);

        /// <summary>
        /// Official <c>page.exposeFunction</c> when the name is already on the context.
        /// </summary>
        /// <param name="name">The conflicting function name.</param>
        /// <returns>The exception message.</returns>
        internal static string AlreadyRegisteredInBrowserContext(string name)
            => AlreadyRegistered(name) + " in the browser context";

        /// <summary>
        /// Official <c>browserContext.exposeFunction</c> when a page already has the name.
        /// </summary>
        /// <param name="name">The conflicting function name.</param>
        /// <returns>The exception message.</returns>
        internal static string AlreadyRegisteredInOneOfThePages(string name)
            => AlreadyRegistered(name) + " in one of the pages";

        /// <summary>
        /// Init-script / evaluate expression that installs <c>window[name]</c>.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <returns>A JavaScript expression.</returns>
        internal static string InstallExpression(string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            return "(() => { if (globalThis.__pw_install_binding__) globalThis.__pw_install_binding__(" + nameJson + "); })();";
        }

        /// <summary>
        /// Init-script / evaluate expression that installs a handle-mode binding.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <returns>A JavaScript expression.</returns>
        internal static string InstallHandleExpression(string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            return "(() => { if (globalThis.__pw_install_binding_handle__) globalThis.__pw_install_binding_handle__(" + nameJson + "); })();";
        }

        /// <summary>
        /// Expression that deletes <c>window[name]</c> after dispose.
        /// </summary>
        /// <param name="name">The JS global name.</param>
        /// <returns>A JavaScript expression.</returns>
        internal static string RemoveExpression(string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            return "(() => { if (globalThis.__pw_remove_binding__) globalThis.__pw_remove_binding__(" + nameJson + "); })();";
        }

        /// <summary>
        /// Removes an evaluate callback from <c>__pw_eval_fns__</c>.
        /// </summary>
        /// <param name="name">The unguessable callback name.</param>
        /// <returns>A JavaScript expression.</returns>
        internal static string RemoveEvalFnExpression(string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            return "(() => { if (globalThis.__pw_remove_eval_fn__) globalThis.__pw_remove_eval_fn__(" + nameJson + "); })();";
        }

        /// <summary>
        /// Installs an evaluate callback on <c>__pw_eval_fns__</c> (not
        /// <c>globalThis[name]</c>) so <c>__pw_fn_*</c> never appears as an own property.
        /// </summary>
        /// <param name="name">The unguessable callback name.</param>
        /// <returns>A JavaScript expression.</returns>
        internal static string InstallEvalFnExpression(string name)
        {
            string nameJson = JsonSerializer.Serialize(name);
            return "(() => { if (typeof globalThis.__pw_install_eval_fn__ === 'function') { globalThis.__pw_install_eval_fn__(" + nameJson + "); return true; } if (!globalThis.__pw_install_binding__) return false; globalThis.__pw_install_binding__(" + nameJson + "); const fn = globalThis[" + nameJson + "]; try { delete globalThis[" + nameJson + "]; } catch (e) { globalThis[" + nameJson + "] = undefined; } (globalThis.__pw_eval_fns__ || (globalThis.__pw_eval_fns__ = new Map())).set(" + nameJson + ", fn); return typeof fn === 'function'; })()";
        }

        /// <summary>
        /// Reads <c>serializedArgs</c> (or legacy <c>args</c>) from a binding payload.
        /// </summary>
        /// <param name="root">Parsed binding payload.</param>
        /// <param name="clone">When <see langword="true"/>, clone each argument element.</param>
        /// <param name="args">The argument array when valid.</param>
        /// <param name="error">Official error when the field is not an array.</param>
        /// <returns><see langword="true"/> when <paramref name="args"/> was populated.</returns>
        internal static bool TryReadSerializedArgs(JsonElement root, bool clone, out JsonElement[] args, out string error)
        {
            args = null;
            error = null;
            JsonElement argsElement;
            if (root.TryGetProperty("serializedArgs", out argsElement))
            {
                if (argsElement.ValueKind != JsonValueKind.Array)
                {
                    error = SerializedArgsError;
                    return false;
                }
            }
            else if (!root.TryGetProperty("args", out argsElement) || argsElement.ValueKind != JsonValueKind.Array)
            {
                error = SerializedArgsError;
                return false;
            }

            args = new JsonElement[argsElement.GetArrayLength()];
            int i = 0;
            foreach (JsonElement arg in argsElement.EnumerateArray())
            {
                args[i++] = clone ? arg.Clone() : arg;
            }

            return true;
        }

        /// <summary>
        /// Revives parked handles and delivers the binding result via
        /// <c>__pw_binding_deliver__</c>. <paramref name="treeJson"/> is already JSON.
        /// </summary>
        /// <param name="seq">The binding call sequence number.</param>
        /// <param name="treeJson">JSON tree with <c>{ __pw_h: index }</c> placeholders.</param>
        /// <returns>A JavaScript expression.</returns>
        internal static string DeliverParkedHandlesExpression(long seq, string treeJson)
        {
            string seqJson = seq.ToString(System.Globalization.CultureInfo.InvariantCulture);
            return "(() => { const tree = " + treeJson + "; const handles = globalThis.__pw_result_handles__ || []; const revive = (v) => { if (v && typeof v === 'object' && typeof v.__pw_h === 'number') return handles[v.__pw_h]; if (Array.isArray(v)) { const a = []; for (let i = 0; i < v.length; i++) a[i] = revive(v[i]); return a; } if (v && typeof v === 'object') { const o = {}; const keys = Object.keys(v); for (let i = 0; i < keys.length; i++) o[keys[i]] = revive(v[keys[i]]); return o; } return v; }; globalThis.__pw_result_handles__ = []; globalThis.__pw_binding_deliver__({ seq: " + seqJson + ", result: revive(tree) }); })()";
        }

        /// <summary>
        /// Expression that removes the stored page-side argument for <paramref name="seq"/>
        /// from <c>__pw_binding_handles__</c> so the .NET side can wrap it as a handle.
        /// </summary>
        /// <param name="seq">The binding call sequence number.</param>
        /// <returns>A JavaScript expression that evaluates to the stored argument.</returns>
        internal static string TakeHandleExpression(long seq)
            => "(function() { var m = globalThis.__pw_binding_handles__; if (!m) return undefined; var v = m.get(" + seq + "); m.delete(" + seq + "); return v; })()";
    }
}
