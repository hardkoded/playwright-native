using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;
using PlaywrightNative.Helpers;
using PlaywrightNative.Transport.Protocol;

namespace PlaywrightNative
{
    /// <summary>
    /// TODO: This class needs to be updated, as does its documentation.
    /// </summary>
    public class BrowserContextOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="BrowserContextOptions"/> class.
        /// </summary>
        public BrowserContextOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="BrowserContextOptions"/> class.
        /// </summary>
        /// <param name="options">Device used to hydrate initial values.</param>
        public BrowserContextOptions(BrowserContextOptions options) => options?.CopyFrom(this);

        /// <summary>
        /// Sets a consistent viewport for each page. Defaults to an 800x600 viewport. null disables the default viewport.
        /// </summary>
        public ViewportSize Viewport { get; set; } = ViewportSizeHelper.Default;

        /// <summary>
        /// Specific user agent to use in this context.
        /// </summary>
        public string UserAgent { get; set; }

        /// <summary>
        /// Official device <c>defaultBrowserType</c> (<c>chromium</c>,
        /// <c>firefox</c>, or <c>webkit</c>).
        /// </summary>
        public string DefaultBrowserType { get; set; }

        /// <summary>
        /// Official Playwright <c>logger</c> for API-call start/success lines
        /// on this context and its pages.
        /// </summary>
        public IPlaywrightLogger Logger { get; set; }

        /// <summary>
        /// Toggles bypassing page's Content-Security-Policy.
        /// </summary>
        public bool? BypassCSP { get; set; }

        /// <summary>
        /// Whether or not to enable or disable JavaScript in the context. Defaults to true.
        /// </summary>
        public bool? JavaScriptEnabled { get; set; }

        /// <summary>
        /// Whether to ignore HTTPS errors during navigation. Defaults to false.
        /// </summary>
        public bool? IgnoreHTTPSErrors { get; set; }

        /// <summary>
        /// Changes the timezone of the context. See <see href="https://cs.chromium.org/chromium/src/third_party/icu/source/data/misc/metaZones.txt?rcl=faee8bc70570192d82d2978a71e2a615788597d1">ICU’s metaZones.txt</see> for a list of supported timezone IDs.
        /// </summary>
        public string TimezoneId { get; set; }

        /// <summary>
        /// Changes the Geolocation of the context.
        /// </summary>
        public Geolocation Geolocation { get; set; }

        /// <summary>
        /// A collection from origin keys to permissions values.
        /// </summary>
        public string[] Permissions { get; set; }

        /// <summary>
        /// Gets or sets whether the meta viewport tag is taken into account.
        /// </summary>
        public bool? IsMobile { get; set; }

        /// <summary>
        /// Whether to emulate network being offline. Defaults to `false`.
        /// </summary>
        public bool? Offline { get; set; }

        /// <summary>
        /// Gets or sets the device scale factor.
        /// </summary>
        public float? DeviceScaleFactor { get; set; }

        /// <summary>
        /// Emulates consistent <c>window.screen</c> size. Used together with the viewport.
        /// </summary>
        public ScreenSize ScreenSize { get; set; }

        /// <summary>
        /// Credentials for HTTP authentication.
        /// </summary>
        public HttpCredentials HttpCredentials { get; set; }

        /// <summary>
        /// Specifies if viewport supports touch events. Defaults to false.
        /// </summary>
        public bool? HasTouch { get; set; }

        /// <summary>
        /// Whether to automatically download all the attachments. Defaults to false where all the downloads are canceled.
        /// </summary>
        public bool? AcceptDownloads { get; set; }

        /// <summary>
        /// Emulates 'prefers-colors-scheme' media feature.
        /// </summary>
        public ColorScheme ColorScheme { get; set; } = ColorScheme.Null;

        /// <summary>
        /// Specify user locale, for example en-GB, de-DE, etc. Locale will affect navigator.language value, Accept-Language request header value as well as number and date formatting rules.
        /// </summary>
        public string Locale { get; set; }

        /// <summary>
        /// An object containing additional HTTP headers to be sent with every request.
        /// </summary>
        public Dictionary<string, string> ExtraHTTPHeaders { get; set; }

        /// <summary>
        /// Network proxy settings to use with this context. Note that browser needs to be launched with the global proxy for this option to work. If all contexts override the proxy, global proxy will be never used and can be any string..
        /// </summary>
        public Proxy Proxy { get; set; }

        /// <summary>
        /// Path to write a HAR file to when the context is closed.
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
        /// Optional regular expression. When set, only matching request URLs are written to the HAR.
        /// </summary>
        public Regex RecordHarUrlRegex { get; set; }

        /// <summary>
        /// HAR recording detail. <see cref="HarMode.Minimal"/> omits response bodies.
        /// </summary>
        public HarMode RecordHarMode { get; set; }

        /// <summary>
        /// Prefix used to resolve relative navigation URLs.
        /// </summary>
        public string BaseURL { get; set; }

        /// <summary>
        /// Whether pages may register service workers. <see cref="ServiceWorkerPolicy.Block"/>
        /// rejects <c>navigator.serviceWorker.register</c>.
        /// </summary>
        public ServiceWorkerPolicy ServiceWorkers { get; set; }

        /// <summary>
        /// Emulates <c>prefers-reduced-motion</c> for every page in the context.
        /// </summary>
        public ReducedMotion ReducedMotion { get; set; } = ReducedMotion.Null;

        /// <summary>
        /// Emulates <c>forced-colors</c> for every page in the context.
        /// </summary>
        public ForcedColors ForcedColors { get; set; } = ForcedColors.Null;

        /// <summary>
        /// Emulates <c>prefers-contrast</c> for every page in the context.
        /// </summary>
        public Contrast Contrast { get; set; } = Contrast.Null;

        /// <summary>
        /// How HAR response bodies are stored. <see cref="HarContentPolicy.Attach"/>
        /// writes them beside the HAR file.
        /// </summary>
        public HarContentPolicy RecordHarContent { get; set; } = (HarContentPolicy)(-1);

        /// <summary>
        /// When <see langword="true"/>, selector actions that target a single
        /// element throw if more than one node matches. Defaults to
        /// <see langword="false"/>. Does not affect
        /// <see cref="IPage.QuerySelectorAsync(string)"/>.
        /// </summary>
        public bool StrictSelectors { get; set; }

        /// <summary>
        /// TLS client certificates presented for matching request origins.
        /// Each entry must include <see cref="ClientCertificate.Origin"/>
        /// and either a cert/key pair or a PFX.
        /// </summary>
        public IEnumerable<ClientCertificate> ClientCertificates { get; set; }

        /// <summary>
        /// Directory to save page videos into. When set, each page records an MP4.
        /// </summary>
        public string RecordVideoDir { get; set; }

        /// <summary>
        /// Optional video frame size. Defaults to 1280x720.
        /// </summary>
        public RecordVideoSize RecordVideoSize { get; set; }

        /// <summary>
        /// Path to the file with saved storage.
        /// </summary>
        public string StorageStatePath { get; set; }

        /// <summary>
        /// Populates context with given storage state.
        /// </summary>
        public string StorageState { get; set; }

        private BrowserContextOptions CopyFrom(BrowserContextOptions options)
        {
            options.UserAgent = UserAgent;
            options.Viewport = Viewport;
            options.HasTouch = HasTouch;
            options.IsMobile = IsMobile;
            options.DeviceScaleFactor = DeviceScaleFactor;

            return options;
        }
    }
}
