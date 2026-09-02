/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// In-memory <see cref="IAPIResponse"/>.
    /// </summary>
    internal sealed partial class APIResponse : IAPIResponse
    {
        private readonly byte[] _body;
        private readonly ResponseSecurityDetailsResult _securityDetails;
        private readonly IReadOnlyList<NameValueEntry> _headersArray;
        private bool _disposed;

        /// <summary>
        /// Initializes a new instance of the <see cref="APIResponse"/> class.
        /// </summary>
        /// <param name="status">Status code.</param>
        /// <param name="statusText">Reason phrase.</param>
        /// <param name="url">Final URL.</param>
        /// <param name="headers">Response headers.</param>
        /// <param name="body">Raw body.</param>
        /// <param name="securityDetails">TLS details when the request used HTTPS.</param>
        /// <param name="timing">Resource timing; unknown fields are <c>-1</c>.</param>
        /// <param name="headersArray">Raw header lines in wire order, or <see langword="null"/>.</param>
        internal APIResponse(
            int status,
            string statusText,
            string url,
            IReadOnlyDictionary<string, string> headers,
            byte[] body,
            ResponseSecurityDetailsResult securityDetails = null,
            RequestTimingResult timing = null,
            IReadOnlyList<NameValueEntry> headersArray = null)
        {
            Status = status;
            StatusText = statusText ?? string.Empty;
            Url = url ?? string.Empty;
            Headers = headers is Dictionary<string, string> dict
                ? dict
                : new Dictionary<string, string>(headers ?? new Dictionary<string, string>(), StringComparer.OrdinalIgnoreCase);
            _body = body ?? Array.Empty<byte>();
            _securityDetails = securityDetails;
            Timing = timing ?? ResourceTimingParser.Create();
            _headersArray = headersArray;
        }

        /// <inheritdoc/>
        public int Status { get; }

        /// <inheritdoc/>
        public string StatusText { get; }

        /// <inheritdoc/>
        public bool Ok => Status >= 200 && Status <= 299;

        /// <inheritdoc/>
        public string Url { get; }

        /// <inheritdoc/>
        public Dictionary<string, string> Headers { get; }

        /// <inheritdoc/>
        public RequestTimingResult Timing { get; }

        /// <inheritdoc/>
        public IReadOnlyList<Header> HeadersArray
        {
            get
            {
                if (_headersArray != null)
                {
                    return _headersArray.Select(e => new Header { Name = e.Name, Value = e.Value }).ToList();
                }

                List<Header> entries = new List<Header>();
                foreach (KeyValuePair<string, string> header in Headers)
                {
                    if (string.Equals(header.Key, "set-cookie", StringComparison.OrdinalIgnoreCase)
                        && header.Value != null
                        && header.Value.Contains('\n', StringComparison.Ordinal))
                    {
                        foreach (string part in header.Value.Split('\n'))
                        {
                            entries.Add(new Header { Name = header.Key, Value = part });
                        }
                    }
                    else
                    {
                        entries.Add(new Header { Name = header.Key, Value = header.Value });
                    }
                }

                return entries;
            }
        }

        /// <inheritdoc/>
        public Task<byte[]> BodyAsync()
        {
            EnsureNotDisposed();
            return Task.FromResult(_body);
        }

        /// <inheritdoc/>
        public Task<string> TextAsync()
        {
            EnsureNotDisposed();
            return Task.FromResult(Encoding.UTF8.GetString(_body));
        }

        /// <inheritdoc/>
        public Task<JsonElement?> JsonAsync()
        {
            EnsureNotDisposed();
            if (_body.Length == 0)
            {
                return Task.FromResult<JsonElement?>(null);
            }

            using JsonDocument document = JsonDocument.Parse(_body);
            return Task.FromResult<JsonElement?>(document.RootElement.Clone());
        }

        /// <inheritdoc/>
        public async Task<ResponseServerAddrResult> ServerAddrAsync()
        {
            EnsureNotDisposed();
            if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri uri)
                || string.IsNullOrEmpty(uri.Host))
            {
                return null;
            }

            if (IPAddress.TryParse(uri.Host, out IPAddress parsed))
            {
                return new ResponseServerAddrResult
                {
                    IpAddress = parsed.ToString(),
                    Port = uri.Port,
                };
            }

            try
            {
                IPAddress[] addresses = await Dns.GetHostAddressesAsync(uri.Host).ConfigureAwait(false);
                if (addresses == null || addresses.Length == 0)
                {
                    return null;
                }

                IPAddress chosen = addresses[0];
                foreach (IPAddress address in addresses)
                {
                    if (address.AddressFamily == AddressFamily.InterNetwork
                        && IPAddress.IsLoopback(address))
                    {
                        chosen = address;
                        break;
                    }

                    if (IPAddress.IsLoopback(address))
                    {
                        chosen = address;
                    }
                }

                return new ResponseServerAddrResult
                {
                    IpAddress = chosen.ToString(),
                    Port = uri.Port,
                };
            }
            catch (SocketException)
            {
                return null;
            }
        }

        /// <inheritdoc/>
        public Task<ResponseSecurityDetailsResult> SecurityDetailsAsync()
        {
            EnsureNotDisposed();
            return Task.FromResult(_securityDetails);
        }

        /// <inheritdoc/>
        public override string ToString()
        {
            StringBuilder text = new StringBuilder();
            text.Append("APIResponse: ");
            text.Append(Status.ToString(System.Globalization.CultureInfo.InvariantCulture));
            text.Append(' ');
            text.Append(StatusText);
            foreach (Header header in HeadersArray)
            {
                text.Append("\n  ");
                text.Append(header.Name);
                text.Append(": ");
                text.Append(header.Value);
            }

            return text.ToString();
        }

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return default;
        }

        private void EnsureNotDisposed()
        {
            if (_disposed)
            {
                throw new PlaywrightSharpException("Response has been disposed");
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task<T> IAPIResponse.JsonAsync<T>(JsonSerializerOptions options) => Task.FromResult<T>(default!);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
