/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
#pragma warning disable CA1062
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;
using static PlaywrightNative.Helpers.CompatCollections;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy expanded-parameter helpers over the official options-bag
    /// <see cref="IPage"/> API (playwright-dotnet compatible).
    /// </summary>
    public static class PageCompatExtensions
    {
        /// <summary>
        /// Legacy spelling/shape used throughout this repo's tests.
        /// Prefer <see cref="IPage.GotoAsync(string, PageGotoOptions)"/>.
        /// </summary>
        public static Task<IResponse> GoToAsync(
            this IPage page,
            string url,
            WaitUntilState waitUntil = default,
            float? timeout = default,
            string referer = default)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            return page.GotoAsync(url, new PageGotoOptions
            {
                WaitUntil = waitUntil,
                Timeout = timeout,
                Referer = referer,
            });
        }

        /// <summary>Legacy go-to with string wait-until state.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IResponse> GoToAsync(
            this IPage page,
            string url,
            string waitUntil,
            float? timeout = default,
            string referer = default)
            => page.GoToAsync(url, ParseWaitUntilState(waitUntil), timeout, referer);

        /// <summary>Legacy string-role <c>getByRole</c>.</summary>
        public static ILocator GetByRole(
            this IPage page,
            string role,
            string name = null,
            bool? exact = null,
            bool? checkedState = null,
            bool? disabled = null,
            bool? expanded = null,
            bool? includeHidden = null,
            int? level = null,
            bool? pressed = null,
            bool? selected = null,
            string description = null,
            Regex descriptionRegex = null,
            Regex nameRegex = null)
            => page.Locator(RoleSelector.Build(
                role,
                name,
                exact,
                checkedState,
                disabled,
                expanded,
                includeHidden,
                level,
                pressed,
                selected,
                description,
                descriptionRegex,
                nameRegex));

        /// <summary>Legacy expanded-parameter check.</summary>
        public static Task CheckAsync(
            this IPage page,
            string selector,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.CheckAsync(selector, new PageCheckOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter click.</summary>
        public static Task ClickAsync(
            this IPage page,
            string selector,
            MouseButton button = default,
            int? clickCount = default,
            float? delay = default,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            int? steps = default,
            bool? strict = default)
            => page.ClickAsync(selector, new PageClickOptions
            {
                Button = button,
                ClickCount = clickCount,
                Delay = delay,
                Position = position,
                Modifiers = modifiers,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter double click.</summary>
        public static Task DblClickAsync(
            this IPage page,
            string selector,
            MouseButton button = default,
            float? delay = default,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.DblClickAsync(selector, new PageDblClickOptions
            {
                Button = button,
                Delay = delay,
                Position = position,
                Modifiers = modifiers,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter fill.</summary>
        public static Task FillAsync(
            this IPage page,
            string selector,
            string value,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.FillAsync(selector, value, new PageFillOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter focus.</summary>
        public static Task FocusAsync(
            this IPage page,
            string selector,
            float? timeout = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.FocusAsync(selector, new PageFocusOptions
            {
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter hover.</summary>
        public static Task HoverAsync(
            this IPage page,
            string selector,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.HoverAsync(selector, new PageHoverOptions
            {
                Position = position,
                Modifiers = modifiers,
                Force = force,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter press.</summary>
        public static Task PressAsync(
            this IPage page,
            string selector,
            string key,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.PressAsync(selector, key, new PagePressOptions
            {
                Delay = delay,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter select option (two string values).</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            string value1,
            string value2)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, new[] { value1, value2 }));

        /// <summary>Legacy expanded-parameter select option (two SelectOptionValue values).</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            SelectOptionValue value1,
            SelectOptionValue value2)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, new[] { value1, value2 }));

        /// <summary>Legacy expanded-parameter select option (two values).</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            IEnumerable<SelectOptionValue> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, values, new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            string values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            bool? strict = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, values, new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            IEnumerable<string> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, values, new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, Array.Empty<string>(), new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            IElementHandle values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, values, new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            IEnumerable<IElementHandle> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, values, new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IPage page,
            string selector,
            SelectOptionValue values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(page.SelectOptionAsync(selector, values, new PageSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IPage page,
            string selector,
            IEnumerable<FilePayload> files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.SetInputFilesAsync(selector, files, new PageSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IPage page,
            string selector,
            string files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default)
            => page.SetInputFilesAsync(selector, files, new PageSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IPage page,
            string selector,
            IEnumerable<string> files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default)
            => page.SetInputFilesAsync(selector, files, new PageSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IPage page,
            string selector,
            FilePayload files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default)
            => page.SetInputFilesAsync(selector, files, new PageSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter tap.</summary>
        public static Task TapAsync(
            this IPage page,
            string selector,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? noWaitAfter = default,
            bool? force = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.TapAsync(selector, new PageTapOptions
            {
                Position = position,
                Modifiers = modifiers,
                NoWaitAfter = noWaitAfter,
                Force = force,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter type.</summary>
        public static Task TypeAsync(
            this IPage page,
            string selector,
            string text,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.TypeAsync(selector, text, new PageTypeOptions
            {
                Delay = delay,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter uncheck.</summary>
        public static Task UncheckAsync(
            this IPage page,
            string selector,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => page.UncheckAsync(selector, new PageUncheckOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter wait for selector.</summary>
        public static Task<IElementHandle> WaitForSelectorAsync(
            this IPage page,
            string selector,
            WaitForSelectorState state = default,
            float? timeout = default,
            bool? strict = default)
            => page.WaitForSelectorAsync(selector, new PageWaitForSelectorOptions
            {
                State = state == EnumCompat.UndefinedWaitForSelectorState ? null : state,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter screenshot.</summary>
        public static Task<byte[]> ScreenshotAsync(
            this IPage page,
            string path = default,
            ScreenshotType type = default,
            int? quality = default,
            bool? omitBackground = default,
            float? timeout = default,
            string scale = default,
            string animations = default,
            string caret = default,
            string style = default,
            IEnumerable<ILocator> mask = default,
            string maskColor = default,
            bool? fullPage = default,
            Clip clip = default)
            => page.ScreenshotAsync(new PageScreenshotOptions
            {
                Path = path,
                Type = type == EnumCompat.UndefinedScreenshotType ? null : type,
                Quality = quality,
                OmitBackground = omitBackground,
                Timeout = timeout,
                Style = style,
                Mask = mask,
                MaskColor = maskColor,
                FullPage = fullPage,
                Clip = clip,
            });

        /// <summary>Legacy expanded-parameter set content.</summary>
        public static Task SetContentAsync(
            this IPage page,
            string html,
            WaitUntilState waitUntil = default,
            float? timeout = default)
            => page.SetContentAsync(html, new PageSetContentOptions
            {
                WaitUntil = waitUntil,
                Timeout = timeout,
            });

        /// <summary>Legacy expanded-parameter wait for navigation.</summary>
        public static Task<IResponse> WaitForNavigationAsync(
            this IPage page,
            string url = default,
            string waitUntil = default,
            float? timeout = default,
            string referer = default,
            float? navigationTimeout = default)
            => page.WaitForNavigationAsync(new PageWaitForNavigationOptions
            {
                Url = url,
                Timeout = timeout ?? navigationTimeout,
            });

        /// <summary>Legacy expanded-parameter wait for function.</summary>
        public static Task<IJSHandle> WaitForFunctionAsync(
            this IPage page,
            string expression,
            object arg = default,
            float? timeout = default,
            object polling = default)
            => page.WaitForFunctionAsync(expression, arg, new PageWaitForFunctionOptions
            {
                Timeout = timeout,
                PollingInterval = ParsePollingInterval(polling),
            });

        /// <summary>Legacy expanded-parameter set content (timeout before waitUntil).</summary>
        public static Task SetContentAsync(
            this IPage page,
            string html,
            float? timeout,
            WaitUntilState waitUntil)
            => page.SetContentAsync(html, waitUntil, timeout);

        /// <summary>Legacy expanded-parameter wait for navigation.</summary>
        public static Task<IResponse> WaitForNavigationAsync(
            this IPage page,
            string urlString,
            Regex urlRegex,
            Func<string, bool> urlFunc,
            float? timeout,
            WaitUntilState waitUntil)
            => page.WaitForNavigationAsync(new PageWaitForNavigationOptions
            {
                Url = urlString,
                UrlRegex = urlRegex,
                UrlFunc = urlFunc,
                WaitUntil = waitUntil,
                Timeout = timeout,
            });

        /// <summary>Legacy expanded-parameter aria snapshot.</summary>
        public static Task<string> AriaSnapshotAsync(this IPage page, float? timeout = default)
            => page.AriaSnapshotAsync(new PageAriaSnapshotOptions { Timeout = timeout });

        /// <summary>Legacy expanded-parameter emulate media.</summary>
        public static Task EmulateMediaAsync(this IPage page, ColorScheme colorScheme)
            => page.EmulateMediaAsync(new PageEmulateMediaOptions { ColorScheme = colorScheme });

        /// <summary>Legacy expanded-parameter emulate media.</summary>
        public static Task EmulateMediaAsync(
            this IPage page,
            ColorScheme? colorScheme = default,
            Media? media = default,
            ReducedMotion? reducedMotion = default,
            ForcedColors? forcedColors = default,
            Contrast? contrast = default)
            => page.EmulateMediaAsync(new PageEmulateMediaOptions
            {
                ColorScheme = colorScheme,
                Media = media,
                ReducedMotion = reducedMotion,
                ForcedColors = forcedColors,
                Contrast = contrast,
            });

        /// <summary>Legacy getByText with exact flag.</summary>
        public static ILocator GetByText(this IPage page, string text, bool? exact = null)
            => page.GetByText(text, new PageGetByTextOptions { Exact = exact });

        /// <summary>Legacy getByLabel with exact flag.</summary>
        public static ILocator GetByLabel(this IPage page, string text, bool? exact = null)
            => page.GetByLabel(text, new PageGetByLabelOptions { Exact = exact });

        /// <summary>Legacy getByPlaceholder with exact flag.</summary>
        public static ILocator GetByPlaceholder(this IPage page, string text, bool? exact = null)
            => page.GetByPlaceholder(text, new PageGetByPlaceholderOptions { Exact = exact });

        /// <summary>Legacy getByAltText with exact flag.</summary>
        public static ILocator GetByAltText(this IPage page, string text, bool? exact = null)
            => page.GetByAltText(text, new PageGetByAltTextOptions { Exact = exact });

        /// <summary>Legacy getByTitle with exact flag.</summary>
        public static ILocator GetByTitle(this IPage page, string text, bool? exact = null)
            => page.GetByTitle(text, new PageGetByTitleOptions { Exact = exact });

        /// <summary>Legacy add init script with argument object.</summary>
        public static Task AddInitScriptAsync(this IPage page, string script, object arg)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.AddInitScriptAsync(script, arg);
                case Firefox.FirefoxPage firefox:
                    return firefox.AddInitScriptAsync(script, arg);
                case WebKit.WKPage webkit:
                    return webkit.AddInitScriptAsync(script, arg);
                default:
                    throw new NotSupportedException("This page does not support PlaywrightNative init-script arguments.");
            }
        }

        /// <summary>Legacy expanded-parameter route with times.</summary>
        public static Task RouteAsync(this IPage page, string url, Action<IRoute> handler, int? times = default)
            => RouteRegistrationCompat.RegisterPageRouteAsync(page, url, handler, times);

        /// <summary>Legacy expanded-parameter route with times.</summary>
        public static Task RouteAsync(this IPage page, string url, Func<IRoute, Task> handler, int? times = default)
            => RouteRegistrationCompat.RegisterPageRouteAsync(page, url, handler, times);

        /// <summary>Legacy unroute all with behavior.</summary>
        public static Task UnrouteAllAsync(this IPage page, UnrouteBehavior behavior = default)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.UnrouteAllAsync(behavior);
                case Firefox.FirefoxPage firefox:
                    return firefox.UnrouteAllAsync(behavior);
                case WebKit.WKPage webkit:
                    return webkit.UnrouteAllAsync(behavior);
                default:
                    return page.UnrouteAllAsync(new PageUnrouteAllOptions { Behavior = UnrouteBehaviorBridge.ToOfficial(behavior) });
            }
        }

        /// <summary>Bind a page-free <see cref="IBy"/> builder.</summary>
        public static ILocator Get(this IPage page, IBy by)
            => LocatorBy.Bind(page, by);

        /// <summary>Any-frame <see cref="IFrameLocator"/> search from the main frame.</summary>
        public static IFrameLocator FrameLocator(this IPage page)
            => new FrameLocator(page.MainFrame);

        /// <summary>Legacy async spelling of <see cref="IPage.GetByRole(AriaRole, PageGetByRoleOptions)"/>.</summary>
        public static Task<IElementHandle> GetByRoleAsync(
            this IPage page,
            string role,
            string name = null,
            bool? exact = null,
            float? timeout = null,
            bool? checkedState = null,
            bool? disabled = null,
            bool? expanded = null,
            bool? includeHidden = null,
            int? level = null,
            bool? pressed = null,
            bool? selected = null,
            string description = null,
            Regex descriptionRegex = null,
            Regex nameRegex = null)
        {
            if (page is Page chromium)
            {
                return chromium.GetByRoleAsync(role, name, exact, timeout, checkedState, disabled, expanded, includeHidden, level, pressed, selected, description, descriptionRegex, nameRegex);
            }

            if (page is Firefox.FirefoxPage firefox)
            {
                return firefox.GetByRoleAsync(role, name, exact, timeout, checkedState, disabled, expanded, includeHidden, level, pressed, selected, description, descriptionRegex, nameRegex);
            }

            if (page is WebKit.WKPage webkit)
            {
                return webkit.GetByRoleAsync(role, name, exact, timeout, checkedState, disabled, expanded, includeHidden, level, pressed, selected, description, descriptionRegex, nameRegex);
            }

            ILocator locator = GetByRole(
                page,
                role,
                name,
                exact,
                checkedState,
                disabled,
                expanded,
                includeHidden,
                level,
                pressed,
                selected,
                description,
                descriptionRegex,
                nameRegex);
            return locator.ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });
        }

        /// <summary>Legacy async spelling of label query returning an element handle.</summary>
        public static Task<IElementHandle> GetByLabelAsync(this IPage page, string text, bool? exact = null, float? timeout = null)
        {
            if (page is Page chromium)
            {
                return chromium.GetByLabelAsync(text, exact, timeout);
            }

            if (page is Firefox.FirefoxPage firefox)
            {
                return firefox.GetByLabelAsync(text, exact, timeout);
            }

            if (page is WebKit.WKPage webkit)
            {
                return webkit.GetByLabelAsync(text, exact, timeout);
            }

            return page.GetByLabel(text, new PageGetByLabelOptions { Exact = exact })
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });
        }

        /// <summary>Gets the default action timeout when supported.</summary>
        public static float DefaultTimeout(this IPage page)
            => page is IHasDefaultTimeouts timeouts
                ? timeouts.DefaultTimeout
                : 30_000;

        /// <summary>Gets the default navigation timeout when supported.</summary>
        public static float DefaultNavigationTimeout(this IPage page)
            => page is IHasDefaultTimeouts timeouts
                ? timeouts.DefaultNavigationTimeout
                : 30_000;

        /// <summary>Legacy spelling of <see cref="IPage.SetExtraHTTPHeadersAsync"/>.</summary>
        public static Task SetExtraHttpHeadersAsync(
            this IPage page,
            IEnumerable<KeyValuePair<string, string>> headers)
            => page.SetExtraHTTPHeadersAsync(headers);

        /// <summary>Legacy unroute with behavior.</summary>
        public static Task UnrouteAsync(this IPage page, string url, UnrouteBehavior behavior)
            => page switch
            {
                Page chromium => chromium.UnrouteAsync(url, behavior: behavior),
                Firefox.FirefoxPage firefox => firefox.UnrouteAsync(url, behavior: behavior),
                WebKit.WKPage webkit => webkit.UnrouteAsync(url, behavior: behavior),
                _ => page.UnrouteAsync(url),
            };

        /// <summary>PlaywrightNative coverage helper.</summary>
        public static ICoverage Coverage(this IPage page)
            => page is IHasPageExtras extras
                ? extras.Coverage
                : throw new NotSupportedException("This page does not expose PlaywrightNative coverage.");

        /// <summary>Legacy frame lookup by URL pattern.</summary>
        public static IFrame FrameByUrl(this IPage page, string urlString, Regex urlRegex, Func<string, bool> urlFunc)
            => PageCompatDispatch.FrameByUrl(page, urlString, urlRegex, urlFunc);

        /// <summary>Legacy remove-all-listeners helper.</summary>
        public static Task RemoveAllListenersAsync(
            this IPage page,
            string type = null,
            RemoveAllListenersBehavior behavior = default)
            => PageCompatDispatch.RemoveAllListenersAsync(page, type, behavior);

        /// <summary>Legacy remove-all-listeners with string behavior.</summary>
        public static Task RemoveAllListenersAsync(this IPage page, string type, string behavior)
            => page.RemoveAllListenersAsync(type, ParseRemoveAllListenersBehavior(behavior));

        /// <summary>Legacy vision-deficiency emulation.</summary>
        public static Task EmulateVisionDeficiencyAsync(this IPage page, VisionDeficiency type = default)
            => PageCompatDispatch.EmulateVisionDeficiencyAsync(page, type);

        /// <summary>Legacy async getByText returning an element handle.</summary>
        public static Task<IElementHandle> GetByTextAsync(this IPage page, string text, bool? exact = null, float? timeout = null)
            => PageCompatDispatch.GetByTextAsync(page, text, exact, timeout);

        /// <summary>Legacy async getByTestId returning an element handle.</summary>
        public static Task<IElementHandle> GetByTestIdAsync(this IPage page, string testId, float? timeout = null)
            => PageCompatDispatch.GetByTestIdAsync(page, testId, timeout);

        /// <summary>Legacy async getByPlaceholder returning an element handle.</summary>
        public static Task<IElementHandle> GetByPlaceholderAsync(this IPage page, string text, bool? exact = null, float? timeout = null)
            => PageCompatDispatch.GetByPlaceholderAsync(page, text, exact, timeout);

        /// <summary>Legacy async getByAltText returning an element handle.</summary>
        public static Task<IElementHandle> GetByAltTextAsync(this IPage page, string text, bool? exact = null, float? timeout = null)
            => PageCompatDispatch.GetByAltTextAsync(page, text, exact, timeout);

        /// <summary>Legacy async getByTitle returning an element handle.</summary>
        public static Task<IElementHandle> GetByTitleAsync(this IPage page, string text, bool? exact = null, float? timeout = null)
            => PageCompatDispatch.GetByTitleAsync(page, text, exact, timeout);

        /// <summary>Legacy add-locator-handler with times/noWaitAfter.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task AddLocatorHandlerAsync(
            this IPage page,
            ILocator locator,
            Func<Task> handler,
            int? times = default,
            bool? noWaitAfter = default)
            => page.AddLocatorHandlerAsync(locator, handler, new PageAddLocatorHandlerOptions
            {
                Times = times,
                NoWaitAfter = noWaitAfter,
            });

        /// <summary>Legacy add-locator-handler with times/noWaitAfter.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task AddLocatorHandlerAsync(
            this IPage page,
            ILocator locator,
            Func<ILocator, Task> handler,
            int? times = default,
            bool? noWaitAfter = default)
            => page.AddLocatorHandlerAsync(locator, handler, new PageAddLocatorHandlerOptions
            {
                Times = times,
                NoWaitAfter = noWaitAfter,
            });

        /// <summary>Internal accessibility tree used by aria snapshots and expect matchers.</summary>
        internal static Task<AccessibilitySnapshotResult> SnapshotAccessibilityAsync(
            this IPage page,
            bool? interestingOnly = null,
            IElementHandle root = null)
            => page is IHasPageExtras extras
                ? extras.SnapshotAccessibilityAsync(interestingOnly, root)
                : throw new NotSupportedException("This page does not expose PlaywrightNative accessibility snapshots.");

        private static WaitUntilState ParseWaitUntilState(string waitUntil)
        {
            if (string.IsNullOrEmpty(waitUntil))
            {
                return default;
            }

            if (string.Equals(waitUntil, "load", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.Load;
            }

            if (string.Equals(waitUntil, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.DOMContentLoaded;
            }

            if (string.Equals(waitUntil, "networkidle", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.NetworkIdle;
            }

            if (string.Equals(waitUntil, "commit", StringComparison.OrdinalIgnoreCase))
            {
                return WaitUntilState.Commit;
            }

            throw new PlaywrightNativeException($"Unknown waitUntil value: {waitUntil}");
        }

        private static RemoveAllListenersBehavior ParseRemoveAllListenersBehavior(string behavior)
        {
            if (string.IsNullOrEmpty(behavior))
            {
                return default;
            }

            if (string.Equals(behavior, "wait", StringComparison.OrdinalIgnoreCase))
            {
                return RemoveAllListenersBehavior.Wait;
            }

            if (string.Equals(behavior, "ignoreErrors", StringComparison.OrdinalIgnoreCase))
            {
                return RemoveAllListenersBehavior.IgnoreErrors;
            }

            if (string.Equals(behavior, "default", StringComparison.OrdinalIgnoreCase))
            {
                return RemoveAllListenersBehavior.Default;
            }

            throw new PlaywrightNativeException($"Unknown removeAllListeners behavior: {behavior}");
        }

        private static float? ParsePollingInterval(object polling)
        {
            if (polling == null)
            {
                return null;
            }

            if (polling is string pollingText)
            {
                if (string.Equals(pollingText, "raf", StringComparison.OrdinalIgnoreCase))
                {
                    return 0;
                }

                if (float.TryParse(pollingText, out float parsed))
                {
                    return parsed;
                }

                throw new PlaywrightNativeException($"Unknown polling value: {pollingText}");
            }

            if (polling is float floatValue)
            {
                return floatValue;
            }

            if (polling is double doubleValue)
            {
                return (float)doubleValue;
            }

            if (polling is int intValue)
            {
                return intValue;
            }

            throw new PlaywrightNativeException($"Unknown polling value: {polling}");
        }
    }
}
