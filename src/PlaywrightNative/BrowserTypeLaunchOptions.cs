// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System.Collections.Generic;
using Microsoft.Extensions.Logging;

namespace PlaywrightNative
{
    /// <summary>
    /// Options for <see cref="Playwright.LaunchChromiumAsync(BrowserTypeLaunchOptions)"/>
    /// and <see cref="Playwright.LaunchFirefoxAsync(BrowserTypeLaunchOptions)"/>.
    /// </summary>
    public class BrowserTypeLaunchOptions
    {
        /// <summary>
        /// Path to the browser binary. When <c>null</c>, the binary is downloaded
        /// via a default <see cref="BrowserFetcher"/> and cached locally.
        /// </summary>
        public string ExecutablePath { get; set; }

        /// <summary>
        /// Additional arguments to pass to the browser process. Appended after
        /// PlaywrightNative's default arguments.
        /// </summary>
        public IEnumerable<string> Args { get; set; }

        /// <summary>
        /// When <see langword="true"/>, Chromium is launched without PlaywrightNative's
        /// default arguments. Required plumbing such as remote debugging is still added.
        /// Ignored by Firefox and WebKit.
        /// </summary>
        /// <remarks>
        /// This is the boolean form of official Playwright <c>ignoreDefaultArgs</c>.
        /// Use <see cref="IgnoreDefaultArgsList"/> to omit specific default switches instead.
        /// </remarks>
        public bool IgnoreDefaultArgs { get; set; }

        /// <summary>
        /// Default Chromium switches to omit from the launch command line (exact match).
        /// Official Playwright <c>ignoreDefaultArgs</c> as a list, for example
        /// <c>["--mute-audio"]</c>. Ignored when <see cref="IgnoreDefaultArgs"/> is
        /// <see langword="true"/>. Ignored by Firefox and WebKit.
        /// </summary>
        public IEnumerable<string> IgnoreDefaultArgsList { get; set; }

        /// <summary>
        /// Extra environment variables for the browser process. These overlay the
        /// current process environment.
        /// </summary>
        public IReadOnlyDictionary<string, string> Env { get; set; }

        /// <summary>Whether to run the browser headless. Defaults to <c>true</c>.</summary>
        public bool Headless { get; set; } = true;

        /// <summary>
        /// Enable Chromium's sandbox. Defaults to <c>false</c> (adds <c>--no-sandbox</c>).
        /// Ignored by Firefox and WebKit.
        /// </summary>
        public bool ChromiumSandbox { get; set; }

        /// <summary>
        /// Open Chromium DevTools for each tab. Adds
        /// <c>--auto-open-devtools-for-tabs</c>. Ignored by Firefox and WebKit.
        /// </summary>
        public bool Devtools { get; set; }

        /// <summary>
        /// Installed Chromium or Edge channel to launch (for example
        /// <see cref="BrowserChannel.Chrome"/> or <see cref="BrowserChannel.Msedge"/>).
        /// Ignored when <see cref="ExecutablePath"/> is set. Firefox and WebKit reject
        /// a non-default channel.
        /// </summary>
        public BrowserChannel Channel { get; set; }

        /// <summary>
        /// Network proxy used by the browser process. Chromium also needs a launch-level
        /// proxy (even a dummy <c>per-context</c> server) when contexts override
        /// <see cref="IBrowser.NewContextAsync(BrowserContextOptions)"/> with their own proxy.
        /// </summary>
        public Proxy Proxy { get; set; }

        /// <summary>
        /// If specified, accepted downloads are saved into this directory. Otherwise
        /// a temporary directory is created per context and deleted when the context closes.
        /// </summary>
        public string DownloadsPath { get; set; }

        /// <summary>
        /// If specified, artifacts (downloads, videos, HAR files, and traces) are
        /// saved into this directory. The directory is not cleaned up when the
        /// browser closes. When <see cref="DownloadsPath"/> is omitted, accepted
        /// downloads use this directory.
        /// </summary>
        public string ArtifactsDir { get; set; }

        /// <summary>
        /// Official <c>tracesDir</c>. When set, <c>tracing.start({ name })</c>
        /// writes <c>{name}.trace</c> and <c>{name}.network</c> into this
        /// directory in addition to any zip passed to <c>stop</c>/<c>stopChunk</c>.
        /// </summary>
        public string TracesDir { get; set; }

        /// <summary>
        /// Maximum time in milliseconds to wait for the browser to start.
        /// Defaults to 30000. Pass <c>0</c> to disable the timeout.
        /// </summary>
        public int? Timeout { get; set; }

        /// <summary>
        /// Not supported on <c>browserType.launch</c>. Use
        /// <see cref="IBrowserType.LaunchPersistentContextAsync"/>.
        /// Official <c>library/browsertype-launch.spec.ts</c>.
        /// </summary>
        public string UserDataDir { get; set; }

        /// <summary>
        /// Not supported unless launching a browser server.
        /// Official <c>library/browsertype-launch.spec.ts</c>.
        /// </summary>
        public int? Port { get; set; }

        /// <summary>
        /// Close the browser process on Ctrl-C (<c>SIGINT</c>). Defaults to
        /// <see langword="true"/>.
        /// </summary>
        public bool? HandleSIGINT { get; set; }

        /// <summary>
        /// Close the browser process on <c>SIGTERM</c>. Defaults to
        /// <see langword="true"/>.
        /// </summary>
        public bool? HandleSIGTERM { get; set; }

        /// <summary>
        /// Close the browser process on <c>SIGHUP</c>. Defaults to
        /// <see langword="true"/>.
        /// </summary>
        public bool? HandleSIGHUP { get; set; }

        /// <summary>Optional logger factory.</summary>
        public ILoggerFactory LoggerFactory { get; set; }

        /// <summary>
        /// Official Playwright <c>logger</c> for API-call start/success lines.
        /// </summary>
        public IPlaywrightLogger Logger { get; set; }

        /// <summary>
        /// Firefox preferences written to the profile <c>user.js</c> before launch
        /// (see <c>about:config</c>). Official Playwright <c>firefoxUserPrefs</c>.
        /// Ignored by Chromium and WebKit.
        /// </summary>
        public IEnumerable<KeyValuePair<string, object>> FirefoxUserPrefs { get; set; }
    }
}
