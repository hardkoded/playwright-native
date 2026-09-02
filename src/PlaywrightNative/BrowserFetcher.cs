// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace PlaywrightNative
{
    /// <summary>
    /// Downloads and manages browser binaries from the Playwright CDN.
    /// </summary>
    public sealed class BrowserFetcher : IBrowserFetcher
    {
        private const string BrowsersPathEnvVar = "PLAYWRIGHT_BROWSERS_PATH";
        private const string DownloadHostEnvVar = "PLAYWRIGHT_DOWNLOAD_HOST";
        private const string ConnectionTimeoutEnvVar = "PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT";
        private const int TransientRetryDelayMs = 1000;

        // Static HttpClient cache keyed by proxy identity. Null-proxy uses _noProxySentinel.
        private static readonly object _noProxySentinel = new();
        private static readonly ConcurrentDictionary<object, Lazy<HttpClient>> _httpClients = new();

        private readonly ILoggerFactory _loggerFactory;

        /// <summary>Initializes a new instance of the <see cref="BrowserFetcher"/> class with default options (Chromium, current platform).</summary>
        public BrowserFetcher() : this(new BrowserFetcherOptions(), loggerFactory: null)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="BrowserFetcher"/> class with a single browser override.</summary>
        /// <param name="browser">The browser to download.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public BrowserFetcher(SupportedBrowser browser, ILoggerFactory loggerFactory = null)
            : this(new BrowserFetcherOptions { Browser = browser }, loggerFactory)
        {
        }

        /// <summary>Initializes a new instance of the <see cref="BrowserFetcher"/> class with full options.</summary>
        /// <param name="options">The options bag.</param>
        /// <param name="loggerFactory">Optional logger factory.</param>
        public BrowserFetcher(BrowserFetcherOptions options, ILoggerFactory loggerFactory = null)
        {
            _ = options ?? throw new ArgumentNullException(nameof(options));

            _loggerFactory = loggerFactory;

            Browser = options.Browser;
            Platform = options.Platform ?? BrowserData.CurrentPlatform();
            CacheDir = ResolveCacheDir(options.Path);
            BaseUrl = ResolveBaseUrl(options.Host);
        }

        /// <inheritdoc/>
        public string BaseUrl { get; set; }

        /// <inheritdoc/>
        public string CacheDir { get; set; }

        /// <inheritdoc/>
        public Platform Platform { get; set; }

        /// <inheritdoc/>
        public SupportedBrowser Browser { get; set; }

        /// <inheritdoc/>
        public IWebProxy WebProxy { get; set; }

        /// <inheritdoc/>
        public async Task<bool> CanDownloadAsync(string buildId)
        {
            if (string.IsNullOrEmpty(buildId))
            {
                throw new ArgumentException("buildId must be non-empty.", nameof(buildId));
            }

            string[] urls = DownloadUrls(buildId);
            HttpClient client = GetHttpClient();

            foreach (string url in urls)
            {
                try
                {
                    using HttpRequestMessage request = new(HttpMethod.Head, url);
                    using HttpResponseMessage response = await client.SendAsync(request).ConfigureAwait(false);
                    if (response.IsSuccessStatusCode)
                    {
                        return true;
                    }
                }
                catch (HttpRequestException)
                {
                    // Try the next host.
                }
            }

            return false;
        }

        /// <inheritdoc/>
        public Task<InstalledBrowser> DownloadAsync()
        {
            string playwrightKey = BrowserData.PlaywrightPlatformKey(Browser, Platform);
            string buildId = BrowserData.ResolveRevision(Browser, playwrightKey, requestedRevision: null);
            return DownloadAsync(buildId);
        }

        /// <inheritdoc/>
        public async Task<InstalledBrowser> DownloadAsync(string buildId)
        {
            if (string.IsNullOrEmpty(buildId))
            {
                throw new ArgumentException("buildId must be non-empty.", nameof(buildId));
            }

            string installDir = BrowserData.InstallationDir(CacheDir, Browser, buildId);
            string markerPath = Path.Combine(installDir, "INSTALLATION_COMPLETE");

            if (Directory.Exists(installDir) && File.Exists(markerPath))
            {
                return new InstalledBrowser
                {
                    Browser = Browser,
                    BuildId = buildId,
                    Platform = Platform,
                    InstallationDir = installDir,
                    PermissionsFixed = !RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
                };
            }

            Directory.CreateDirectory(CacheDir);

            string zipPath = installDir + ".dl-" + Guid.NewGuid().ToString("N") + ".zip";
            ILogger log = _loggerFactory?.CreateLogger<BrowserFetcher>();

            try
            {
                await DownloadToFileAsync(buildId, zipPath, log).ConfigureAwait(false);

                if (Directory.Exists(installDir))
                {
                    Directory.Delete(installDir, recursive: true);
                }

                bool permissionsFixed = ArchiveExtractor.Extract(zipPath, installDir);

                File.WriteAllText(markerPath, string.Empty);

                return new InstalledBrowser
                {
                    Browser = Browser,
                    BuildId = buildId,
                    Platform = Platform,
                    InstallationDir = installDir,
                    PermissionsFixed = permissionsFixed,
                };
            }
            catch (Exception ex) when (ex is not OperationCanceledException && ex is not PlaywrightNativeException)
            {
                if (Directory.Exists(installDir))
                {
                    try
                    {
                        Directory.Delete(installDir, recursive: true);
                    }
                    catch (IOException)
                    {
                        // best-effort cleanup
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // best-effort cleanup
                    }
                }

                throw new PlaywrightNativeException($"Failed to download {Browser} build {buildId}: {ex.Message}", ex);
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    try
                    {
                        File.Delete(zipPath);
                    }
                    catch (IOException)
                    {
                        // best-effort cleanup
                    }
                    catch (UnauthorizedAccessException)
                    {
                        // best-effort cleanup
                    }
                }
            }
        }

        /// <inheritdoc/>
        public IEnumerable<InstalledBrowser> GetInstalledBrowsers()
        {
            if (!Directory.Exists(CacheDir))
            {
                yield break;
            }

            foreach (string dir in Directory.EnumerateDirectories(CacheDir))
            {
                string name = Path.GetFileName(dir);
                int dash = name.IndexOf('-');
                if (dash <= 0 || dash == name.Length - 1)
                {
                    continue;
                }

                if (!File.Exists(Path.Combine(dir, "INSTALLATION_COMPLETE")))
                {
                    continue;
                }

                string browserPart = name.Substring(0, dash);
                string buildId = name.Substring(dash + 1);

                SupportedBrowser browser = browserPart switch
                {
                    "chromium" => SupportedBrowser.Chromium,
                    "firefox" => SupportedBrowser.Firefox,
                    "webkit" => SupportedBrowser.Webkit,
                    _ => (SupportedBrowser)(-1),
                };
                if ((int)browser < 0)
                {
                    continue;
                }

                yield return new InstalledBrowser
                {
                    Browser = browser,
                    BuildId = buildId,
                    Platform = Platform,
                    InstallationDir = dir,
                };
            }
        }

        /// <inheritdoc/>
        public void Uninstall(string buildId)
        {
            string dir = BrowserData.InstallationDir(CacheDir, Browser, buildId);
            if (Directory.Exists(dir))
            {
                Directory.Delete(dir, recursive: true);
            }
        }

        /// <inheritdoc/>
        public string GetExecutablePath(string buildId)
        {
            string installDir = BrowserData.InstallationDir(CacheDir, Browser, buildId);
            return BrowserData.ExecutablePath(Browser, Platform, installDir);
        }

        private static string ResolveCacheDir(string optionsPath)
        {
            string env = Environment.GetEnvironmentVariable(BrowsersPathEnvVar);
            if (!string.IsNullOrEmpty(env))
            {
                if (env == "0")
                {
                    return Path.GetDirectoryName(typeof(BrowserFetcher).Assembly.Location);
                }

                return env;
            }

            if (!string.IsNullOrEmpty(optionsPath))
            {
                return optionsPath;
            }

            return BrowserData.DefaultCacheDir();
        }

        private static string ResolveBaseUrl(string optionsHost)
        {
            string env = Environment.GetEnvironmentVariable(DownloadHostEnvVar);
            if (!string.IsNullOrEmpty(env))
            {
                return env;
            }

            return string.IsNullOrEmpty(optionsHost) ? null : optionsHost;
        }

        private static bool IsTransientStatus(int statusCode)
            => statusCode >= 500 || statusCode == 408 || statusCode == 429;

        private HttpClient GetHttpClient()
        {
            object key = WebProxy ?? _noProxySentinel;
            return _httpClients.GetOrAdd(key, _ => new Lazy<HttpClient>(BuildHttpClient)).Value;
        }

        private HttpClient BuildHttpClient()
        {
            HttpClientHandler handler = new()
            {
                CheckCertificateRevocationList = true,
            };

            if (WebProxy != null)
            {
                handler.Proxy = WebProxy;
                handler.UseProxy = true;
            }

            HttpClient client = new(handler, disposeHandler: true);

            string timeoutEnv = Environment.GetEnvironmentVariable(ConnectionTimeoutEnvVar);
            if (!string.IsNullOrEmpty(timeoutEnv) && int.TryParse(timeoutEnv, out int timeoutMs) && timeoutMs > 0)
            {
                client.Timeout = TimeSpan.FromMilliseconds(timeoutMs);
            }
            else
            {
                // Downloads are large; allow plenty of time by default.
                client.Timeout = TimeSpan.FromMinutes(30);
            }

            return client;
        }

        private string[] DownloadUrls(string buildId)
        {
            string playwrightKey = BrowserData.PlaywrightPlatformKey(Browser, Platform);
            string revision = BrowserData.ResolveRevision(Browser, playwrightKey, buildId);
            string[] hosts = string.IsNullOrEmpty(BaseUrl) ? null : new string[] { BaseUrl };
            return BrowserData.DownloadUrls(Browser, playwrightKey, revision, hosts);
        }

        private async Task DownloadToFileAsync(string buildId, string zipPath, ILogger log)
        {
            string[] urls = DownloadUrls(buildId);
            HttpClient client = GetHttpClient();
            List<string> errors = new();

            foreach (string url in urls)
            {
                for (int attempt = 0; attempt < 2; attempt++)
                {
                    try
                    {
                        log?.LogInformation("Downloading {Browser} {BuildId} from {Url} (attempt {Attempt})", Browser, buildId, url, attempt + 1);

                        using HttpResponseMessage response = await client.GetAsync(new Uri(url), HttpCompletionOption.ResponseHeadersRead).ConfigureAwait(false);
                        if (!response.IsSuccessStatusCode)
                        {
                            int code = (int)response.StatusCode;
                            errors.Add($"{url} -> {code}");
                            if (!IsTransientStatus(code) || attempt == 1)
                            {
                                break; // try the next host
                            }

                            await Task.Delay(TransientRetryDelayMs).ConfigureAwait(false);
                            continue;
                        }

                        using FileStream output = File.Create(zipPath);
                        using Stream source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                        await source.CopyToAsync(output).ConfigureAwait(false);
                        return;
                    }
                    catch (HttpRequestException ex)
                    {
                        errors.Add($"{url} -> {ex.Message}");
                        if (attempt == 1)
                        {
                            break;
                        }

                        await Task.Delay(TransientRetryDelayMs).ConfigureAwait(false);
                    }
                    catch (IOException ex)
                    {
                        errors.Add($"{url} -> {ex.Message}");
                        if (attempt == 1)
                        {
                            break;
                        }

                        await Task.Delay(TransientRetryDelayMs).ConfigureAwait(false);
                    }
                }
            }

            throw new PlaywrightNativeException(
                $"Failed to download {Browser} build {buildId} from any of {urls.Length} host(s):{Environment.NewLine}  {string.Join(Environment.NewLine + "  ", errors)}");
        }
    }
}
