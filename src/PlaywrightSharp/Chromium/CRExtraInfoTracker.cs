/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text.Json;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.Chromium
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
                    return;
                }

                list.PendingRequestExtra.Enqueue(extra);
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
                    return;
                }

                list.PendingResponseExtra.Enqueue(extra);
            }
        }

        internal void Finished(string requestId)
        {
            if (!_hops.TryRemove(requestId, out HopList list))
            {
                return;
            }

            lock (list.Gate)
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

                for (int i = 0; i < list.Hops.Count; i++)
                {
                    Hop hop = list.Hops[i];
                    hop.FlushRequest();
                    hop.FlushResponse();
                    hop.Request?.EnsureRawRequestHeaders();
                    hop.Response?.EnsureRawResponseHeaders();
                }
            }
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

                IReadOnlyList<NameValueEntry> fromText = _responseExtra.TryGetProperty("headersText", out JsonElement textElement)
                    ? ResponseHeaders.ParseHeadersText(textElement.GetString())
                    : Array.Empty<NameValueEntry>();
                IReadOnlyList<NameValueEntry> headers = fromText.Count > 0
                    ? fromText
                    : _responseExtra.TryGetProperty("headers", out JsonElement headersEl)
                        ? RawNetworkHeaders.FromObject(headersEl)
                        : HeaderMap.Array(Response.Headers);
                Response.ApplyExtraHeaders(headers);
            }
        }
    }
}
