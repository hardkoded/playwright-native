/*
 * Copyright (c) Microsoft Corporation.
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
using System.Diagnostics;
using System.Globalization;
using System.Net;
using System.Net.Http;
using System.Net.Sockets;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/network-timeout.spec.ts</c> parity. Upstream drives
    /// the internal <c>utils.httpRequest</c>; the .NET twin is
    /// <see cref="HttpRequestHelper"/>, which the browser fetcher uses.
    /// File-level <c>mode !== 'default'</c> does not apply here.
    /// </summary>
    [TestFixture]
    public class NetworkTimeoutTests : PlaywrightTestEx
    {
        [PlaywrightTest("network-timeout.spec.ts", "httpRequest should honor socketTimeout while connecting")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HttpRequestShouldHonorSocketTimeoutWhileConnecting()
        {
            // 203.0.113.1 is TEST-NET-3 (RFC 5737), guaranteed to be unroutable, so the
            // TCP connection stalls instead of failing. Make sure the requested timeout
            // governs from the start.
            const int socketTimeout = 8000;
            Stopwatch stopwatch = Stopwatch.StartNew();
            Exception error = await HttpRequestErrorAsync("http://203.0.113.1/index.html", socketTimeout).ConfigureAwait(false);
            long elapsed = stopwatch.ElapsedMilliseconds;
            if (!error.Message.Contains("timed out after", StringComparison.Ordinal))
            {
                // Some networks actively refuse TEST-NET addresses instead of blackholing them.
                Assert.Ignore($"environment does not stall connections to TEST-NET: {error.Message}");
            }

            Assert.That(error.Message, Does.Contain($"timed out after {socketTimeout}ms"));
            Assert.That(elapsed, Is.GreaterThanOrEqualTo(socketTimeout - 1000));
        }

        [PlaywrightTest("network-timeout.spec.ts", "httpRequest should honor socketTimeout for stalled responses")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task HttpRequestShouldHonorSocketTimeoutForStalledResponses()
        {
            // Accepts connections but never responds.
            TcpListener server = new TcpListener(IPAddress.Loopback, 0);
            server.Start();
            int port = ((IPEndPoint)server.LocalEndpoint).Port;
            const int socketTimeout = 1500;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                Exception error = await HttpRequestErrorAsync(
                    "http://127.0.0.1:" + port.ToString(CultureInfo.InvariantCulture) + "/index.html",
                    socketTimeout).ConfigureAwait(false);
                long elapsed = stopwatch.ElapsedMilliseconds;
                Assert.That(error.Message, Does.Contain($"timed out after {socketTimeout}ms"));
                Assert.That(elapsed, Is.GreaterThanOrEqualTo(socketTimeout - 500));
                Assert.That(elapsed, Is.LessThan(socketTimeout * 3));
            }
            finally
            {
                server.Stop();
            }
        }

        [PlaywrightTest("network-timeout.spec.ts", "httpRequest should honor ipv4first result order")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void HttpRequestShouldHonorIpv4firstResultOrder()
        {
            Assert.Ignore("Node dns.promises.lookup / dns.setDefaultResultOrder hook");
        }

        private static async Task<Exception> HttpRequestErrorAsync(string url, int socketTimeout)
        {
            using HttpClient client = new HttpClient(HttpRequestHelper.CreateHandler(socketTimeout), disposeHandler: true)
            {
                Timeout = System.Threading.Timeout.InfiniteTimeSpan,
            };
            using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            try
            {
                using HttpResponseMessage response = await HttpRequestHelper.SendAsync(client, request, HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                return new InvalidOperationException("unexpected response");
            }
            catch (HttpRequestException ex)
            {
                return ex;
            }
        }
    }
}
