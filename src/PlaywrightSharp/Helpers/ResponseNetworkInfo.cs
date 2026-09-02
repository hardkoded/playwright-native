/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.Json;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Parses remote address and TLS details from a network response payload.
    /// </summary>
    internal static class ResponseNetworkInfo
    {
        /// <summary>
        /// Reads <c>remoteIPAddress</c> / <c>remotePort</c> from a response object.
        /// </summary>
        /// <param name="response">The protocol response payload.</param>
        /// <returns>The address, or <see langword="null"/> when absent.</returns>
        internal static ResponseServerAddrResult ParseServerAddr(JsonElement response)
        {
            string ipAddress = GetString(response, "remoteIPAddress");
            if (string.IsNullOrEmpty(ipAddress))
            {
                return null;
            }

            return new ResponseServerAddrResult
            {
                IpAddress = ipAddress,
                Port = GetInt(response, "remotePort"),
            };
        }

        /// <summary>
        /// Reads <c>securityDetails</c> from a response object.
        /// </summary>
        /// <param name="response">The protocol response payload.</param>
        /// <returns>The details, or <see langword="null"/> when the connection is not TLS.</returns>
        internal static ResponseSecurityDetailsResult ParseSecurityDetails(JsonElement response)
        {
            if (!response.TryGetProperty("securityDetails", out JsonElement details)
                || details.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return new ResponseSecurityDetailsResult
            {
                Protocol = GetString(details, "protocol"),
                SubjectName = GetString(details, "subjectName"),
                Issuer = GetString(details, "issuer"),
                ValidFrom = (float)GetDouble(details, "validFrom"),
                ValidTo = (float)GetDouble(details, "validTo"),
            };
        }

        /// <summary>
        /// Reads the CDP / WebKit <c>fromServiceWorker</c> flag.
        /// </summary>
        /// <param name="response">The protocol response payload.</param>
        /// <returns><see langword="true"/> when a service worker served the response.</returns>
        internal static bool ParseFromServiceWorker(JsonElement response)
        {
            if (response.TryGetProperty("fromServiceWorker", out JsonElement flag)
                && flag.ValueKind == JsonValueKind.True)
            {
                return true;
            }

            // Official WebKit Playwright uses response.source === 'service-worker'.
            return response.TryGetProperty("source", out JsonElement source)
                && string.Equals(source.GetString(), "service-worker", StringComparison.OrdinalIgnoreCase);
        }

        /// <summary>
        /// Reads the CDP / WebKit <c>protocol</c> field (e.g. <c>http/1.1</c>, <c>h2</c>).
        /// </summary>
        /// <param name="response">The protocol response payload.</param>
        /// <returns>The HTTP version string, or <see langword="null"/> when absent.</returns>
        internal static string ParseHttpVersion(JsonElement response)
        {
            string protocol = GetString(response, "protocol");
            if (string.IsNullOrEmpty(protocol))
            {
                protocol = GetString(response, "httpVersion");
            }

            return string.IsNullOrEmpty(protocol) ? null : protocol;
        }

        /// <summary>
        /// Parses WebKit <c>metrics.remoteAddress</c> (<c>127.0.0.1:8000</c>,
        /// <c>::1:8907</c>, or <c>ipv6.port</c>).
        /// </summary>
        /// <param name="value">The raw remote address string.</param>
        /// <returns>The address, or <see langword="null"/> when absent.</returns>
        internal static ResponseServerAddrResult ParseRemoteAddress(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            int colon = value.LastIndexOf(':');
            int dot = value.LastIndexOf('.');
            if (dot < 0 && colon >= 0)
            {
                return new ResponseServerAddrResult
                {
                    IpAddress = "[" + value[..colon] + "]",
                    Port = ParsePort(value[(colon + 1)..]),
                };
            }

            if (colon > dot && colon >= 0)
            {
                return new ResponseServerAddrResult
                {
                    IpAddress = value[..colon],
                    Port = ParsePort(value[(colon + 1)..]),
                };
            }

            if (dot >= 0)
            {
                return new ResponseServerAddrResult
                {
                    IpAddress = "[" + value[..dot] + "]",
                    Port = ParsePort(value[(dot + 1)..]),
                };
            }

            return null;
        }

        /// <summary>
        /// Reads <c>metrics.protocol</c> from a WebKit <c>Network.loadingFinished</c> payload.
        /// </summary>
        /// <param name="finished">The loading-finished event payload.</param>
        /// <returns>The HTTP version string, or <see langword="null"/> when absent.</returns>
        internal static string ParseHttpVersionFromFinished(JsonElement finished)
        {
            if (finished.TryGetProperty("metrics", out JsonElement metrics)
                && metrics.ValueKind == JsonValueKind.Object)
            {
                string fromMetrics = GetString(metrics, "protocol");
                if (!string.IsNullOrEmpty(fromMetrics))
                {
                    return fromMetrics;
                }
            }

            return ParseHttpVersion(finished);
        }

        /// <summary>
        /// Reads WebKit <c>response.security.certificate</c> plus
        /// <c>metrics.securityConnection.protocol</c>.
        /// </summary>
        /// <param name="response">The <c>Network.responseReceived</c> payload.</param>
        /// <param name="finished">The <c>Network.loadingFinished</c> payload, if any.</param>
        /// <returns>TLS details, or <see langword="null"/>.</returns>
        internal static ResponseSecurityDetailsResult ParseWebKitSecurity(JsonElement response, JsonElement? finished)
        {
            JsonElement certificate = default;
            bool hasCertificate = response.TryGetProperty("security", out JsonElement security)
                && security.ValueKind == JsonValueKind.Object
                && security.TryGetProperty("certificate", out certificate)
                && certificate.ValueKind == JsonValueKind.Object;

            string protocol = null;
            if (finished.HasValue
                && finished.Value.TryGetProperty("metrics", out JsonElement metrics)
                && metrics.ValueKind == JsonValueKind.Object
                && metrics.TryGetProperty("securityConnection", out JsonElement connection)
                && connection.ValueKind == JsonValueKind.Object)
            {
                protocol = GetString(connection, "protocol");
            }

            if (!hasCertificate && string.IsNullOrEmpty(protocol))
            {
                return null;
            }

            string subject = hasCertificate ? GetString(certificate, "subject") : null;
            if (!string.IsNullOrEmpty(subject) && subject.StartsWith("CN=", StringComparison.Ordinal))
            {
                subject = subject[3..];
            }

            if (!string.IsNullOrEmpty(protocol)
                && protocol.StartsWith("TLSv", StringComparison.OrdinalIgnoreCase))
            {
                protocol = "TLS " + protocol[4..];
            }

            if (string.IsNullOrEmpty(protocol) && hasCertificate)
            {
                protocol = "TLS 1.3";
            }

            return new ResponseSecurityDetailsResult
            {
                Protocol = protocol,
                SubjectName = subject,
                ValidFrom = hasCertificate ? (float)GetDouble(certificate, "validFrom") : 0,
                ValidTo = hasCertificate ? (float)GetDouble(certificate, "validUntil") : 0,
            };
        }

        /// <summary>
        /// Maps CDP / WebKit protocol tokens to Playwright-style HTTP versions.
        /// Missing values default to <c>HTTP/1.1</c>.
        /// </summary>
        /// <param name="protocol">A raw protocol token, or <see langword="null"/>.</param>
        /// <returns>A display HTTP version such as <c>HTTP/1.1</c> or <c>HTTP/2.0</c>.</returns>
        internal static string NormalizeHttpVersion(string protocol)
        {
            if (string.IsNullOrEmpty(protocol)
                || string.Equals(protocol, "http/1.1", StringComparison.OrdinalIgnoreCase))
            {
                return "HTTP/1.1";
            }

            if (string.Equals(protocol, "h2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocol, "http/2", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocol, "http/2.0", StringComparison.OrdinalIgnoreCase))
            {
                return "HTTP/2.0";
            }

            if (string.Equals(protocol, "h3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocol, "http/3", StringComparison.OrdinalIgnoreCase)
                || string.Equals(protocol, "http/3.0", StringComparison.OrdinalIgnoreCase))
            {
                return "HTTP/3.0";
            }

            if (string.Equals(protocol, "http/1.0", StringComparison.OrdinalIgnoreCase))
            {
                return "HTTP/1.0";
            }

            return protocol;
        }

        private static int ParsePort(string text)
            => int.TryParse(text, out int port) ? port : 0;

        private static string GetString(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                ? property.GetString()
                : null;
        }

        private static int GetInt(JsonElement element, string name)
        {
            return element.TryGetProperty(name, out JsonElement property)
                && property.TryGetInt32(out int value)
                ? value
                : 0;
        }

        private static double GetDouble(JsonElement element, string name)
        {
            if (!element.TryGetProperty(name, out JsonElement property))
            {
                return 0;
            }

            if (property.TryGetInt64(out long integer))
            {
                return integer;
            }

            if (property.TryGetDouble(out double value))
            {
                return value;
            }

            return 0;
        }
    }
}
