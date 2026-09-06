// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IAPIResponseAssertions"/> that checks <see cref="IAPIResponse.Ok"/>.
    /// </summary>
    public sealed partial class APIResponseAssertions : IAPIResponseAssertions
    {
        private readonly IAPIResponse _response;
        private readonly bool _negate;

        /// <summary>
        /// Initializes a new instance of the <see cref="APIResponseAssertions"/> class.
        /// </summary>
        /// <param name="response">The response to assert against.</param>
        public APIResponseAssertions(IAPIResponse response)
            : this(response, negate: false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="APIResponseAssertions"/> class.
        /// </summary>
        /// <param name="response">The response to assert against.</param>
        /// <param name="negate">When <see langword="true"/>, invert each assertion.</param>
        public APIResponseAssertions(IAPIResponse response, bool negate)
        {
            _response = response ?? throw new ArgumentNullException(nameof(response));
            _negate = negate;
        }

        /// <inheritdoc/>
        public IAPIResponseAssertions Not => new APIResponseAssertions(_response, !_negate);

        /// <inheritdoc/>
        public async Task ToBeOKAsync(float? timeout = default)
        {
            bool ok = _response.Ok;
            if (_negate ? !ok : ok)
            {
                return;
            }

            StringBuilder log = new StringBuilder();
            log.Append(_negate ? "expect(response).not.toBeOK() failed" : "expect(response).toBeOK() failed");
            log.Append("\n→ GET ");
            log.Append(_response.Url);
            log.Append("\n← ");
            log.Append(_response.Status.ToString(System.Globalization.CultureInfo.InvariantCulture));
            log.Append(' ');
            log.Append(_response.StatusText);
            if (!_negate && IsPrintableTextContentType(_response))
            {
                string text = await _response.TextAsync().ConfigureAwait(false);
                log.Append("\nResponse text:\n");
                log.Append(text);
            }

            throw new PlaywrightNativeException(log.ToString());
        }

        private static bool IsPrintableTextContentType(IAPIResponse response)
        {
            if (!response.Headers.TryGetValue("content-type", out string type) || string.IsNullOrEmpty(type))
            {
                return false;
            }

            return type.StartsWith("text/", StringComparison.OrdinalIgnoreCase)
                || type.Contains("json", StringComparison.OrdinalIgnoreCase)
                || type.Contains("xml", StringComparison.OrdinalIgnoreCase)
                || type.Contains("javascript", StringComparison.OrdinalIgnoreCase);
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IAPIResponseAssertions.ToBeOKAsync() => ToBeOKAsync();
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
