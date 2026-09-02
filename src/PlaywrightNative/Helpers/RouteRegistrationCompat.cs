/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy route registration helpers over official routing APIs.
    /// </summary>
    internal static class RouteRegistrationCompat
    {
        internal static Task RegisterPageRouteAsync(IPage page, string url, Action<IRoute> handler, int? times)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxPage firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKPage webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(page.RouteAsync(url, handler, new PageRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterPageRouteAsync(IPage page, string url, Func<IRoute, Task> handler, int? times)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxPage firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKPage webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(page.RouteAsync(url, handler, new PageRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterPageRouteAsync(IPage page, Regex url, Action<IRoute> handler, int? times)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxPage firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKPage webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(page.RouteAsync(url, handler, new PageRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterPageRouteAsync(IPage page, Regex url, Func<IRoute, Task> handler, int? times)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxPage firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKPage webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(page.RouteAsync(url, handler, new PageRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterPageRouteAsync(IPage page, Func<string, bool> url, Action<IRoute> handler, int? times)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxPage firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKPage webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(page.RouteAsync(url, handler, new PageRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterPageRouteAsync(IPage page, Func<string, bool> url, Func<IRoute, Task> handler, int? times)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxPage firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKPage webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(page.RouteAsync(url, handler, new PageRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterContextRouteAsync(IBrowserContext context, string url, Action<IRoute> handler, int? times)
        {
            switch (context)
            {
                case ChromiumBrowserContext chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxBrowserContext firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKBrowserContext webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(context.RouteAsync(url, handler, new BrowserContextRouteOptions { Times = times }));
            }
        }

        internal static Task RegisterContextRouteAsync(IBrowserContext context, string url, Func<IRoute, Task> handler, int? times)
        {
            switch (context)
            {
                case ChromiumBrowserContext chromium:
                    return chromium.RouteAsync(url, handler, times);
                case FirefoxBrowserContext firefox:
                    return firefox.RouteAsync(url, handler, times);
                case WKBrowserContext webkit:
                    return webkit.RouteAsync(url, handler, times);
                default:
                    return AwaitRegistrationAsync(context.RouteAsync(url, handler, new BrowserContextRouteOptions { Times = times }));
            }
        }

        private static async Task AwaitRegistrationAsync(Task<IAsyncDisposable> registration)
        {
            await registration.ConfigureAwait(false);
        }
    }
}
