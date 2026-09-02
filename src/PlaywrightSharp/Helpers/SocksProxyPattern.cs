/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Net;
using System.Text.RegularExpressions;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official <c>parsePattern</c> from Playwright <c>socksProxy.ts</c>.
    /// Matches SOCKS proxy host/port tokens, including <c>*</c>,
    /// <c>&lt;loopback&gt;</c>, leading-dot suffixes, and comma lists.
    /// </summary>
    internal static class SocksProxyPattern
    {
        /// <summary>
        /// Official <c>parsePattern(pattern)</c>.
        /// </summary>
        /// <param name="pattern">Comma-separated host[:port] tokens.</param>
        /// <returns>A matcher for <c>(host, port)</c>.</returns>
        internal static Func<string, int, bool> Parse(string pattern)
        {
            if (string.IsNullOrEmpty(pattern))
            {
                return (_, _) => false;
            }

            string[] tokens = pattern.Split(',');
            Func<string, int, bool>[] matchers = new Func<string, int, bool>[tokens.Length];
            for (int i = 0; i < tokens.Length; i++)
            {
                matchers[i] = CompileToken(tokens[i], pattern);
            }

            return (host, port) =>
            {
                foreach (Func<string, int, bool> matcher in matchers)
                {
                    if (matcher(host, port))
                    {
                        return true;
                    }
                }

                return false;
            };
        }

        private static Func<string, int, bool> CompileToken(string token, string pattern)
        {
            Match match = Regex.Match(token, @"^(.*?)(?::(\d+))?$");
            if (!match.Success)
            {
                throw new PlaywrightSharpException("Unsupported token \"" + token + "\" in pattern \"" + pattern + "\"");
            }

            int? tokenPort = null;
            if (match.Groups[2].Success)
            {
                tokenPort = int.Parse(match.Groups[2].Value, System.Globalization.CultureInfo.InvariantCulture);
            }

            string tokenHost = match.Groups[1].Value;
            if (tokenHost == "<loopback>")
            {
                return (host, port) =>
                    PortMatches(tokenPort, port)
                    && (host == "localhost"
                        || host.EndsWith(".localhost", StringComparison.Ordinal)
                        || host == "127.0.0.1"
                        || host == "[::1]");
            }

            if (tokenHost == "*")
            {
                return (_, port) => PortMatches(tokenPort, port);
            }

            if (IsIp(tokenHost))
            {
                return (host, port) => host == tokenHost && PortMatches(tokenPort, port);
            }

            if (tokenHost.Length > 0 && tokenHost[0] == '.')
            {
                tokenHost = "*" + tokenHost;
            }

            Regex tokenRegex = StarMatchToRegex(tokenHost);
            return (host, port) =>
            {
                if (!PortMatches(tokenPort, port))
                {
                    return false;
                }

                if (IsIp(host))
                {
                    return false;
                }

                return tokenRegex.IsMatch(host);
            };
        }

        private static bool PortMatches(int? tokenPort, int port)
            => tokenPort == null || tokenPort.Value == port;

        private static bool IsIp(string host)
            => !string.IsNullOrEmpty(host) && IPAddress.TryParse(host, out _);

        private static Regex StarMatchToRegex(string pattern)
        {
            string[] parts = pattern.Split('*');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = Regex.Escape(parts[i]);
            }

            return new Regex("^" + string.Join(".*", parts) + "$", RegexOptions.CultureInvariant);
        }
    }
}
