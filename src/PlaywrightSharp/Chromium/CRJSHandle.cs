/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.Chromium
{
    /// <summary>
    /// Handle to a JavaScript value in the browser — backed by a CDP remote object.
    /// Disposed via <c>Runtime.releaseObject</c>. A specialized subclass is used when
    /// the remote object is a DOM node (<c>subtype == "node"</c>).
    /// </summary>
    internal class CRJSHandle : IAsyncDisposable
    {
        private readonly CRExecutionContext _context;
        private readonly string _objectId;
        private readonly JsonElement? _primitiveRemote;
        private string _preview;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRJSHandle"/> class.
        /// </summary>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The CDP remote object ID. May be <see langword="null"/> for a primitive.</param>
        /// <param name="preview">Official handle preview from the remote object.</param>
        public CRJSHandle(CRExecutionContext context, string objectId, string preview = null)
            : this(context, objectId, preview, null)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="CRJSHandle"/> class for a
        /// primitive remote value that has no <c>objectId</c>.
        /// </summary>
        /// <param name="context">The execution context that owns the remote object.</param>
        /// <param name="objectId">The CDP remote object ID. May be <see langword="null"/> for a primitive.</param>
        /// <param name="preview">Official handle preview from the remote object.</param>
        /// <param name="primitiveRemote">The protocol remote object used to unbox primitives.</param>
        internal CRJSHandle(
            CRExecutionContext context,
            string objectId,
            string preview,
            JsonElement? primitiveRemote)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _objectId = objectId;
            _preview = preview ?? (string.IsNullOrEmpty(objectId) ? "undefined" : "JSHandle@object");
            _primitiveRemote = primitiveRemote;
        }

        /// <summary>Gets the CDP remote object identifier for this handle.</summary>
        internal string ObjectId => _objectId;

        /// <summary>Gets a value indicating whether this handle has been disposed.</summary>
        internal bool IsDisposed => _disposed;

        /// <summary>Gets the execution context that owns this handle.</summary>
        internal CRExecutionContext ExecutionContext => _context;

        /// <summary>Gets the official JSHandle preview.</summary>
        internal string Preview => _preview;

        /// <summary>Gets the execution context that owns this handle.</summary>
        protected CRExecutionContext Context => _context;

        /// <inheritdoc/>
        public async ValueTask DisposeAsync()
        {
            GC.SuppressFinalize(this);

            if (_disposed)
            {
                return;
            }

            _disposed = true;
            if (!string.IsNullOrEmpty(_objectId))
            {
                await _context.ReleaseHandleAsync(_objectId).ConfigureAwait(false);
            }
        }

        /// <summary>
        /// Wraps this handle as a public <see cref="IJSHandle"/>. Primitives without
        /// an <c>objectId</c> become <see cref="ImmediateJSHandle"/> so evaluate can unbox them.
        /// </summary>
        /// <param name="page">The owning page, used for element-handle wrapping.</param>
        /// <returns>The public handle.</returns>
        internal IJSHandle ToPublicHandle(CRPage page)
        {
            if (this is CRElementHandle element)
            {
                return new ChromiumElementHandle(element);
            }

            if (string.IsNullOrEmpty(_objectId))
            {
                return ToImmediateHandle();
            }

            return new ChromiumJSHandle(this, page);
        }

        /// <summary>
        /// Converts a primitive remote object into an in-process handle that
        /// <c>EvaluateAsync</c> can pass by value (including Infinity / -0).
        /// </summary>
        /// <returns>An immediate handle for the stored primitive.</returns>
        internal IJSHandle ToImmediateHandle()
        {
            if (_primitiveRemote is JsonElement remote)
            {
                if (JsonValueHelper.TryReadUnserializableToken(remote, out string token))
                {
                    return new ImmediateJSHandle(JsonSerializer.SerializeToElement(token), token);
                }

                return RemoteObject.WrapPrimitive(remote);
            }

            return new ImmediateJSHandle(default, _preview);
        }

        /// <summary>Updates the official preview (used for async node descriptions).</summary>
        /// <param name="preview">The new preview string.</param>
        internal void SetPreview(string preview)
        {
            if (!string.IsNullOrEmpty(preview))
            {
                _preview = preview;
            }
        }

        /// <summary>
        /// Evaluates a JavaScript function with this handle as the first argument
        /// (via CDP <c>Runtime.callFunctionOn</c> with <c>objectId</c>).
        /// </summary>
        /// <typeparam name="T">The result type.</typeparam>
        /// <param name="functionDeclaration">A function declaration; the handle is passed as the first argument.</param>
        /// <param name="args">Additional arguments beyond the handle.</param>
        /// <returns>The deserialized result of the function call.</returns>
        internal Task<T> EvaluateFunctionAsync<T>(string functionDeclaration, params object[] args)
        {
            EnsureNotDisposed();
            return _context.EvaluateFunctionOnHandleAsync<T>(_objectId, functionDeclaration, args);
        }

        /// <summary>
        /// Same as <see cref="EvaluateFunctionAsync{T}(string, object[])"/> but returns
        /// the raw CDP <c>RemoteObject</c>.
        /// </summary>
        /// <param name="functionDeclaration">A function declaration; the handle is passed as the first argument.</param>
        /// <param name="args">Additional arguments beyond the handle.</param>
        /// <returns>The raw CDP <c>RemoteObject</c> as a <see cref="JsonElement"/>, or <c>null</c>.</returns>
        internal Task<JsonElement?> EvaluateFunctionAsync(string functionDeclaration, params object[] args)
        {
            EnsureNotDisposed();
            return _context.EvaluateFunctionOnHandleAsync(_objectId, functionDeclaration, args);
        }

        /// <summary>
        /// Evaluates a function on this handle and returns the raw remote object
        /// (<c>returnByValue: false</c>).
        /// </summary>
        /// <param name="functionDeclaration">A function declaration; the handle is the first argument.</param>
        /// <param name="args">Additional arguments beyond the handle.</param>
        /// <returns>The CDP remote object, or <see langword="null"/>.</returns>
        internal Task<JsonElement?> EvaluateHandleRawAsync(string functionDeclaration, params object[] args)
        {
            EnsureNotDisposed();
            return _context.EvaluateHandleOnHandleAsync(_objectId, functionDeclaration, args);
        }

        /// <summary>Guards against use after <see cref="DisposeAsync"/>.</summary>
        protected void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new PlaywrightSharpException(EvaluateSerialization.DisposedHandleMessage);
            }
        }
    }
}
