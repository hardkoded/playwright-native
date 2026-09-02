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
using System.Collections.Generic;
using System.Linq;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Converts official <c>Microsoft.Playwright</c> option bags into PlaywrightNative types.
    /// </summary>
    internal static class MicrosoftOptionsBridge
    {
        internal static PlaywrightNative.TracingStartOptions ToTracingStartOptions(Microsoft.Playwright.TracingStartOptions options)
        {
            if (options == null)
            {
                return null;
            }

            if (options is PlaywrightNative.TracingStartOptions local)
            {
                return local;
            }

            return new PlaywrightNative.TracingStartOptions
            {
                Live = options.Live,
                Screenshots = options.Screenshots,
                Snapshots = options.Snapshots,
                Sources = options.Sources,
                Name = options.Name,
                Title = options.Title,
            };
        }

        internal static BrowserTypeLaunchOptions ToLaunchOptions(Microsoft.Playwright.BrowserTypeLaunchOptions options)
        {
            if (options == null)
            {
                return null;
            }

            BrowserTypeLaunchOptions result = new BrowserTypeLaunchOptions
            {
                Args = options.Args,
                ArtifactsDir = options.ArtifactsDir,
                ChromiumSandbox = options.ChromiumSandbox ?? false,
                DownloadsPath = options.DownloadsPath,
                ExecutablePath = options.ExecutablePath,
                FirefoxUserPrefs = options.FirefoxUserPrefs,
                HandleSIGHUP = options.HandleSIGHUP,
                HandleSIGINT = options.HandleSIGINT,
                HandleSIGTERM = options.HandleSIGTERM,
                Headless = options.Headless ?? true,
                IgnoreDefaultArgs = options.IgnoreAllDefaultArgs ?? false,
                IgnoreDefaultArgsList = options.IgnoreDefaultArgs,
                Proxy = options.Proxy,
                Timeout = options.Timeout.HasValue ? (int?)options.Timeout.Value : null,
                TracesDir = options.TracesDir,
            };

            if (options.Env != null)
            {
                Dictionary<string, string> env = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in options.Env)
                {
                    env[pair.Key] = pair.Value;
                }

                result.Env = env;
            }

            if (!string.IsNullOrEmpty(options.Channel))
            {
                result.Channel = ParseChannel(options.Channel);
            }

            return result;
        }

        internal static BrowserTypeLaunchPersistentContextOptions ToPersistentLaunchOptions(
            Microsoft.Playwright.BrowserTypeLaunchPersistentContextOptions options)
        {
            if (options == null)
            {
                return null;
            }

            BrowserTypeLaunchPersistentContextOptions result = new BrowserTypeLaunchPersistentContextOptions
            {
                Args = options.Args,
                ArtifactsDir = options.ArtifactsDir,
                ChromiumSandbox = options.ChromiumSandbox ?? false,
                DownloadsPath = options.DownloadsPath,
                ExecutablePath = options.ExecutablePath,
                FirefoxUserPrefs = options.FirefoxUserPrefs,
                HandleSIGHUP = options.HandleSIGHUP,
                HandleSIGINT = options.HandleSIGINT,
                HandleSIGTERM = options.HandleSIGTERM,
                Headless = options.Headless ?? true,
                IgnoreDefaultArgs = options.IgnoreAllDefaultArgs ?? false,
                IgnoreDefaultArgsList = options.IgnoreDefaultArgs,
                Proxy = options.Proxy,
                Timeout = options.Timeout.HasValue ? (int?)options.Timeout.Value : null,
                TracesDir = options.TracesDir,
            };

            if (options.Env != null)
            {
                Dictionary<string, string> env = new Dictionary<string, string>(StringComparer.Ordinal);
                foreach (KeyValuePair<string, string> pair in options.Env)
                {
                    env[pair.Key] = pair.Value;
                }

                result.Env = env;
            }

            if (!string.IsNullOrEmpty(options.Channel))
            {
                result.Channel = ParseChannel(options.Channel);
            }

            result.ViewportSize = options.ViewportSize;
            result.Locale = options.Locale;
            result.TimezoneId = options.TimezoneId;
            result.UserAgent = options.UserAgent;
            result.DeviceScaleFactor = options.DeviceScaleFactor;
            result.IsMobile = options.IsMobile;
            result.HasTouch = options.HasTouch;
            result.ColorScheme = options.ColorScheme ?? default;
            result.ReducedMotion = options.ReducedMotion ?? default;
            result.ForcedColors = options.ForcedColors ?? default;
            result.Contrast = options.Contrast ?? default;
            result.AcceptDownloads = options.AcceptDownloads ?? true;
            result.BypassCSP = options.BypassCSP ?? false;
            result.IgnoreHTTPSErrors = options.IgnoreHTTPSErrors ?? false;
            result.JavaScriptEnabled = options.JavaScriptEnabled ?? true;
            result.Offline = options.Offline ?? false;
            result.StrictSelectors = options.StrictSelectors ?? false;
            result.BaseURL = options.BaseURL;
            result.ExtraHTTPHeaders = options.ExtraHTTPHeaders == null
                ? null
                : new Dictionary<string, string>(options.ExtraHTTPHeaders);
            result.Geolocation = options.Geolocation;
            result.HttpCredentials = options.HttpCredentials;
            result.Permissions = options.Permissions == null ? null : options.Permissions.ToArray();
            result.ClientCertificates = options.ClientCertificates;
            result.RecordHarPath = options.RecordHarPath;
            result.RecordHarMode = options.RecordHarMode ?? default;
            result.RecordHarContent = options.RecordHarContent ?? default;
            result.RecordHarOmitContent = options.RecordHarOmitContent;
            result.RecordHarUrl = options.RecordHarUrlFilterString ?? options.RecordHarUrlFilter;
            result.RecordHarUrlRegex = options.RecordHarUrlFilterRegex;
            result.RecordVideoDir = options.RecordVideoDir;
            result.RecordVideoSize = options.RecordVideoSize;
            result.ServiceWorkers = options.ServiceWorkers ?? default;
            result.ScreenSize = options.ScreenSize;
            return result;
        }

        internal static BrowserContextOptions ToBrowserContextOptions(BrowserNewContextOptions options)
        {
            if (options == null)
            {
                return null;
            }

            BrowserContextOptions result = new BrowserContextOptions
            {
                Viewport = options.ViewportSize,
                Locale = options.Locale,
                TimezoneId = options.TimezoneId,
                UserAgent = options.UserAgent,
                DeviceScaleFactor = options.DeviceScaleFactor,
                IsMobile = options.IsMobile,
                HasTouch = options.HasTouch,
                ColorScheme = options.ColorScheme ?? default,
                ReducedMotion = options.ReducedMotion ?? default,
                ForcedColors = options.ForcedColors ?? default,
                Contrast = options.Contrast ?? default,
                AcceptDownloads = options.AcceptDownloads ?? true,
                BypassCSP = options.BypassCSP ?? false,
                IgnoreHTTPSErrors = options.IgnoreHTTPSErrors ?? false,
                JavaScriptEnabled = options.JavaScriptEnabled ?? true,
                Offline = options.Offline ?? false,
                StrictSelectors = options.StrictSelectors ?? false,
                BaseURL = options.BaseURL,
                ExtraHTTPHeaders = options.ExtraHTTPHeaders == null
                    ? null
                    : new Dictionary<string, string>(options.ExtraHTTPHeaders),
                Geolocation = options.Geolocation,
                HttpCredentials = options.HttpCredentials,
                Permissions = options.Permissions == null ? null : options.Permissions.ToArray(),
                ClientCertificates = options.ClientCertificates,
                RecordHarPath = options.RecordHarPath,
                RecordHarMode = options.RecordHarMode ?? default,
                RecordHarContent = options.RecordHarContent ?? default,
                RecordHarOmitContent = options.RecordHarOmitContent,
                RecordHarUrl = options.RecordHarUrlFilterString ?? options.RecordHarUrlFilter,
                RecordHarUrlRegex = options.RecordHarUrlFilterRegex,
                RecordVideoDir = options.RecordVideoDir,
                RecordVideoSize = options.RecordVideoSize,
                ServiceWorkers = options.ServiceWorkers ?? default,
                ScreenSize = options.ScreenSize,
                Proxy = options.Proxy,
                StorageState = options.StorageState,
                StorageStatePath = options.StorageStatePath,
            };

            return result;
        }

        internal static BrowserContextOptions ToBrowserContextOptions(BrowserNewPageOptions options)
        {
            if (options == null)
            {
                return null;
            }

            return ToBrowserContextOptions(new BrowserNewContextOptions
            {
                AcceptDownloads = options.AcceptDownloads,
                BaseURL = options.BaseURL,
                BypassCSP = options.BypassCSP,
                ClientCertificates = options.ClientCertificates,
                ColorScheme = options.ColorScheme,
                Contrast = options.Contrast,
                DeviceScaleFactor = options.DeviceScaleFactor,
                ExtraHTTPHeaders = options.ExtraHTTPHeaders,
                ForcedColors = options.ForcedColors,
                Geolocation = options.Geolocation,
                HasTouch = options.HasTouch,
                HttpCredentials = options.HttpCredentials,
                IgnoreHTTPSErrors = options.IgnoreHTTPSErrors,
                IsMobile = options.IsMobile,
                JavaScriptEnabled = options.JavaScriptEnabled,
                Locale = options.Locale,
                Offline = options.Offline,
                Permissions = options.Permissions,
                Proxy = options.Proxy,
                RecordHarContent = options.RecordHarContent,
                RecordHarMode = options.RecordHarMode,
                RecordHarOmitContent = options.RecordHarOmitContent,
                RecordHarPath = options.RecordHarPath,
                RecordHarUrlFilter = options.RecordHarUrlFilter,
                RecordHarUrlFilterRegex = options.RecordHarUrlFilterRegex,
                RecordHarUrlFilterString = options.RecordHarUrlFilterString,
                RecordVideoDir = options.RecordVideoDir,
                RecordVideoSize = options.RecordVideoSize,
                ReducedMotion = options.ReducedMotion,
                ScreenSize = options.ScreenSize,
                ServiceWorkers = options.ServiceWorkers,
                StorageState = options.StorageState,
                StorageStatePath = options.StorageStatePath,
                StrictSelectors = options.StrictSelectors,
                TimezoneId = options.TimezoneId,
                UserAgent = options.UserAgent,
                ViewportSize = options.ViewportSize,
            });
        }

        internal static BrowserNewContextOptions ToBrowserNewContextOptions(BrowserContextOptions options)
        {
            if (options == null)
            {
                return null;
            }

            BrowserNewContextOptions result = new BrowserNewContextOptions
            {
                ViewportSize = options.Viewport,
                Locale = options.Locale,
                TimezoneId = options.TimezoneId,
                UserAgent = options.UserAgent,
                DeviceScaleFactor = options.DeviceScaleFactor,
                IsMobile = options.IsMobile,
                HasTouch = options.HasTouch,
                ColorScheme = options.ColorScheme,
                ReducedMotion = options.ReducedMotion,
                ForcedColors = options.ForcedColors,
                Contrast = options.Contrast,
                AcceptDownloads = options.AcceptDownloads,
                BypassCSP = options.BypassCSP,
                IgnoreHTTPSErrors = options.IgnoreHTTPSErrors,
                JavaScriptEnabled = options.JavaScriptEnabled,
                Offline = options.Offline,
                StrictSelectors = options.StrictSelectors,
                BaseURL = options.BaseURL,
                Geolocation = options.Geolocation,
                HttpCredentials = options.HttpCredentials,
                Permissions = options.Permissions,
                ClientCertificates = options.ClientCertificates,
                RecordHarPath = options.RecordHarPath,
                RecordHarMode = options.RecordHarMode,
                RecordHarContent = options.RecordHarContent,
                RecordHarOmitContent = options.RecordHarOmitContent,
                RecordHarUrlFilterString = options.RecordHarUrl,
                RecordHarUrlFilterRegex = options.RecordHarUrlRegex,
                RecordVideoDir = options.RecordVideoDir,
                RecordVideoSize = options.RecordVideoSize,
                ServiceWorkers = options.ServiceWorkers,
                ScreenSize = options.ScreenSize,
                Proxy = options.Proxy,
                StorageState = options.StorageState,
                StorageStatePath = options.StorageStatePath,
            };

            if (options.ExtraHTTPHeaders != null)
            {
                result.ExtraHTTPHeaders = new Dictionary<string, string>(options.ExtraHTTPHeaders);
            }

            return result;
        }

        internal static BrowserNewPageOptions ToBrowserNewPageOptions(BrowserContextOptions options)
        {
            BrowserNewContextOptions contextOptions = ToBrowserNewContextOptions(options);
            if (contextOptions == null)
            {
                return null;
            }

            return new BrowserNewPageOptions
            {
                AcceptDownloads = contextOptions.AcceptDownloads,
                BaseURL = contextOptions.BaseURL,
                BypassCSP = contextOptions.BypassCSP,
                ClientCertificates = contextOptions.ClientCertificates,
                ColorScheme = contextOptions.ColorScheme,
                Contrast = contextOptions.Contrast,
                DeviceScaleFactor = contextOptions.DeviceScaleFactor,
                ExtraHTTPHeaders = contextOptions.ExtraHTTPHeaders,
                ForcedColors = contextOptions.ForcedColors,
                Geolocation = contextOptions.Geolocation,
                HasTouch = contextOptions.HasTouch,
                HttpCredentials = contextOptions.HttpCredentials,
                IgnoreHTTPSErrors = contextOptions.IgnoreHTTPSErrors,
                IsMobile = contextOptions.IsMobile,
                JavaScriptEnabled = contextOptions.JavaScriptEnabled,
                Locale = contextOptions.Locale,
                Offline = contextOptions.Offline,
                Permissions = contextOptions.Permissions,
                Proxy = contextOptions.Proxy,
                RecordHarContent = contextOptions.RecordHarContent,
                RecordHarMode = contextOptions.RecordHarMode,
                RecordHarOmitContent = contextOptions.RecordHarOmitContent,
                RecordHarPath = contextOptions.RecordHarPath,
                RecordHarUrlFilter = contextOptions.RecordHarUrlFilter,
                RecordHarUrlFilterRegex = contextOptions.RecordHarUrlFilterRegex,
                RecordHarUrlFilterString = contextOptions.RecordHarUrlFilterString,
                RecordVideoDir = contextOptions.RecordVideoDir,
                RecordVideoSize = contextOptions.RecordVideoSize,
                ReducedMotion = contextOptions.ReducedMotion,
                ScreenSize = contextOptions.ScreenSize,
                ServiceWorkers = contextOptions.ServiceWorkers,
                StorageState = contextOptions.StorageState,
                StorageStatePath = contextOptions.StorageStatePath,
                StrictSelectors = contextOptions.StrictSelectors,
                TimezoneId = contextOptions.TimezoneId,
                UserAgent = contextOptions.UserAgent,
                ViewportSize = contextOptions.ViewportSize,
            };
        }

        private static void CopyLaunchOptions(BrowserTypeLaunchOptions source, BrowserTypeLaunchOptions target)
        {
            target.ExecutablePath = source.ExecutablePath;
            target.Args = source.Args;
            target.IgnoreDefaultArgs = source.IgnoreDefaultArgs;
            target.IgnoreDefaultArgsList = source.IgnoreDefaultArgsList;
            target.Env = source.Env;
            target.Headless = source.Headless;
            target.ChromiumSandbox = source.ChromiumSandbox;
            target.Devtools = source.Devtools;
            target.Channel = source.Channel;
            target.Proxy = source.Proxy;
            target.DownloadsPath = source.DownloadsPath;
            target.ArtifactsDir = source.ArtifactsDir;
            target.TracesDir = source.TracesDir;
            target.Timeout = source.Timeout;
            target.UserDataDir = source.UserDataDir;
            target.Port = source.Port;
            target.HandleSIGINT = source.HandleSIGINT;
            target.HandleSIGTERM = source.HandleSIGTERM;
            target.HandleSIGHUP = source.HandleSIGHUP;
            target.LoggerFactory = source.LoggerFactory;
            target.Logger = source.Logger;
            target.FirefoxUserPrefs = source.FirefoxUserPrefs;
        }

        private static BrowserChannel ParseChannel(string channel)
        {
            if (string.IsNullOrEmpty(channel))
            {
                return BrowserChannel.Undefined;
            }

            switch (channel.ToUpperInvariant())
            {
                case "CHROME":
                    return BrowserChannel.Chrome;
                case "CHROME-BETA":
                    return BrowserChannel.ChromeBeta;
                case "CHROME-DEV":
                    return BrowserChannel.ChromeDev;
                case "CHROME-CANARY":
                    return BrowserChannel.ChromeCanary;
                case "MSEDGE":
                    return BrowserChannel.Msedge;
                case "MSEDGE-BETA":
                    return BrowserChannel.MsedgeBeta;
                case "MSEDGE-DEV":
                    return BrowserChannel.MsedgeDev;
                case "MSEDGE-CANARY":
                    return BrowserChannel.MsedgeCanary;
                default:
                    if (Enum.TryParse(channel, ignoreCase: true, out BrowserChannel parsed))
                    {
                        return parsed;
                    }

                    return BrowserChannel.Undefined;
            }
        }
    }
}
