/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable SA1402, SA1649, CA1044, CA1061
using System.Collections.Generic;
using System.Threading;
using Microsoft.Playwright;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp.Compat
{
    /// <summary>Single or multiple HTTP credentials for legacy API request options.</summary>
#pragma warning disable CA2225
    public readonly struct HttpCredentialsSetting
    {
        /// <summary>Single credential.</summary>
        public HttpCredentials Credential { get; init; }

        /// <summary>Multiple credentials.</summary>
        public HttpCredentials[] Credentials { get; init; }

        /// <summary>Implicit conversion from a single credential.</summary>
        public static implicit operator HttpCredentialsSetting(HttpCredentials value) => new() { Credential = value };

        /// <summary>Implicit conversion from a credential array.</summary>
        public static implicit operator HttpCredentialsSetting(HttpCredentials[] value) => new() { Credentials = value };
    }
#pragma warning restore CA2225

    /// <summary>
    /// Legacy page wait-for-selector options with <c>WaitFor</c>/<c>Visibility</c> spellings.
    /// </summary>
    public class LegacyPageWaitForSelectorOptions : Microsoft.Playwright.PageWaitForSelectorOptions
    {
        /// <summary>Legacy alias for <see cref="State"/> accepting strings or booleans.</summary>
        public new object State
        {
            set => base.State = WaitForSelectorName.ToOfficialState(value);
        }

        /// <summary>Legacy wait-for string (visible/hidden/attached/detached).</summary>
        public string WaitFor
        {
            set => base.State = WaitForSelectorName.ToOfficialState(value);
        }

        /// <summary>Legacy visibility string (visible/hidden).</summary>
        public string Visibility
        {
            set => base.State = WaitForSelectorName.ToOfficialState(value, value);
        }
    }

    /// <summary>Legacy frame wait-for-selector options.</summary>
    public class LegacyFrameWaitForSelectorOptions : Microsoft.Playwright.FrameWaitForSelectorOptions
    {
        /// <summary>Legacy alias for <see cref="State"/> accepting strings or booleans.</summary>
        public new object State
        {
            set => base.State = WaitForSelectorName.ToOfficialState(value);
        }

        /// <summary>Legacy wait-for string.</summary>
        public string WaitFor
        {
            set => base.State = WaitForSelectorName.ToOfficialState(value);
        }

        /// <summary>Legacy visibility string.</summary>
        public string Visibility
        {
            set => base.State = WaitForSelectorName.ToOfficialState(value, value);
        }
    }

    /// <summary>Legacy element-handle press options with <see cref="Force"/>.</summary>
    public class LegacyElementHandlePressOptions : Microsoft.Playwright.ElementHandlePressOptions
    {
        /// <summary>Whether to bypass actionability checks.</summary>
        public bool? Force { get; set; }
    }

    /// <summary>Legacy element-handle type options with <see cref="Force"/>.</summary>
    public class LegacyElementHandleTypeOptions : Microsoft.Playwright.ElementHandleTypeOptions
    {
        /// <summary>Whether to bypass actionability checks.</summary>
        public bool? Force { get; set; }
    }

    /// <summary>Legacy element-handle set-input-files options with <see cref="Force"/>.</summary>
    public class LegacyElementHandleSetInputFilesOptions : Microsoft.Playwright.ElementHandleSetInputFilesOptions
    {
        /// <summary>Whether to bypass actionability checks.</summary>
        public bool? Force { get; set; }
    }

    /// <summary>Legacy page click options with mouse <see cref="Steps"/>.</summary>
    public class LegacyPageClickOptions : Microsoft.Playwright.PageClickOptions
    {
        /// <summary>Number of mouse move steps.</summary>
        public int? Steps { get; set; }
    }

    /// <summary>Legacy frame click options with mouse <see cref="Steps"/>.</summary>
    public class LegacyFrameClickOptions : Microsoft.Playwright.FrameClickOptions
    {
        /// <summary>Number of mouse move steps.</summary>
        public int? Steps { get; set; }
    }

    /// <summary>Legacy page focus options with scroll mode.</summary>
    public class LegacyPageFocusOptions : Microsoft.Playwright.PageFocusOptions
    {
        /// <summary>Scroll-into-view mode.</summary>
        public ActionScroll Scroll { get; set; }
    }

    /// <summary>Legacy frame focus options with scroll mode.</summary>
    public class LegacyFrameFocusOptions : Microsoft.Playwright.FrameFocusOptions
    {
        /// <summary>Scroll-into-view mode.</summary>
        public ActionScroll Scroll { get; set; }
    }

    /// <summary>Legacy wait-for-file-chooser options with cancellation token.</summary>
    public class LegacyPageWaitForFileChooserOptions : Microsoft.Playwright.PageWaitForFileChooserOptions
    {
        /// <summary>Cancellation token (PlaywrightSharp tests only).</summary>
        public CancellationToken CancellationToken { get; set; }
    }

    /// <summary>Legacy wait-for-load-state options accepting load state in the bag.</summary>
    public class LegacyPageWaitForLoadStateOptions : Microsoft.Playwright.PageWaitForLoadStateOptions
    {
        /// <summary>Load state to wait for (string or enum).</summary>
        public object State { get; set; }
    }

    /// <summary>Legacy locator wait-for-function options with <see cref="Arg"/>.</summary>
    public class LegacyLocatorWaitForFunctionOptions : Microsoft.Playwright.LocatorWaitForFunctionOptions
    {
        /// <summary>Function argument.</summary>
        public object Arg { get; set; }
    }

    /// <summary>Legacy persistent-context launch options with regex HAR URL filter alias.</summary>
    public class LegacyBrowserTypeLaunchPersistentContextOptions : Microsoft.Playwright.BrowserTypeLaunchPersistentContextOptions
    {
        /// <summary>Legacy HAR URL glob filter (PlaywrightSharp-only).</summary>
        public new string RecordHarUrlFilter { get; set; }

        /// <summary>Legacy HAR URL regex filter (PlaywrightSharp-only).</summary>
        public new System.Text.RegularExpressions.Regex RecordHarUrlFilterRegex { get; set; }
    }

    /// <summary>Legacy locator click options with abort signal and mouse steps.</summary>
    public class LegacyLocatorClickOptions : Microsoft.Playwright.LocatorClickOptions
    {
        /// <summary>Abort signal (PlaywrightSharp-only).</summary>
        public AbortSignal Signal { get; set; }

        /// <summary>Scroll-into-view mode.</summary>
        public new ActionScroll Scroll { get; set; }
    }

    /// <summary>Legacy locator hover options with scroll mode.</summary>
    public class LegacyLocatorHoverOptions : Microsoft.Playwright.LocatorHoverOptions
    {
        /// <summary>Scroll-into-view mode.</summary>
        public new ActionScroll Scroll { get; set; }
    }

    /// <summary>Legacy page drag-and-drop options with scroll mode and steps.</summary>
    public class LegacyPageDragAndDropOptions : Microsoft.Playwright.PageDragAndDropOptions
    {
    }

    /// <summary>Legacy API request new-context options with credential arrays.</summary>
    public class LegacyAPIRequestNewContextOptions : Microsoft.Playwright.APIRequestNewContextOptions
    {
        private HttpCredentialsSetting _httpCredentials;

        /// <summary>HTTP credentials (single value or array).</summary>
        public new HttpCredentialsSetting HttpCredentials
        {
            get => _httpCredentials;
            set
            {
                _httpCredentials = value;
                if (value.Credentials != null && value.Credentials.Length == 1)
                {
                    base.HttpCredentials = value.Credentials[0];
                }
                else if (value.Credential != null)
                {
                    base.HttpCredentials = value.Credential;
                }
            }
        }

        /// <summary>Resolved credentials for PlaywrightSharp dispatch.</summary>
        internal IEnumerable<HttpCredentials> ResolveHttpCredentials()
        {
            if (_httpCredentials.Credentials != null)
            {
                return _httpCredentials.Credentials;
            }

            if (_httpCredentials.Credential != null)
            {
                return new[] { _httpCredentials.Credential };
            }

            return base.HttpCredentials != null ? new[] { base.HttpCredentials } : null;
        }
    }

    /// <summary>Legacy clip type exposing <see cref="Size"/>.</summary>
    public class LegacyClip : Microsoft.Playwright.Clip
    {
        /// <summary>Legacy size alias (<see cref="Width"/>/<see cref="Height"/>).</summary>
        public ScreenSize Size
        {
            get => new ScreenSize { Width = (int)Width, Height = (int)Height };
            set
            {
                if (value == null)
                {
                    return;
                }

                Width = value.Width;
                Height = value.Height;
            }
        }
    }

    internal static class WaitForSelectorName
    {
        internal static WaitForSelectorState? ToOfficialState(object value, string visibility = null)
            => Helpers.WaitForSelectorName.ToOfficialState(value, visibility);
    }
}
