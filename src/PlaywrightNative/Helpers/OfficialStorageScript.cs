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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>packages/injected/src/storageScript.ts</c> collect/restore,
    /// including <c>utilityScriptSerializers</c> <c>valueEncoded</c> structured clone.
    /// </summary>
    internal static class OfficialStorageScript
    {
        internal const string CollectIndexedDB =
            @"(async () => {
                function isPlainObject(v) {
                    return !!(v && v.constructor === Object);
                }
                function isRegExp(obj) {
                    try { return obj instanceof RegExp || Object.prototype.toString.call(obj) === '[object RegExp]'; }
                    catch (error) { return false; }
                }
                function isDate(obj) {
                    try { return obj instanceof Date || Object.prototype.toString.call(obj) === '[object Date]'; }
                    catch (error) { return false; }
                }
                function isURL(obj) {
                    try { return obj instanceof URL || Object.prototype.toString.call(obj) === '[object URL]'; }
                    catch (error) { return false; }
                }
                function isError(obj) {
                    try { return obj instanceof Error || (obj && Object.getPrototypeOf(obj)?.name === 'Error'); }
                    catch (error) { return false; }
                }
                function isTypedArray(obj, constructor) {
                    try { return obj instanceof constructor || Object.prototype.toString.call(obj) === '[object ' + constructor.name + ']'; }
                    catch (error) { return false; }
                }
                function isArrayBuffer(obj) {
                    try { return obj instanceof ArrayBuffer || Object.prototype.toString.call(obj) === '[object ArrayBuffer]'; }
                    catch (error) { return false; }
                }
                const typedArrayConstructors = {
                    i8: Int8Array, ui8: Uint8Array, ui8c: Uint8ClampedArray,
                    i16: Int16Array, ui16: Uint16Array, i32: Int32Array, ui32: Uint32Array,
                    f32: Float32Array, f64: Float64Array, bi64: BigInt64Array, bui64: BigUint64Array
                };
                function typedArrayToBase64(array) {
                    if ('toBase64' in array)
                        return array.toBase64();
                    const binary = Array.from(new Uint8Array(array.buffer, array.byteOffset, array.byteLength)).map(b => String.fromCharCode(b)).join('');
                    return btoa(binary);
                }
                function serializeAsCallArgument(value) {
                    const visitorInfo = { visited: new Map(), lastId: 0 };
                    function serialize(inner) {
                        if (inner && typeof inner === 'object') {
                            if (typeof globalThis.Window === 'function' && inner instanceof globalThis.Window)
                                return 'ref: <Window>';
                            if (typeof globalThis.Document === 'function' && inner instanceof globalThis.Document)
                                return 'ref: <Document>';
                            if (typeof globalThis.Node === 'function' && inner instanceof globalThis.Node)
                                return 'ref: <Node>';
                        }
                        return innerSerialize(inner);
                    }
                    function innerSerialize(inner) {
                        if (typeof inner === 'symbol')
                            return { v: 'undefined' };
                        if (Object.is(inner, undefined))
                            return { v: 'undefined' };
                        if (Object.is(inner, null))
                            return { v: 'null' };
                        if (Object.is(inner, NaN))
                            return { v: 'NaN' };
                        if (Object.is(inner, Infinity))
                            return { v: 'Infinity' };
                        if (Object.is(inner, -Infinity))
                            return { v: '-Infinity' };
                        if (Object.is(inner, -0))
                            return { v: '-0' };
                        if (typeof inner === 'boolean' || typeof inner === 'number' || typeof inner === 'string')
                            return inner;
                        if (typeof inner === 'bigint')
                            return { bi: inner.toString() };
                        if (isError(inner)) {
                            let stack;
                            if (inner.stack && inner.stack.startsWith(inner.name + ': ' + inner.message))
                                stack = inner.stack;
                            else
                                stack = inner.name + ': ' + inner.message + '\n' + inner.stack;
                            return { e: { n: inner.name, m: inner.message, s: stack } };
                        }
                        if (isDate(inner))
                            return { d: inner.toJSON() };
                        if (isURL(inner))
                            return { u: inner.toJSON() };
                        if (isRegExp(inner))
                            return { r: { p: inner.source, f: inner.flags } };
                        for (const k of Object.keys(typedArrayConstructors)) {
                            if (isTypedArray(inner, typedArrayConstructors[k]))
                                return { ta: { b: typedArrayToBase64(inner), k: k } };
                        }
                        if (isArrayBuffer(inner))
                            return { ab: { b: typedArrayToBase64(new Uint8Array(inner)) } };
                        const existing = visitorInfo.visited.get(inner);
                        if (existing)
                            return { ref: existing };
                        if (Array.isArray(inner)) {
                            const a = [];
                            const id = ++visitorInfo.lastId;
                            visitorInfo.visited.set(inner, id);
                            for (let i = 0; i < inner.length; ++i)
                                a.push(serialize(inner[i]));
                            return { a: a, id: id };
                        }
                        if (typeof inner === 'object') {
                            const o = [];
                            const id = ++visitorInfo.lastId;
                            visitorInfo.visited.set(inner, id);
                            for (const name of Object.keys(inner)) {
                                let item;
                                try { item = inner[name]; }
                                catch (e) { continue; }
                                if (name === 'toJSON' && typeof item === 'function')
                                    o.push({ k: name, v: { o: [], id: 0 } });
                                else
                                    o.push({ k: name, v: serialize(item) });
                            }
                            return { o: o, id: id };
                        }
                    }
                    return serialize(value);
                }
                function trySerialize(value) {
                    let trivial = true;
                    function walk(v) {
                        const isTrivial = (
                            isPlainObject(v)
                            || Array.isArray(v)
                            || typeof v === 'string'
                            || typeof v === 'number'
                            || typeof v === 'boolean'
                            || Object.is(v, null)
                        );
                        if (!isTrivial)
                            trivial = false;
                        if (v && typeof v === 'object' && (isPlainObject(v) || Array.isArray(v))) {
                            if (Array.isArray(v)) {
                                for (const item of v)
                                    walk(item);
                            } else {
                                for (const name of Object.keys(v))
                                    walk(v[name]);
                            }
                        }
                    }
                    walk(value);
                    const encoded = serializeAsCallArgument(value);
                    if (trivial)
                        return { trivial: value };
                    return { encoded: encoded };
                }
                function idbRequestToPromise(request) {
                    return new Promise((resolve, reject) => {
                        request.addEventListener('success', () => resolve(request.result));
                        request.addEventListener('error', () => reject(request.error));
                    });
                }
                async function collectDB(dbInfo) {
                    if (!dbInfo.name)
                        throw new Error('Database name is empty');
                    if (!dbInfo.version)
                        throw new Error('Database version is unset');
                    const db = await idbRequestToPromise(indexedDB.open(dbInfo.name));
                    try {
                        if (db.objectStoreNames.length === 0)
                            return { name: dbInfo.name, version: dbInfo.version, stores: [] };
                        const transaction = db.transaction(db.objectStoreNames, 'readonly');
                        const stores = await Promise.all([...db.objectStoreNames].map(async storeName => {
                            const objectStore = transaction.objectStore(storeName);
                            const keys = await idbRequestToPromise(objectStore.getAllKeys());
                            const records = await Promise.all(keys.map(async key => {
                                const record = {};
                                if (objectStore.keyPath === null) {
                                    const serializedKey = trySerialize(key);
                                    if (serializedKey.trivial !== undefined)
                                        record.key = serializedKey.trivial;
                                    else
                                        record.keyEncoded = serializedKey.encoded;
                                }
                                const value = await idbRequestToPromise(objectStore.get(key));
                                const serializedValue = trySerialize(value);
                                if (serializedValue.trivial !== undefined)
                                    record.value = serializedValue.trivial;
                                else
                                    record.valueEncoded = serializedValue.encoded;
                                return record;
                            }));
                            const indexes = [...objectStore.indexNames].map(indexName => {
                                const index = objectStore.index(indexName);
                                return {
                                    name: index.name,
                                    keyPath: typeof index.keyPath === 'string' ? index.keyPath : undefined,
                                    keyPathArray: Array.isArray(index.keyPath) ? index.keyPath : undefined,
                                    multiEntry: index.multiEntry,
                                    unique: index.unique
                                };
                            });
                            return {
                                name: storeName,
                                records: records,
                                indexes: indexes,
                                autoIncrement: objectStore.autoIncrement,
                                keyPath: typeof objectStore.keyPath === 'string' ? objectStore.keyPath : undefined,
                                keyPathArray: Array.isArray(objectStore.keyPath) ? objectStore.keyPath : undefined
                            };
                        }));
                        return { name: dbInfo.name, version: dbInfo.version, stores: stores };
                    } finally {
                        db.close();
                    }
                }
                try {
                    if (typeof indexedDB === 'undefined' || !indexedDB.databases)
                        throw new Error('indexedDB.databases is not available');
                    const databases = await indexedDB.databases();
                    const result = [];
                    for (const info of databases)
                        result.push(await collectDB(info));
                    return JSON.stringify(result);
                } catch (e) {
                    throw new Error('Unable to serialize IndexedDB: ' + (e && e.message ? e.message : e));
                }
            })()";

        internal static string Restore(string originStateJson)
        {
            return @"(async () => {
                const originState = " + originStateJson + @";
                function parseEvaluationResultValue(value, handles, refs) {
                    handles = handles || [];
                    refs = refs || new Map();
                    if (Object.is(value, undefined))
                        return undefined;
                    if (typeof value === 'object' && value) {
                        if ('ref' in value)
                            return refs.get(value.ref);
                        if ('v' in value) {
                            if (value.v === 'undefined') return undefined;
                            if (value.v === 'null') return null;
                            if (value.v === 'NaN') return NaN;
                            if (value.v === 'Infinity') return Infinity;
                            if (value.v === '-Infinity') return -Infinity;
                            if (value.v === '-0') return -0;
                            return undefined;
                        }
                        if ('d' in value)
                            return new Date(value.d);
                        if ('u' in value)
                            return new URL(value.u);
                        if ('bi' in value)
                            return BigInt(value.bi);
                        if ('e' in value) {
                            const error = new Error(value.e.m);
                            error.name = value.e.n;
                            error.stack = value.e.s;
                            return error;
                        }
                        if ('r' in value)
                            return new RegExp(value.r.p, value.r.f);
                        if ('a' in value) {
                            const result = [];
                            refs.set(value.id, result);
                            for (const a of value.a)
                                result.push(parseEvaluationResultValue(a, handles, refs));
                            return result;
                        }
                        if ('o' in value) {
                            const result = {};
                            refs.set(value.id, result);
                            for (const entry of value.o) {
                                if (entry.k === '__proto__')
                                    continue;
                                result[entry.k] = parseEvaluationResultValue(entry.v, handles, refs);
                            }
                            return result;
                        }
                        if ('ta' in value) {
                            const ctors = {
                                i8: Int8Array, ui8: Uint8Array, ui8c: Uint8ClampedArray,
                                i16: Int16Array, ui16: Uint16Array, i32: Int32Array, ui32: Uint32Array,
                                f32: Float32Array, f64: Float64Array, bi64: BigInt64Array, bui64: BigUint64Array
                            };
                            const binary = atob(value.ta.b);
                            const bytes = new Uint8Array(binary.length);
                            for (let i = 0; i < binary.length; i++)
                                bytes[i] = binary.charCodeAt(i);
                            return new ctors[value.ta.k](bytes.buffer);
                        }
                        if ('ab' in value) {
                            const binary = atob(value.ab.b);
                            const bytes = new Uint8Array(binary.length);
                            for (let i = 0; i < binary.length; i++)
                                bytes[i] = binary.charCodeAt(i);
                            return bytes.buffer;
                        }
                    }
                    return value;
                }
                function idbRequestToPromise(request) {
                    return new Promise((resolve, reject) => {
                        request.addEventListener('success', () => resolve(request.result));
                        request.addEventListener('error', () => reject(request.error));
                    });
                }
                async function restoreDB(dbInfo) {
                    const openRequest = indexedDB.open(dbInfo.name, dbInfo.version);
                    openRequest.addEventListener('upgradeneeded', () => {
                        const db = openRequest.result;
                        for (const store of dbInfo.stores || []) {
                            const objectStore = db.createObjectStore(store.name, { autoIncrement: store.autoIncrement, keyPath: store.keyPathArray ?? store.keyPath });
                            for (const index of store.indexes || [])
                                objectStore.createIndex(index.name, index.keyPathArray ?? index.keyPath, { unique: index.unique, multiEntry: index.multiEntry });
                        }
                    });
                    const db = await idbRequestToPromise(openRequest);
                    try {
                        if (db.objectStoreNames.length === 0)
                            return;
                        const transaction = db.transaction(db.objectStoreNames, 'readwrite');
                        await Promise.all((dbInfo.stores || []).map(async store => {
                            const objectStore = transaction.objectStore(store.name);
                            await Promise.all((store.records || []).map(async record => {
                                await idbRequestToPromise(objectStore.add(
                                    record.value !== undefined ? record.value : parseEvaluationResultValue(record.valueEncoded),
                                    record.key !== undefined ? record.key : parseEvaluationResultValue(record.keyEncoded)
                                ));
                            }));
                        }));
                    } finally {
                        db.close();
                    }
                }
                const registrations = navigator.serviceWorker ? await navigator.serviceWorker.getRegistrations() : [];
                await Promise.all(registrations.map(async r => {
                    if (!r.installing && !r.waiting && !r.active)
                        r.unregister().catch(() => {});
                    else
                        await r.unregister().catch(() => {});
                }));
                try {
                    for (const db of await (indexedDB.databases ? indexedDB.databases() : []) || []) {
                        if (db.name)
                            indexedDB.deleteDatabase(db.name);
                    }
                    await Promise.all((originState && originState.indexedDB ? originState.indexedDB : []).map(dbInfo => restoreDB(dbInfo)));
                } catch (e) {
                    throw new Error('Unable to restore IndexedDB: ' + (e && e.message ? e.message : e));
                }
                sessionStorage.clear();
                localStorage.clear();
                for (const item of (originState && originState.localStorage) || [])
                    localStorage.setItem(item.name, item.value);
                return true;
            })()";
        }
    }
}
