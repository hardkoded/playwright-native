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
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using PlaywrightNative.Helpers;

namespace PlaywrightNative.Chromium
{
    /// <summary>
    /// A registered Chromium route: glob, regex, or predicate plus handler identity
    /// so <c>UnrouteAsync</c> can remove the same registration.
    /// </summary>
    internal sealed class CRRouteEntry
    {
        private readonly object _remainingLock = new();
        private int? _remaining;

        /// <summary>
        /// Initializes a new instance of the <see cref="CRRouteEntry"/> class.
        /// </summary>
        /// <param name="urlString">Glob pattern, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regular expression, or <see langword="null"/>.</param>
        /// <param name="urlFunc">URL predicate, or <see langword="null"/>.</param>
        /// <param name="handler">The interception handler.</param>
        /// <param name="handlerIdentity">The original <see cref="Action{T}"/> passed to <c>RouteAsync</c>.</param>
        /// <param name="isContextRoute">
        /// <see langword="true"/> when the route was registered on the browser context.
        /// </param>
        /// <param name="times">
        /// How many times the handler is used. When omitted, the handler is used
        /// for every matching request.
        /// </param>
        internal CRRouteEntry(
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            Func<CRRoute, Task> handler,
            object handlerIdentity,
            bool isContextRoute,
            int? times = null)
        {
            UrlString = urlString;
            UrlRegex = urlRegex;
            UrlFunc = urlFunc;
            Handler = handler ?? throw new ArgumentNullException(nameof(handler));
            HandlerIdentity = handlerIdentity;
            IsContextRoute = isContextRoute;
            _remaining = times;
        }

        /// <summary>Gets the glob pattern, if any.</summary>
        internal string UrlString { get; }

        /// <summary>Gets the regular expression, if any.</summary>
        internal Regex UrlRegex { get; }

        /// <summary>Gets the predicate, if any.</summary>
        internal Func<string, bool> UrlFunc { get; }

        /// <summary>Gets the interception handler.</summary>
        internal Func<CRRoute, Task> Handler { get; }

        /// <summary>Gets the original handler instance used for unroute matching.</summary>
        internal object HandlerIdentity { get; }

        /// <summary>Gets a value indicating whether this route was registered on the context.</summary>
        internal bool IsContextRoute { get; }

        /// <summary>Gets the in-flight handler tracker for <see cref="UnrouteBehavior"/>.</summary>
        internal RouteHandlerLifetime Lifetime { get; } = new();

        /// <summary>
        /// Called when <see cref="ConsumeAndShouldRemove"/> expires a timed route.
        /// Context routes use this to drop the entry from every page.
        /// </summary>
        internal Action OnExpired { get; set; }

        /// <summary>
        /// Decrements the remaining invocation count. Returns <see langword="true"/>
        /// when the route should be removed after this invocation.
        /// </summary>
        /// <returns><see langword="true"/> when no invocations remain.</returns>
        internal bool ConsumeAndShouldRemove()
        {
            bool expired;
            lock (_remainingLock)
            {
                if (!_remaining.HasValue)
                {
                    return false;
                }

                _remaining = _remaining.Value - 1;
                expired = _remaining.Value <= 0;
            }

            if (expired)
            {
                OnExpired?.Invoke();
            }

            return expired;
        }

        /// <summary>
        /// Returns whether <paramref name="url"/> matches this route's matcher.
        /// </summary>
        /// <param name="url">The request URL.</param>
        /// <param name="baseUrl">Optional context <c>baseURL</c> for relative globs.</param>
        /// <returns><see langword="true"/> when the URL matches.</returns>
        internal bool MatchesUrl(string url, string baseUrl = null)
            => UrlMatcher.Matches(url, UrlString, UrlRegex, UrlFunc, baseUrl);

        /// <summary>
        /// Returns whether this entry should be removed by an unroute call.
        /// </summary>
        /// <param name="urlString">Glob used at registration, or <see langword="null"/>.</param>
        /// <param name="urlRegex">Regex used at registration, or <see langword="null"/>.</param>
        /// <param name="urlFunc">Predicate used at registration, or <see langword="null"/>.</param>
        /// <param name="handlerIdentity">
        /// Handler to remove, or <see langword="null"/> to remove every matching matcher.
        /// </param>
        /// <returns><see langword="true"/> when this entry matches the unroute arguments.</returns>
        internal bool MatchesRegistration(string urlString, Regex urlRegex, Func<string, bool> urlFunc, object handlerIdentity)
        {
            if (!SameMatcher(urlString, urlRegex, urlFunc))
            {
                return false;
            }

            return handlerIdentity == null || UrlMatcher.SameHandler(HandlerIdentity, handlerIdentity);
        }

        private bool SameMatcher(string urlString, Regex urlRegex, Func<string, bool> urlFunc)
        {
            if (urlFunc != null)
            {
                return ReferenceEquals(UrlFunc, urlFunc);
            }

            if (urlRegex != null)
            {
                return UrlMatcher.SameRegex(UrlRegex, urlRegex);
            }

            return string.Equals(UrlString, urlString, StringComparison.Ordinal);
        }
    }
}
