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
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>browser.newContext</c> option checks.
    /// </summary>
    internal static class BrowserContextOptionGuard
    {
        /// <summary>
        /// Throws when <c>deviceScaleFactor</c> or <c>isMobile</c> is combined with
        /// a null viewport, matching upstream Playwright.
        /// </summary>
        /// <param name="viewport">The requested viewport, or <see cref="ViewportSizeHelper.NoViewport"/>.</param>
        /// <param name="deviceScaleFactor">Optional device scale factor.</param>
        /// <param name="isMobile">Optional mobile emulation flag.</param>
        internal static void ThrowIfNullViewportConflicts(
            ViewportSize viewport,
            float? deviceScaleFactor,
            bool? isMobile)
        {
            if (!IsNullViewport(viewport))
            {
                return;
            }

            if (deviceScaleFactor.HasValue)
            {
                throw new PlaywrightNativeException(
                    "\"deviceScaleFactor\" option is not supported with null \"viewport\"");
            }

            if (isMobile.HasValue)
            {
                throw new PlaywrightNativeException(
                    "\"isMobile\" option is not supported with null \"viewport\"");
            }
        }

        /// <summary>
        /// Official <c>normalizeProxySettings</c>: SOCKS4/5 cannot carry
        /// username/password.
        /// </summary>
        /// <param name="proxy">The requested proxy, or <see langword="null"/>.</param>
        internal static void ThrowIfInvalidProxy(Proxy proxy)
        {
            if (proxy == null || string.IsNullOrEmpty(proxy.Server))
            {
                return;
            }

            if (string.IsNullOrEmpty(proxy.Username) && string.IsNullOrEmpty(proxy.Password))
            {
                return;
            }

            string server = proxy.Server;
            if (server.StartsWith("socks4:", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("Socks4 proxy protocol does not support authentication");
            }

            if (server.StartsWith("socks5:", System.StringComparison.OrdinalIgnoreCase))
            {
                throw new PlaywrightNativeException("Browser does not support socks5 proxy authentication");
            }
        }

        private static bool IsNullViewport(ViewportSize viewport)
            => viewport != null && viewport.Width < 0 && viewport.Height < 0;
    }
}
