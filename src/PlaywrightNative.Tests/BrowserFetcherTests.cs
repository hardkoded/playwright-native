// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using PlaywrightNative;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    [TestFixture]
    public class BrowserFetcherTests
    {
        [SetUp]
        public void ClearEnvironmentBefore() => ClearEnvironment();

        [TearDown]
        public void ClearEnvironment()
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", null);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_CONNECTION_TIMEOUT", null);
        }

        [PlaywrightTest("browsers-path.spec.ts", "Defaults to chromium and current platform")]
        [Test]
        public void DefaultsToChromiumAndCurrentPlatform()
        {
            BrowserFetcher fetcher = new();
            Assert.That(fetcher.Browser, Is.EqualTo(SupportedBrowser.Chromium));
            Assert.That(fetcher.Platform, Is.EqualTo(BrowserData.CurrentPlatform()));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Options browser overrides default")]
        [Test]
        public void OptionsBrowserOverridesDefault()
        {
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Browser = SupportedBrowser.Firefox });
            Assert.That(fetcher.Browser, Is.EqualTo(SupportedBrowser.Firefox));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Options platform overrides auto detect")]
        [Test]
        public void OptionsPlatformOverridesAutoDetect()
        {
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Platform = Platform.Win64 });
            Assert.That(fetcher.Platform, Is.EqualTo(Platform.Win64));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Cache dir prefers environment variable over options")]
        [Test]
        public void CacheDirPrefersEnvironmentVariableOverOptions()
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "/env/cache");
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Path = "/options/cache" });
            Assert.That(fetcher.CacheDir, Is.EqualTo("/env/cache"));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Cache dir environment zero resolves to assembly directory")]
        [Test]
        public void CacheDirEnvironmentZeroResolvesToAssemblyDirectory()
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_BROWSERS_PATH", "0");
            BrowserFetcher fetcher = new();
            string expected = Path.GetDirectoryName(typeof(BrowserFetcher).Assembly.Location);
            Assert.That(fetcher.CacheDir, Is.EqualTo(expected));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Cache dir falls back to options then default")]
        [Test]
        public void CacheDirFallsBackToOptionsThenDefault()
        {
            BrowserFetcher fromOptions = new(new BrowserFetcherOptions { Path = "/options/cache" });
            Assert.That(fromOptions.CacheDir, Is.EqualTo("/options/cache"));

            BrowserFetcher fromDefault = new();
            Assert.That(fromDefault.CacheDir, Is.EqualTo(BrowserData.DefaultCacheDir()));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Base url prefers environment variable over options")]
        [Test]
        public void BaseUrlPrefersEnvironmentVariableOverOptions()
        {
            Environment.SetEnvironmentVariable("PLAYWRIGHT_DOWNLOAD_HOST", "https://env.test");
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Host = "https://options.test" });
            Assert.That(fetcher.BaseUrl, Is.EqualTo("https://env.test"));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Base url falls back to options then null")]
        [Test]
        public void BaseUrlFallsBackToOptionsThenNull()
        {
            BrowserFetcher fromOptions = new(new BrowserFetcherOptions { Host = "https://options.test" });
            Assert.That(fromOptions.BaseUrl, Is.EqualTo("https://options.test"));

            BrowserFetcher fromDefault = new();
            Assert.That(fromDefault.BaseUrl, Is.Null);
        }

        [PlaywrightTest("browsers-path.spec.ts", "Setters override initial values")]
        [Test]
        public void SettersOverrideInitialValues()
        {
            BrowserFetcher fetcher = new();
            fetcher.Browser = SupportedBrowser.Webkit;
            fetcher.Platform = Platform.Linux;
            fetcher.CacheDir = "/new/cache";
            fetcher.BaseUrl = "https://new.test";
            Assert.That(fetcher.Browser, Is.EqualTo(SupportedBrowser.Webkit));
            Assert.That(fetcher.Platform, Is.EqualTo(Platform.Linux));
            Assert.That(fetcher.CacheDir, Is.EqualTo("/new/cache"));
            Assert.That(fetcher.BaseUrl, Is.EqualTo("https://new.test"));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Get executable path builds path in cache dir")]
        [Test]
        public void GetExecutablePathBuildsPathInCacheDir()
        {
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Path = "/tmp/cache", Platform = Platform.Linux });
            string actual = fetcher.GetExecutablePath("9999");
            Assert.That(actual, Is.EqualTo(Path.Combine("/tmp/cache", "chromium-9999", "chrome-linux", "chrome")));
        }

        [PlaywrightTest("browsers-path.spec.ts", "Get installed browsers returns empty when cache missing")]
        [Test]
        public void GetInstalledBrowsersReturnsEmptyWhenCacheMissing()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-fetcher-test-" + Guid.NewGuid());
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Path = tempCache });
            Assert.That(fetcher.GetInstalledBrowsers(), Is.Empty);
        }

        [PlaywrightTest("browsers-path.spec.ts", "Get installed browsers lists completed builds only")]
        [Test]
        public void GetInstalledBrowsersListsCompletedBuildsOnly()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-fetcher-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempCache);
            try
            {
                string completedDir = Path.Combine(tempCache, "chromium-1111");
                Directory.CreateDirectory(completedDir);
                File.WriteAllText(Path.Combine(completedDir, "INSTALLATION_COMPLETE"), string.Empty);

                string partialDir = Path.Combine(tempCache, "firefox-2222");
                Directory.CreateDirectory(partialDir); // no marker

                BrowserFetcher fetcher = new(new BrowserFetcherOptions { Path = tempCache });
                List<InstalledBrowser> found = new(fetcher.GetInstalledBrowsers());

                Assert.That(found, Has.Count.EqualTo(1));
                Assert.That(found[0].Browser, Is.EqualTo(SupportedBrowser.Chromium));
                Assert.That(found[0].BuildId, Is.EqualTo("1111"));
                Assert.That(found[0].InstallationDir, Is.EqualTo(completedDir));
            }
            finally
            {
                Directory.Delete(tempCache, recursive: true);
            }
        }

        [PlaywrightTest("browsers-path.spec.ts", "Uninstall removes build directory when present")]
        [Test]
        public void UninstallRemovesBuildDirectoryWhenPresent()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-fetcher-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempCache);
            try
            {
                string buildDir = Path.Combine(tempCache, "chromium-3333");
                Directory.CreateDirectory(buildDir);
                File.WriteAllText(Path.Combine(buildDir, "INSTALLATION_COMPLETE"), string.Empty);

                BrowserFetcher fetcher = new(new BrowserFetcherOptions { Path = tempCache });
                fetcher.Uninstall("3333");

                Assert.That(Directory.Exists(buildDir), Is.False);
            }
            finally
            {
                if (Directory.Exists(tempCache))
                {
                    Directory.Delete(tempCache, recursive: true);
                }
            }
        }

        [PlaywrightTest("browsers-path.spec.ts", "Uninstall is no op when build missing")]
        [Test]
        public void UninstallIsNoOpWhenBuildMissing()
        {
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-fetcher-test-" + Guid.NewGuid());
            BrowserFetcher fetcher = new(new BrowserFetcherOptions { Path = tempCache });
            Assert.DoesNotThrow(() => fetcher.Uninstall("does-not-exist"));
        }

        [PlaywrightTest("browsers-path.spec.ts", "DownloadAsync no arg delegates to default build")]
        [Test]
        public void DownloadAsyncNoArgDelegatesToDefaultBuild()
        {
            // Cache hit path: marker present, no network involved.
            string tempCache = Path.Combine(Path.GetTempPath(), "pwsharp-fetcher-test-" + Guid.NewGuid());
            Directory.CreateDirectory(tempCache);
            try
            {
                string buildDir = Path.Combine(tempCache, $"chromium-{BrowserData.ChromiumRevision}");
                Directory.CreateDirectory(buildDir);
                File.WriteAllText(Path.Combine(buildDir, "INSTALLATION_COMPLETE"), string.Empty);

                BrowserFetcher fetcher = new(new BrowserFetcherOptions
                {
                    Browser = SupportedBrowser.Chromium,
                    Path = tempCache,
                    Platform = Platform.Linux,
                });

                InstalledBrowser installed = fetcher.DownloadAsync().GetAwaiter().GetResult();

                Assert.That(installed.Browser, Is.EqualTo(SupportedBrowser.Chromium));
                Assert.That(installed.BuildId, Is.EqualTo(BrowserData.ChromiumRevision));
                Assert.That(installed.InstallationDir, Is.EqualTo(buildDir));
            }
            finally
            {
                Directory.Delete(tempCache, recursive: true);
            }
        }
    }
}
