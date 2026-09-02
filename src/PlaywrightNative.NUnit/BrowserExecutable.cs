/*
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
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
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace PlaywrightNative.NUnit;

/// <summary>
/// Resolves and installs browser binaries for PlaywrightNative NUnit fixtures.
/// </summary>
/// <remarks>
/// Resolution order:
/// <list type="number">
/// <item>Environment variable (<c>PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH</c>,
/// <c>PLAYWRIGHT_WEBKIT_EXECUTABLE_PATH</c>, or <c>PLAYWRIGHT_FIREFOX_EXECUTABLE_PATH</c>)
/// when it points to an existing file.</item>
/// <item>An already-installed build in the default <see cref="BrowserFetcher"/> cache.</item>
/// <item>A fresh <see cref="IBrowserFetcher.DownloadAsync()"/> for the pinned build.</item>
/// </list>
/// Prefer calling <see cref="EnsureCurrentProductAsync"/> from a test-assembly
/// <c>[SetUpFixture]</c> so the download happens once up front. <see cref="BrowserTest"/>
/// also ensures the current product lazily on first launch.
/// </remarks>
public static class BrowserExecutable
{
    private static readonly SemaphoreSlim Gate = new SemaphoreSlim(1, 1);
    private static bool _chromiumResolved;
    private static bool _webkitResolved;
    private static bool _firefoxResolved;

    /// <summary>
    /// Gets the resolved Chromium executable path, or <c>null</c> when unavailable.
    /// </summary>
    public static string ChromiumExecutablePath { get; private set; }

    /// <summary>
    /// Gets the resolved WebKit executable path, or <c>null</c> when unavailable.
    /// </summary>
    public static string WebkitExecutablePath { get; private set; }

    /// <summary>
    /// Gets the resolved Firefox executable path, or <c>null</c> when unavailable.
    /// </summary>
    public static string FirefoxExecutablePath { get; private set; }

    /// <summary>
    /// Ensures Chromium is resolved, and WebKit/Firefox when <c>PRODUCT</c>/<c>BROWSER</c>
    /// selects them.
    /// </summary>
    public static async Task EnsureCurrentProductAsync()
    {
        await EnsureAsync("chromium").ConfigureAwait(false);

        string browserName = ResolveBrowserName();
        if (browserName == "webkit")
        {
            await EnsureAsync("webkit").ConfigureAwait(false);
        }
        else if (browserName == "firefox")
        {
            await EnsureAsync("firefox").ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ensures the given browser name (<c>chromium</c>, <c>firefox</c>, or <c>webkit</c>)
    /// is resolved/downloaded.
    /// </summary>
    /// <param name="browserName">Target browser name.</param>
    public static async Task EnsureAsync(string browserName)
    {
        string name = (browserName ?? "chromium").Trim().ToLowerInvariant();
        await Gate.WaitAsync().ConfigureAwait(false);
        try
        {
            switch (name)
            {
                case "firefox":
                    if (!_firefoxResolved)
                    {
                        FirefoxExecutablePath = await ResolveBrowserAsync(
                            SupportedBrowser.Firefox,
                            "PLAYWRIGHT_FIREFOX_EXECUTABLE_PATH").ConfigureAwait(false);
                        _firefoxResolved = true;
                    }

                    break;
                case "webkit":
                    if (!_webkitResolved)
                    {
                        WebkitExecutablePath = await ResolveBrowserAsync(
                            SupportedBrowser.Webkit,
                            "PLAYWRIGHT_WEBKIT_EXECUTABLE_PATH").ConfigureAwait(false);
                        _webkitResolved = true;
                    }

                    break;
                default:
                    if (!_chromiumResolved)
                    {
                        ChromiumExecutablePath = await ResolveBrowserAsync(
                            SupportedBrowser.Chromium,
                            "PLAYWRIGHT_CHROMIUM_EXECUTABLE_PATH").ConfigureAwait(false);
                        _chromiumResolved = true;
                    }

                    break;
            }
        }
        finally
        {
            Gate.Release();
        }
    }

    /// <summary>
    /// Returns the cached executable path for <paramref name="browserName"/>, or <c>null</c>.
    /// Call <see cref="EnsureAsync"/> first.
    /// </summary>
    /// <param name="browserName">Target browser name.</param>
    /// <returns>Executable path or <c>null</c>.</returns>
    public static string GetPath(string browserName)
    {
        string name = (browserName ?? "chromium").Trim().ToLowerInvariant();
        return name switch
        {
            "firefox" => FirefoxExecutablePath,
            "webkit" => WebkitExecutablePath,
            _ => ChromiumExecutablePath,
        };
    }

    /// <summary>
    /// Builds launch options with <c>ExecutablePath</c> filled for the given browser.
    /// Calls <see cref="Assert.Ignore(string)"/> when the binary could not be resolved.
    /// </summary>
    /// <param name="browserName">Target browser name.</param>
    /// <returns>Launch options ready for PlaywrightNative.</returns>
    public static async Task<BrowserTypeLaunchOptions> CreateLaunchOptionsAsync(string browserName)
    {
        await EnsureAsync(browserName).ConfigureAwait(false);
        string path = GetPath(browserName);
        string name = (browserName ?? "chromium").Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(path))
        {
            string label = name switch
            {
                "firefox" => "Firefox",
                "webkit" => "WebKit",
                _ => "Chromium",
            };
            Assert.Ignore($"{label} executable not available (download skipped or failed).");
        }

        return new BrowserTypeLaunchOptions { ExecutablePath = path };
    }

    /// <summary>
    /// Resolves the browser name from <c>PRODUCT</c> or <c>BROWSER</c>.
    /// </summary>
    /// <returns><c>chromium</c>, <c>firefox</c>, or <c>webkit</c>.</returns>
    public static string ResolveBrowserName()
    {
        string product = Environment.GetEnvironmentVariable("PRODUCT");
        if (!string.IsNullOrEmpty(product))
        {
            if (product.Equals("FIREFOX", StringComparison.OrdinalIgnoreCase))
            {
                return "firefox";
            }

            if (product.Equals("WEBKIT", StringComparison.OrdinalIgnoreCase))
            {
                return "webkit";
            }

            return "chromium";
        }

        string browser = Environment.GetEnvironmentVariable("BROWSER");
        return string.IsNullOrEmpty(browser) ? "chromium" : browser.Trim().ToLowerInvariant();
    }

    private static async Task<string> ResolveBrowserAsync(SupportedBrowser browser, string envVar)
    {
        string envPath = Environment.GetEnvironmentVariable(envVar);
        if (!string.IsNullOrEmpty(envPath) && File.Exists(envPath))
        {
            return envPath;
        }

        BrowserFetcher fetcher = new BrowserFetcher(browser);

        InstalledBrowser existing = fetcher.GetInstalledBrowsers()
            .FirstOrDefault(b => b.Browser == browser);
        if (existing != null)
        {
            string existingPath = existing.GetExecutablePath();
            if (File.Exists(existingPath))
            {
                return existingPath;
            }
        }

        try
        {
            InstalledBrowser downloaded = await fetcher.DownloadAsync().ConfigureAwait(false);
            string downloadedPath = downloaded.GetExecutablePath();
            if (File.Exists(downloadedPath))
            {
                return downloadedPath;
            }
        }
        catch
        {
            // Network unreachable, archive corrupt, or extraction failed.
        }

        return null;
    }
}

/// <summary>
/// Optional assembly-level prefetch for browser binaries. Include a subclass (or
/// identical <c>[SetUpFixture]</c>) in the <em>test</em> assembly so NUnit runs it;
/// fixtures in referenced packages are not discovered as test assemblies.
/// </summary>
public class BrowserExecutableFixture
{
    /// <summary>
    /// Gets <see cref="BrowserExecutable.ChromiumExecutablePath"/>.
    /// </summary>
    public static string ChromiumExecutablePath => BrowserExecutable.ChromiumExecutablePath;

    /// <summary>
    /// Gets <see cref="BrowserExecutable.WebkitExecutablePath"/>.
    /// </summary>
    public static string WebkitExecutablePath => BrowserExecutable.WebkitExecutablePath;

    /// <summary>
    /// Gets <see cref="BrowserExecutable.FirefoxExecutablePath"/>.
    /// </summary>
    public static string FirefoxExecutablePath => BrowserExecutable.FirefoxExecutablePath;

    /// <summary>
    /// Prefetches browsers for the current product.
    /// </summary>
    public virtual Task ResolveAsync() => BrowserExecutable.EnsureCurrentProductAsync();
}
