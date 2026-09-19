// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0

using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlaywrightNative
{
    /// <summary>
    /// Options for <see cref="IBrowserType.LaunchPersistentContextAsync(string, BrowserTypeLaunchOptions)"/>.
    /// Includes launch options plus context emulation such as
    /// <see cref="ViewportSize"/>.
    /// </summary>
    public class BrowserTypeLaunchPersistentContextOptions : BrowserTypeLaunchOptions
    {
        /// <summary>
        /// Emulated viewport. Applied to pages created in the persistent context.
        /// </summary>
        public ViewportSize ViewportSize { get; set; }

        /// <summary>
        /// Specify user locale, for example <c>en-GB</c> or <c>de-DE</c>.
        /// Affects <c>navigator.language</c> and the <c>Accept-Language</c> header.
        /// </summary>
        public string Locale { get; set; }

        /// <summary>
        /// Changes the timezone of the persistent context. See ICU's
        /// <c>metaZones.txt</c> for supported IANA timezone IDs.
        /// </summary>
        public string TimezoneId { get; set; }

        /// <summary>
        /// Specific user agent to use in this persistent context.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Whether to emulate network being offline. Defaults to <see langword="false"/>.
        /// </summary>
        public bool? Offline { get; set; }

        /// <summary>
        /// Emulates <c>prefers-color-scheme</c>.
        /// </summary>
        public ColorScheme ColorScheme { get; set; } = ColorScheme.Null;

        /// <summary>
        /// Emulates <c>prefers-reduced-motion</c>.
        /// </summary>
        public ReducedMotion ReducedMotion { get; set; } = ReducedMotion.Null;

        /// <summary>
        /// Emulates <c>forced-colors</c>.
        /// </summary>
        public ForcedColors ForcedColors { get; set; } = ForcedColors.Null;

        /// <summary>
        /// Specifies if the viewport supports touch events. Defaults to
        /// <see langword="false"/>.
        /// </summary>
        public bool? HasTouch { get; set; }

        /// <summary>
        /// Additional HTTP headers sent with every request from this
        /// persistent context.
        /// </summary>
        public Dictionary<string, string> ExtraHTTPHeaders { get; set; }

        /// <summary>
        /// Changes the geolocation of the persistent context.
        /// </summary>
        public Geolocation Geolocation { get; set; }

        /// <summary>
        /// A list of permissions to grant to all pages in this persistent
        /// context.
        /// </summary>
        public string[] Permissions { get; set; }

        /// <summary>
        /// Toggles bypassing page Content-Security-Policy. Defaults to
        /// <see langword="false"/>.
        /// </summary>
        public bool? BypassCSP { get; set; }

        /// <summary>
        /// Whether to ignore HTTPS errors during navigation. Defaults to
        /// <see langword="false"/>.
        /// </summary>
        public bool? IgnoreHTTPSErrors { get; set; }

        /// <summary>
        /// Whether to enable JavaScript in the persistent context. Defaults to
        /// <see langword="true"/>.
        /// </summary>
        public bool? JavaScriptEnabled { get; set; }

        /// <summary>
        /// Specify device scale factor (can be thought of as dpr). Defaults to
        /// <c>1</c>.
        /// </summary>
        public float? DeviceScaleFactor { get; set; }

        /// <summary>
        /// Whether the meta viewport tag is taken into account and touch
        /// events are enabled. Defaults to <see langword="false"/>.
        /// </summary>
        public bool? IsMobile { get; set; }

        /// <summary>
        /// Emulates consistent <c>window.screen</c> size. Used together with
        /// the viewport.
        /// </summary>
        public ScreenSize ScreenSize { get; set; }

        /// <summary>
        /// Whether to automatically download all attachments. Defaults to
        /// <see langword="false"/> where downloads are canceled.
        /// </summary>
        public bool? AcceptDownloads { get; set; }

        /// <summary>
        /// Prefix used to resolve relative navigation URLs in this
        /// persistent context.
        /// </summary>
        public string BaseURL { get; set; }

        /// <summary>
        /// When <see langword="true"/>, selector actions that target a single
        /// element throw if more than one node matches. Defaults to
        /// <see langword="false"/>.
        /// </summary>
        public bool StrictSelectors { get; set; }

        /// <summary>
        /// Whether pages may register service workers.
        /// <see cref="ServiceWorkerPolicy.Block"/> rejects
        /// <c>navigator.serviceWorker.register</c>.
        /// </summary>
        public ServiceWorkerPolicy ServiceWorkers { get; set; }

        /// <summary>
        /// Credentials for HTTP authentication.
        /// </summary>
        public HttpCredentials HttpCredentials { get; set; }

        /// <summary>
        /// Emulates <c>prefers-contrast</c> for every page in this
        /// persistent context.
        /// </summary>
        public Contrast Contrast { get; set; } = Contrast.Null;

        /// <summary>
        /// Path to write a HAR file to when the persistent context is closed.
        /// </summary>
        public string RecordHarPath { get; set; }

        /// <summary>
        /// When <see langword="true"/>, HAR entries omit response bodies.
        /// </summary>
        public bool? RecordHarOmitContent { get; set; }

        /// <summary>
        /// Optional glob. When set, only matching request URLs are written to the HAR.
        /// </summary>
        public string RecordHarUrl { get; set; }

        /// <summary>
        /// HAR recording detail. <see cref="HarMode.Minimal"/> omits response bodies.
        /// </summary>
        public HarMode RecordHarMode { get; set; }

        /// <summary>
        /// How HAR response bodies are stored. <see cref="HarContentPolicy.Attach"/>
        /// writes them beside the HAR file.
        /// </summary>
        public HarContentPolicy RecordHarContent { get; set; } = (HarContentPolicy)(-1);

        /// <summary>
        /// Optional regular expression. When set, only matching request URLs
        /// are written to the HAR.
        /// </summary>
        public Regex RecordHarUrlRegex { get; set; }

        /// <summary>
        /// Directory to save page videos into. When set, each page records an MP4.
        /// </summary>
        public string RecordVideoDir { get; set; }

        /// <summary>
        /// Optional video frame size. Defaults to 1280x720.
        /// </summary>
        public RecordVideoSize RecordVideoSize { get; set; }

        /// <summary>
        /// Populates the persistent context with the given storage state JSON.
        /// </summary>
        public string StorageState { get; set; }

        /// <summary>
        /// Path to a file with saved storage state.
        /// </summary>
        public string StorageStatePath { get; set; }

        /// <summary>
        /// TLS client certificates presented for matching request origins.
        /// Each entry must include <see cref="ClientCertificate.Origin"/>
        /// and either a cert/key pair or a PFX.
        /// </summary>
        public IEnumerable<ClientCertificate> ClientCertificates { get; set; }
    }
}
