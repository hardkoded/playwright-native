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
#pragma warning disable CA1062
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy helpers over official <see cref="IBrowserContext"/>.
    /// </summary>
    public static class BrowserContextCompatExtensions
    {
        /// <summary>Gets strict-selector mode when supported.</summary>
        public static bool StrictSelectors(this IBrowserContext context)
            => context is IHasStrictSelectors strict
                ? strict.StrictSelectors
                : false;

        /// <summary>Gets the default action timeout when supported.</summary>
        public static float DefaultTimeout(this IBrowserContext context)
            => context is IHasDefaultTimeouts timeouts
                ? timeouts.DefaultTimeout
                : 30_000;

        /// <summary>Sets the default action timeout when supported.</summary>
        public static void SetDefaultTimeoutValue(this IBrowserContext context, float timeout)
        {
            if (context is IHasDefaultTimeouts timeouts)
            {
                timeouts.DefaultTimeout = timeout;
            }
            else
            {
                context.SetDefaultTimeout(timeout);
            }
        }

        /// <summary>Gets the default navigation timeout when supported.</summary>
        public static float DefaultNavigationTimeout(this IBrowserContext context)
            => context is IHasDefaultTimeouts timeouts
                ? timeouts.DefaultNavigationTimeout
                : 30_000;

        /// <summary>Sets the default navigation timeout when supported.</summary>
        public static void SetDefaultNavigationTimeoutValue(this IBrowserContext context, float timeout)
        {
            if (context is IHasDefaultTimeouts timeouts)
            {
                timeouts.DefaultNavigationTimeout = timeout;
            }
            else
            {
                context.SetDefaultNavigationTimeout(timeout);
            }
        }

        /// <summary>Legacy alias for <see cref="IBrowserContext.CookiesAsync()"/>.</summary>
        public static Task<IReadOnlyList<BrowserContextCookiesResult>> GetCookiesAsync(
            this IBrowserContext context,
            IEnumerable<string> urls = default)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            if (context is IHasBrowserContextExtras extras)
            {
                return extras.GetCookiesAsync(urls);
            }

            if (urls == null)
            {
                return context.CookiesAsync();
            }

            return context.CookiesAsync(urls);
        }

        /// <summary>Legacy spelling of <see cref="IBrowserContext.SetExtraHTTPHeadersAsync"/>.</summary>
        public static Task SetExtraHttpHeadersAsync(
            this IBrowserContext context,
            IEnumerable<KeyValuePair<string, string>> headers)
            => context.SetExtraHTTPHeadersAsync(headers);

        /// <summary>Legacy HTTP credentials setter.</summary>
        public static Task SetHttpCredentialsAsync(
            this IBrowserContext context,
            IEnumerable<HttpCredentials> httpCredentials)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            switch (context)
            {
                case ChromiumBrowserContext chromium:
                    return chromium.SetHttpCredentialsAsync(httpCredentials);
                case FirefoxBrowserContext firefox:
                    return firefox.SetHttpCredentialsAsync(httpCredentials);
                case WKBrowserContext webkit:
                    return webkit.SetHttpCredentialsAsync(httpCredentials);
                default:
                    throw new NotSupportedException("This browser context does not support SetHttpCredentialsAsync.");
            }
        }

        /// <summary>Legacy HTTP credentials setter.</summary>
        public static Task SetHttpCredentialsAsync(this IBrowserContext context, HttpCredentials httpCredentials)
            => context.SetHttpCredentialsAsync(httpCredentials == null ? null : new[] { httpCredentials });

        /// <summary>Legacy close with reason string.</summary>
        public static Task CloseAsync(this IBrowserContext context, string reason)
            => context.CloseAsync(new BrowserContextCloseOptions { Reason = reason });

        /// <summary>Legacy clear cookies by name.</summary>
        public static Task ClearCookiesAsync(this IBrowserContext context, string name)
            => context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Name = name });

        /// <summary>Legacy clear cookies by name regex.</summary>
        public static Task ClearCookiesAsync(this IBrowserContext context, Regex nameRegex)
            => CookieClearFilter.ClearAsync(context, null, null, null, nameRegex);

        /// <summary>Legacy clear cookies by domain/path regex.</summary>
        public static Task ClearCookiesAsync(
            this IBrowserContext context,
            string name,
            Regex domainRegex,
            Regex pathRegex = null)
            => CookieClearFilter.ClearAsync(context, name, null, null, null, domainRegex, pathRegex);

        /// <summary>Legacy clear cookies by URL.</summary>
        public static Task ClearCookiesAsync(this IBrowserContext context, Uri url)
            => CookieClearFilter.ClearAsync(context, null, null, null, url: url?.ToString());

        /// <summary>Legacy clear cookies by name and domain.</summary>
        public static Task ClearCookiesAsync(this IBrowserContext context, string name, string domain)
            => context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Name = name, Domain = domain });

        /// <summary>Legacy clear cookies by name, domain, and path.</summary>
        public static Task ClearCookiesAsync(this IBrowserContext context, string name, string domain, string path)
            => context.ClearCookiesAsync(new BrowserContextClearCookiesOptions { Name = name, Domain = domain, Path = path });

        /// <summary>Legacy route-from-HAR with expanded parameters.</summary>
        public static Task RouteFromHARAsync(
            this IBrowserContext context,
            string har,
            string url = default,
            HarNotFound notFound = default,
            bool update = default,
            HarMode updateMode = default,
            RouteFromHarUpdateContentPolicy updateContent = default)
        {
            if (update)
            {
                return context.RouteFromHARAsync(har, new BrowserContextRouteFromHAROptions
                {
                    Url = url,
                    NotFound = notFound,
                    Update = true,
                    UpdateMode = updateMode,
                    UpdateContent = updateContent,
                });
            }

            return HarPlayback.InstallAsync(context, har, url, notFound);
        }

        /// <summary>Legacy route-from-HAR with regex URL filter and update flag.</summary>
        public static Task RouteFromHARAsync(
            this IBrowserContext context,
            string har,
            Regex urlRegex,
            HarNotFound notFound = default,
            bool update = default,
            HarMode updateMode = default,
            RouteFromHarUpdateContentPolicy updateContent = default)
        {
            if (update)
            {
                return context.RouteFromHARAsync(har, new BrowserContextRouteFromHAROptions
                {
                    UrlRegex = urlRegex,
                    NotFound = notFound,
                    Update = true,
                    UpdateMode = updateMode,
                    UpdateContent = updateContent,
                });
            }

            return HarPlayback.InstallAsync(context, har, urlRegex, notFound);
        }

        /// <summary>Legacy storage state including credentials.</summary>
        public static Task<string> StorageStateAsync(this IBrowserContext context, bool credentials)
            => StorageStateAsync(context, path: null, indexedDB: null, credentials: credentials);

        /// <summary>Legacy storage state with path, indexed DB, and credentials flags.</summary>
        public static Task<string> StorageStateAsync(
            this IBrowserContext context,
            string path = default,
            bool? indexedDB = default,
            bool? credentials = default)
        {
            switch (context)
            {
                case ChromiumBrowserContext chromium:
                    return chromium.StorageStateAsync(path, indexedDB, credentials);
                case FirefoxBrowserContext firefox:
                    return firefox.StorageStateAsync(path, indexedDB, credentials);
                case WKBrowserContext webkit:
                    return webkit.StorageStateAsync(path, indexedDB, credentials);
                default:
                    return context.StorageStateAsync(new BrowserContextStorageStateOptions
                    {
                        Path = path,
                        IndexedDB = indexedDB,
                    });
            }
        }

        /// <summary>Legacy expanded-parameter route with times.</summary>
        public static Task RouteAsync(this IBrowserContext context, string url, Action<IRoute> handler, int? times = default)
            => RouteRegistrationCompat.RegisterContextRouteAsync(context, url, handler, times);

        /// <summary>Legacy expanded-parameter route with times.</summary>
        public static Task RouteAsync(this IBrowserContext context, string url, Func<IRoute, Task> handler, int? times = default)
            => RouteRegistrationCompat.RegisterContextRouteAsync(context, url, handler, times);

        /// <summary>Legacy unroute all with behavior.</summary>
        public static Task UnrouteAllAsync(this IBrowserContext context, UnrouteBehavior behavior = default)
        {
            switch (context)
            {
                case ChromiumBrowserContext chromium:
                    return chromium.UnrouteAllAsync(behavior);
                case FirefoxBrowserContext firefox:
                    return firefox.UnrouteAllAsync(behavior);
                case WKBrowserContext webkit:
                    return webkit.UnrouteAllAsync(behavior);
                default:
                    return context.UnrouteAllAsync(new BrowserContextUnrouteAllOptions
                    {
                        Behavior = UnrouteBehaviorBridge.ToOfficial(behavior),
                    });
            }
        }

        /// <summary>Legacy unroute with behavior.</summary>
        public static Task UnrouteAsync(this IBrowserContext context, string url, UnrouteBehavior behavior)
            => context switch
            {
                ChromiumBrowserContext chromium => chromium.UnrouteAsync(url, behavior: behavior),
                FirefoxBrowserContext firefox => firefox.UnrouteAsync(url, behavior: behavior),
                WKBrowserContext webkit => webkit.UnrouteAsync(url, behavior: behavior),
                _ => context.UnrouteAsync(url),
            };
    }
}
