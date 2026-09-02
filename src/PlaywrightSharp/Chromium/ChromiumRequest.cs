/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.Chromium
{
    /// <summary>Public <see cref="IRequest"/> wrapping <see cref="CRRequest"/>.</summary>
    internal sealed partial class ChromiumRequest : IRequest, IHasOwningPage
    {
        private readonly CRRequest _crRequest;
        private readonly Func<CRResponse, ChromiumResponse> _resolveResponse;
        private readonly Func<CRRequest, ChromiumRequest> _resolveRequest;
        private readonly Func<Frame, IFrame> _resolveFrame;

        internal ChromiumRequest(
            CRRequest crRequest,
            Func<CRResponse, ChromiumResponse> resolveResponse,
            Func<CRRequest, ChromiumRequest> resolveRequest = null,
            Func<Frame, IFrame> resolveFrame = null)
        {
            _crRequest = crRequest ?? throw new ArgumentNullException(nameof(crRequest));
            _resolveResponse = resolveResponse ?? throw new ArgumentNullException(nameof(resolveResponse));
            _resolveRequest = resolveRequest;
            _resolveFrame = resolveFrame;
        }

        /// <inheritdoc/>
        public string Failure => _crRequest.FailureText;

        /// <inheritdoc/>
        public IFrame Frame
        {
            get
            {
                if (_crRequest.ServiceWorker != null)
                {
                    throw new PlaywrightSharpException(
                        "Service Worker requests do not have an associated frame.");
                }

                if (_crRequest.FrameUnavailable)
                {
                    throw new PlaywrightSharpException(
                        "Frame for this navigation request is not available, because the request\nwas issued before the frame is created. You can check whether the request\nis a navigation request by calling isNavigationRequest() method.");
                }

                IFrame frame = ResolveFrame();
                if (frame != null && frame.Page == null)
                {
                    throw new PlaywrightSharpException(
                        "Frame for this navigation request is not available, because the request\nwas issued before the frame is created. You can check whether the request\nis a navigation request by calling isNavigationRequest() method.");
                }

                return frame;
            }
        }

        /// <inheritdoc/>
        public IPage OwningPage => ResolveFrame()?.Page;

        /// <inheritdoc/>
        public Dictionary<string, string> Headers
        {
            get
            {
                Dictionary<string, string> result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                string refererOverride = _crRequest.ChromiumRefererOverride;
                foreach (KeyValuePair<string, string> header in _crRequest.Headers)
                {
                    string value = !string.IsNullOrEmpty(refererOverride)
                        && string.Equals(header.Key, "referer", StringComparison.OrdinalIgnoreCase)
                        ? refererOverride
                        : header.Value;
                    result[header.Key] = value;
                }

                return result;
            }
        }

        /// <inheritdoc/>
        public bool IsNavigationRequest => _crRequest.IsNavigationRequest;

        /// <inheritdoc/>
        public string Method => _crRequest.Method;

        /// <inheritdoc/>
        public string PostData => _crRequest.PostData;

        /// <inheritdoc/>
        public byte[] PostDataBuffer => _crRequest.PostDataBuffer;

        /// <inheritdoc/>
        public IRequest RedirectedFrom => WrapRedirect(_crRequest.RedirectedFrom);

        /// <inheritdoc/>
        public IRequest RedirectedTo => WrapRedirect(_crRequest.RedirectedTo);

        /// <inheritdoc/>
        public string ResourceType => NetworkRequestEvents.NormalizeResourceType(_crRequest.ResourceType);

        /// <inheritdoc/>
        public RequestTimingResult Timing => _crRequest.Timing;

        /// <inheritdoc/>
        public string Url => _crRequest.Url;

        /// <inheritdoc/>
        public IWorker ServiceWorker => _crRequest.ServiceWorker;

        /// <inheritdoc/>
        public IResponse ExistingResponse
        {
            get
            {
                CRResponse crResponse = _crRequest.Response;
                return crResponse == null ? null : _resolveResponse(crResponse);
            }
        }

        /// <inheritdoc/>
        public async Task<IResponse> GetResponseAsync()
        {
            CRResponse crResponse = await _crRequest.WaitForResponseAsync().ConfigureAwait(false);
            return crResponse == null ? null : _resolveResponse(crResponse);
        }

        /// <inheritdoc/>
        public Task<IResponse> ResponseAsync() => GetResponseAsync();

        /// <inheritdoc/>
        public async Task<RequestSizesResult> GetSizesAsync()
        {
            await _crRequest.WaitUntilFinishedAsync().ConfigureAwait(false);
            CRResponse response = _crRequest.Response;
            if (response == null)
            {
                throw new PlaywrightSharpException("Unable to fetch sizes for failed request");
            }

            return RequestSizesCalculator.Compute(
                _crRequest.Method,
                _crRequest.Url,
                _crRequest.Headers,
                _crRequest.PostData,
                response.Status,
                response.StatusText,
                response.Headers,
                _crRequest.EncodedDataLength,
                encodedDataLengthIncludesHeaders: true);
        }

        /// <inheritdoc/>
        public Task<RequestSizesResult> SizesAsync() => GetSizesAsync();

        /// <inheritdoc/>
        public JsonDocument GetPayloadAsJson(JsonDocumentOptions documentOptions = default)
            => RequestPostData.ParseJson(_crRequest.PostData, _crRequest.Headers, documentOptions);

        /// <inheritdoc/>
        public async Task<Dictionary<string, string>> AllHeadersAsync()
        {
            if (_crRequest.ContinuedHeaders != null)
            {
                return HeaderMap.All(_crRequest.Headers);
            }

            IReadOnlyList<NameValueEntry> raw = await _crRequest.WaitForRawHeadersAsync().ConfigureAwait(false);
            await _crRequest.WaitForResponseAsync().ConfigureAwait(false);
            Dictionary<string, string> map = RawNetworkHeaders.AllJoined(raw);
            foreach (KeyValuePair<string, string> header in _crRequest.Headers)
            {
                if (string.IsNullOrEmpty(header.Key) || string.IsNullOrEmpty(header.Value))
                {
                    continue;
                }

#pragma warning disable CA1308
                string key = header.Key.ToLowerInvariant();
#pragma warning restore CA1308
                if (!map.ContainsKey(key))
                {
                    map[key] = header.Value;
                }
            }

            if (!map.ContainsKey("host")
                && Uri.TryCreate(_crRequest.Url, UriKind.Absolute, out Uri uri)
                && !string.IsNullOrEmpty(uri.Authority))
            {
                map["host"] = uri.Authority;
            }

            return map;
        }

        /// <inheritdoc/>
        public async Task<string> HeaderValueAsync(string name)
        {
            if (_crRequest.ContinuedHeaders != null)
            {
                return HeaderMap.Value(_crRequest.Headers, name);
            }

            IReadOnlyList<NameValueEntry> raw = await _crRequest.WaitForRawHeadersAsync().ConfigureAwait(false);
            return RawNetworkHeaders.JoinedValue(raw, name);
        }

        /// <inheritdoc/>
        public async Task<IReadOnlyList<Header>> HeadersArrayAsync()
        {
            IReadOnlyList<NameValueEntry> raw;
            if (_crRequest.ContinuedHeaders != null)
            {
                raw = HeaderMap.Array(_crRequest.ContinuedHeaders);
            }
            else
            {
                raw = await _crRequest.WaitForRawHeadersAsync().ConfigureAwait(false);
            }

            return raw.Select(e => new Header { Name = e.Name, Value = e.Value }).ToList();
        }

        private IRequest WrapRedirect(CRRequest request)
        {
            if (request == null)
            {
                return null;
            }

            if (_resolveRequest != null)
            {
                return _resolveRequest(request);
            }

            return new ChromiumRequest(request, _resolveResponse, _resolveRequest, _resolveFrame);
        }

        private IFrame ResolveFrame() => _resolveFrame?.Invoke(_crRequest.Frame);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        JsonElement? IRequest.PostDataJSON() => default!;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
