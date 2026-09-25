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
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// Aligns <c>Network.*ExtraInfo</c> events with request/response hops that
    /// share one CDP <c>requestId</c> across redirects. Mirrors official
    /// Playwright <c>ResponseExtraInfoTracker</c>: extras and responses are
    /// paired by index, and response extras are applied only after the hop
    /// has a <see cref="CRResponse"/>.
    /// </summary>
    internal sealed class CRExtraInfoTracker
    {
        private readonly ConcurrentDictionary<string, HopList> _hops = new(StringComparer.Ordinal);

        internal void RequestCreated(string requestId, CRRequest request)
        {
            HopList list = _hops.GetOrAdd(requestId, _ => new HopList());
            lock (list.Gate)
            {
                Hop hop = new Hop { Request = request };
                list.Hops.Add(hop);
                if (list.PendingRequestExtra.Count > 0)
                {
                    hop.StoreRequestExtra(list.PendingRequestExtra.Dequeue());
                }

                hop.FlushRequest();
            }
        }

        internal void ResponseCreated(string requestId, CRResponse response)
        {
            HopList list = _hops.GetOrAdd(requestId, _ => new HopList());
            lock (list.Gate)
            {
                Hop hop = FindHop(list, response.Request) ?? LastWithoutResponse(list);
                if (hop == null)
                {
                    hop = new Hop { Request = response.Request };
                    list.Hops.Add(hop);
                    if (list.PendingRequestExtra.Count > 0)
                    {
                        hop.StoreRequestExtra(list.PendingRequestExtra.Dequeue());
                    }
                }

                hop.Response = response;
                if (!hop.HasResponseExtra && list.PendingResponseExtra.Count > 0)
                {
                    hop.StoreResponseExtra(list.PendingResponseExtra.Dequeue());
                }

                hop.FlushRequest();
                hop.FlushResponse();
                TryStopTracking(requestId, list);
            }
        }

        internal void RequestExtraInfo(string requestId, JsonElement extra)
        {
            HopList list = _hops.GetOrAdd(requestId, _ => new HopList());
            lock (list.Gate)
            {
                Hop hop = FirstWithoutRequestExtra(list);
                if (hop != null)
                {
                    hop.StoreRequestExtra(extra);
                    hop.FlushRequest();
                }
                else
                {
                    list.PendingRequestExtra.Enqueue(extra);
                }

                TryStopTracking(requestId, list);
            }
        }

        internal void ResponseExtraInfo(string requestId, JsonElement extra)
        {
            HopList list = _hops.GetOrAdd(requestId, _ => new HopList());
            lock (list.Gate)
            {
                Hop hop = FirstWithoutResponseExtra(list);
                if (hop != null)
                {
                    hop.StoreResponseExtra(extra);
                    hop.FlushResponse();
                }
                else
                {
                    list.PendingResponseExtra.Enqueue(extra);
                }

                TryStopTracking(requestId, list);
            }
        }

        internal void Finished(string requestId)
        {
            // Extra-info and loadingFinished travel on different CDP channels
            // and can arrive in either order. Official _checkFinished keeps the
            // entry until every hasExtraInfo response is paired; dropping the
            // hop here left WaitForRawHeadersAsync hanging (HAR zip 30s).
            if (!_hops.TryGetValue(requestId, out HopList list))
            {
                return;
            }

            lock (list.Gate)
            {
                list.LoadingDone = true;
                AssignPendingExtras(list);

                for (int i = 0; i < list.Hops.Count; i++)
                {
                    Hop hop = list.Hops[i];
                    hop.FlushRequest();
                    hop.FlushResponse();
                    SealRequestHeaders(hop);
                    SealResponseHeaders(requestId, list, hop);
                }

                TryStopTracking(requestId, list);
            }
        }

        private void AssignPendingExtras(HopList list)
        {
            while (list.PendingRequestExtra.Count > 0)
            {
                Hop hop = FirstWithoutRequestExtra(list);
                if (hop == null)
                {
                    break;
                }

                hop.StoreRequestExtra(list.PendingRequestExtra.Dequeue());
            }

            while (list.PendingResponseExtra.Count > 0)
            {
                Hop hop = FirstWithoutResponseExtra(list);
                if (hop == null)
                {
                    break;
                }

                hop.StoreResponseExtra(list.PendingResponseExtra.Dequeue());
            }
        }

        private void TryStopTracking(string requestId, HopList list)
        {
            if (!list.LoadingDone)
            {
                return;
            }

            for (int i = 0; i < list.Hops.Count; i++)
            {
                if (!ResponseHeadersSettled(list.Hops[i]))
                {
                    return;
                }
            }

            for (int i = 0; i < list.Hops.Count; i++)
            {
                Hop hop = list.Hops[i];
                hop.FlushRequest();
                hop.FlushResponse();
                SealRequestHeaders(hop);
                hop.Response?.EnsureRawResponseHeaders();
            }

            _hops.TryRemove(requestId, out _);
        }

        private void SealRequestHeaders(Hop hop)
        {
            if (hop?.Request == null)
            {
                return;
            }

            // requestWillBeSent headers omit Accept* until ExtraInfo. Sealing
            // them in loadingFinished races that event and HeadersArray returns
            // the short list (ShouldReportRawHeaders).
            if (hop.HasRequestExtra || hop.Request.ServedFromCache)
            {
                hop.Request.EnsureRawRequestHeaders();
                return;
            }

            CRRequest pending = hop.Request;
            _ = Task.Run(async () =>
            {
                await Task.Delay(750).ConfigureAwait(false);
                pending.EnsureRawRequestHeaders();
            });
        }

        private void SealResponseHeaders(string requestId, HopList list, Hop hop)
        {
            if (hop?.Response == null)
            {
                return;
            }

            if (ResponseHeadersSettled(hop))
            {
                hop.Response.EnsureRawResponseHeaders();
                return;
            }

            // ExtraInfo and loadingFinished travel on different CDP channels.
            // For iframe/nested worker scripts Chromium may set hasExtraInfo
            // without ever delivering responseReceivedExtraInfo (playwright#39948).
            // Mirror SealRequestHeaders: allow channel skew, then seal provisional.
            // Immediate seal raced ExtraInfo under Windows suite load and locked
            // comma-joined Network.responseReceived headers into HeadersArray
            // (ShouldReportAllHeaders). Prefer a deferred seal; ApplyExtraHeaders
            // can still upgrade after a provisional seal.
            CRResponse pending = hop.Response;
            _ = DeferredSealResponseHeadersAsync(requestId, list, hop, pending);
        }

        private async Task DeferredSealResponseHeadersAsync(
            string requestId,
            HopList list,
            Hop hop,
            CRResponse pending)
        {
            await Task.Delay(750).ConfigureAwait(false);
            if (!_hops.TryGetValue(requestId, out HopList current) || !ReferenceEquals(current, list))
            {
                return;
            }

            lock (list.Gate)
            {
                if (!hop.HasResponseExtra && pending.ExpectsExtraInfo)
                {
                    pending.SetExpectsExtraInfo(false);
                    pending.EnsureRawResponseHeaders();
                }

                TryStopTracking(requestId, list);
            }
        }

        private bool ResponseHeadersSettled(Hop hop)
        {
            if (hop.Response == null)
            {
                // Keep a stored extra until the response object exists so
                // ResponseCreated can apply it. No response and no extra
                // (loadingFailed) is settled.
                return !hop.HasResponseExtra;
            }

            return !hop.Response.ExpectsExtraInfo || hop.HasResponseExtra;
        }

        private Hop FindHop(HopList list, CRRequest request)
        {
            for (int i = 0; i < list.Hops.Count; i++)
            {
                if (ReferenceEquals(list.Hops[i].Request, request))
                {
                    return list.Hops[i];
                }
            }

            return null;
        }

        private Hop LastWithoutResponse(HopList list)
        {
            for (int i = list.Hops.Count - 1; i >= 0; i--)
            {
                if (list.Hops[i].Response == null)
                {
                    return list.Hops[i];
                }
            }

            return null;
        }

        private Hop FirstWithoutRequestExtra(HopList list)
        {
            for (int i = 0; i < list.Hops.Count; i++)
            {
                if (!list.Hops[i].HasRequestExtra)
                {
                    return list.Hops[i];
                }
            }

            return null;
        }

        private Hop FirstWithoutResponseExtra(HopList list)
        {
            for (int i = 0; i < list.Hops.Count; i++)
            {
                if (!list.Hops[i].HasResponseExtra)
                {
                    return list.Hops[i];
                }
            }

            return null;
        }

        private sealed class HopList
        {
            internal object Gate { get; } = new object();

            internal List<Hop> Hops { get; } = new();

            internal Queue<JsonElement> PendingRequestExtra { get; } = new();

            internal Queue<JsonElement> PendingResponseExtra { get; } = new();

            internal bool LoadingDone { get; set; }
        }

        private sealed class Hop
        {
            private JsonElement _requestExtra;
            private JsonElement _responseExtra;

            internal CRRequest Request { get; set; }

            internal CRResponse Response { get; set; }

            internal bool HasRequestExtra { get; private set; }

            internal bool HasResponseExtra { get; private set; }

            internal void StoreRequestExtra(JsonElement extra)
            {
                HasRequestExtra = true;
                _requestExtra = extra;
            }

            internal void StoreResponseExtra(JsonElement extra)
            {
                HasResponseExtra = true;
                _responseExtra = extra;
            }

            internal void FlushRequest()
            {
                if (Request == null || !HasRequestExtra)
                {
                    return;
                }

                IReadOnlyList<NameValueEntry> headers = _requestExtra.TryGetProperty("headers", out JsonElement headersEl)
                    ? RawNetworkHeaders.FromObject(headersEl)
                    : HeaderMap.Array(Request.Headers);
                Request.SetRawRequestHeaders(headers);
            }

            internal void FlushResponse()
            {
                if (Response == null || !HasResponseExtra)
                {
                    return;
                }

                // Official responseExtraInfoTracker._patchHeaders uses
                // headersObjectToArray(responseExtraInfo.headers, '\n') — not
                // headersText. Chrome joins duplicate non-cookie values with
                // '\n' in the headers object; headersText may collapse them to
                // a single comma-joined line (ShouldReportAllHeaders).
                IReadOnlyList<NameValueEntry> headers = _responseExtra.TryGetProperty("headers", out JsonElement headersEl)
                    ? RawNetworkHeaders.FromObject(headersEl)
                    : Array.Empty<NameValueEntry>();
                if (headers.Count == 0
                    && _responseExtra.TryGetProperty("headersText", out JsonElement textElement))
                {
                    headers = ResponseHeaders.ParseHeadersText(textElement.GetString());
                }

                if (headers.Count == 0)
                {
                    headers = HeaderMap.Array(Response.Headers);
                }

                Response.ApplyExtraHeaders(headers);
            }
        }
    }
}
