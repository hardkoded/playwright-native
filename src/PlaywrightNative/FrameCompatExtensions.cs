/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
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
    /// Legacy expanded-parameter helpers over official <see cref="IFrame"/>.
    /// </summary>
    public static class FrameCompatExtensions
    {
        /// <summary>Legacy expanded-parameter check.</summary>
        public static Task CheckAsync(
            this IFrame frame,
            string selector,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.CheckAsync(selector, new FrameCheckOptions
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
            this IFrame frame,
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
            => frame.ClickAsync(selector, new FrameClickOptions
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
            this IFrame frame,
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
            => frame.DblClickAsync(selector, new FrameDblClickOptions
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
            this IFrame frame,
            string selector,
            string value,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.FillAsync(selector, value, new FrameFillOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter focus.</summary>
        public static Task FocusAsync(
            this IFrame frame,
            string selector,
            float? timeout = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.FocusAsync(selector, new FrameFocusOptions
            {
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter hover.</summary>
        public static Task HoverAsync(
            this IFrame frame,
            string selector,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.HoverAsync(selector, new FrameHoverOptions
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
            this IFrame frame,
            string selector,
            string key,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.PressAsync(selector, key, new FramePressOptions
            {
                Delay = delay,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            IEnumerable<SelectOptionValue> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, values, new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            string values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            bool? strict = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, values, new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            IEnumerable<string> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, values, new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, Array.Empty<string>(), new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            IElementHandle values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, values, new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            IEnumerable<IElementHandle> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, values, new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IFrame frame,
            string selector,
            SelectOptionValue values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(frame.SelectOptionAsync(selector, values, new FrameSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
                Strict = strict,
            }));

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IFrame frame,
            string selector,
            IEnumerable<FilePayload> files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.SetInputFilesAsync(selector, files, new FrameSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IFrame frame,
            string selector,
            string files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default)
            => frame.SetInputFilesAsync(selector, files, new FrameSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IFrame frame,
            string selector,
            IEnumerable<string> files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default)
            => frame.SetInputFilesAsync(selector, files, new FrameSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IFrame frame,
            string selector,
            FilePayload files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? strict = default)
            => frame.SetInputFilesAsync(selector, files, new FrameSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter tap.</summary>
        public static Task TapAsync(
            this IFrame frame,
            string selector,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? noWaitAfter = default,
            bool? force = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.TapAsync(selector, new FrameTapOptions
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
            this IFrame frame,
            string selector,
            string text,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.TypeAsync(selector, text, new FrameTypeOptions
            {
                Delay = delay,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy expanded-parameter uncheck.</summary>
        public static Task UncheckAsync(
            this IFrame frame,
            string selector,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            bool? strict = default)
            => frame.UncheckAsync(selector, new FrameUncheckOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
                Strict = strict,
            });

        /// <summary>Legacy string-role <c>getByRole</c>.</summary>
        public static ILocator GetByRole(
            this IFrame frame,
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
            => frame.Locator(RoleSelector.Build(
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

        /// <summary>Legacy getByText with exact flag.</summary>
        public static ILocator GetByText(this IFrame frame, string text, bool? exact = null)
            => frame.GetByText(text, new FrameGetByTextOptions { Exact = exact });

        /// <summary>Legacy getByLabel with exact flag.</summary>
        public static ILocator GetByLabel(this IFrame frame, string text, bool? exact = null)
            => frame.GetByLabel(text, new FrameGetByLabelOptions { Exact = exact });

        /// <summary>Legacy getByPlaceholder with exact flag.</summary>
        public static ILocator GetByPlaceholder(this IFrame frame, string text, bool? exact = null)
            => frame.GetByPlaceholder(text, new FrameGetByPlaceholderOptions { Exact = exact });

        /// <summary>Legacy getByAltText with exact flag.</summary>
        public static ILocator GetByAltText(this IFrame frame, string text, bool? exact = null)
            => frame.GetByAltText(text, new FrameGetByAltTextOptions { Exact = exact });

        /// <summary>Legacy getByTitle with exact flag.</summary>
        public static ILocator GetByTitle(this IFrame frame, string text, bool? exact = null)
            => frame.GetByTitle(text, new FrameGetByTitleOptions { Exact = exact });

        /// <summary>Legacy expanded-parameter wait for selector.</summary>
        public static Task<IElementHandle> WaitForSelectorAsync(
            this IFrame frame,
            string selector,
            WaitForSelectorState state = default,
            float? timeout = default,
            bool? strict = default)
            => frame.WaitForSelectorAsync(selector, new FrameWaitForSelectorOptions
            {
                State = state == EnumCompat.UndefinedWaitForSelectorState ? null : state,
                Timeout = timeout,
                Strict = strict,
            });

        /// <summary>Legacy async getByRole returning an element handle.</summary>
        public static Task<IElementHandle> GetByRoleAsync(
            this IFrame frame,
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
            => GetByRole(
                    frame,
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
                    nameRegex)
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        /// <summary>Legacy async getByText returning an element handle.</summary>
        public static Task<IElementHandle> GetByTextAsync(this IFrame frame, string text, bool? exact = null, float? timeout = null)
            => PageCompatDispatch.GetByTextAsync(frame, text, exact, timeout);

        /// <summary>Legacy async getByTestId returning an element handle.</summary>
        public static Task<IElementHandle> GetByTestIdAsync(this IFrame frame, string testId, float? timeout = null)
            => PageCompatDispatch.GetByTestIdAsync(frame, testId, timeout);

        /// <summary>Legacy async getByLabel returning an element handle.</summary>
        public static Task<IElementHandle> GetByLabelAsync(this IFrame frame, string text, bool? exact = null, float? timeout = null)
            => frame.GetByLabel(text, new FrameGetByLabelOptions { Exact = exact })
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        /// <summary>Legacy select-text helper.</summary>
        public static async Task SelectTextAsync(
            this IFrame frame,
            string selector,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
        {
            IElementHandle handle = await frame.WaitForSelectorAsync(
                selector,
                WaitForSelectorState.Visible,
                timeout,
                strict).ConfigureAwait(false);
            if (handle == null)
            {
                throw new PlaywrightNativeException($"Failed to find element matching selector \"{selector}\"");
            }

            await handle.SelectTextAsync(timeout, force, scroll).ConfigureAwait(false);
        }

        /// <summary>Legacy evaluate with exposeFunctions.</summary>
        /// <typeparam name="T">Result type.</typeparam>
        public static Task<T> EvaluateExposingFunctionsAsync<T>(this IFrame frame, string expression, object arg = default)
            => EvaluateCallbacks.EvaluateTargetAsync<T>(frame, expression, arg, exposeFunctions: true);

        /// <summary>Legacy aria snapshot YAML.</summary>
        public static Task<string> AriaSnapshotAsync(this IFrame frame, AriaSnapshotMode mode = default, int? depth = default, bool? boxes = default)
            => frame.Locator("body").AriaSnapshotAsync(new LocatorAriaSnapshotOptions());

        /// <summary>Legacy aria snapshot scoped to a ref selector.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<string> AriaSnapshotAsync(this IFrame frame, string refSelector, float? timeout = default)
            => frame.Locator(refSelector).AriaSnapshotAsync(new LocatorAriaSnapshotOptions { Timeout = timeout });

        /// <summary>Legacy wait-for-navigation with URL string.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<IResponse> WaitForNavigationAsync(
            this IFrame frame,
            string url,
            FrameWaitForNavigationOptions options = default)
            => frame.WaitForNavigationAsync(new FrameWaitForNavigationOptions
            {
                Url = url,
                Timeout = options?.Timeout,
                WaitUntil = options?.WaitUntil,
            });

        /// <summary>Any-frame search from this frame.</summary>
        public static IFrameLocator FrameLocator(this IFrame frame)
            => new FrameLocator(frame);

        /// <summary>Legacy locator chaining on frames.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static ILocator Locator(this IFrame frame, ILocator selectorOrLocator)
            => selectorOrLocator;

        /// <summary>Legacy querySelector with strict flag.</summary>
        public static Task<IElementHandle> QuerySelectorAsync(this IFrame frame, string selector, bool strict)
            => frame.QuerySelectorAsync(selector, new FrameQuerySelectorOptions { Strict = strict });

        /// <summary>Legacy async getByRole with official enum role.</summary>
        public static Task<IElementHandle> GetByRoleAsync(this IFrame frame, AriaRole role, float? timeout = null)
            => frame.GetByRoleAsync(role.ToRoleString(), timeout: timeout);

        /// <summary>Legacy async getByAltText returning an element handle.</summary>
        public static Task<IElementHandle> GetByAltTextAsync(this IFrame frame, string text, bool? exact = null, float? timeout = null)
            => frame.GetByAltText(text, new FrameGetByAltTextOptions { Exact = exact })
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        /// <summary>Legacy async getByPlaceholder returning an element handle.</summary>
        public static Task<IElementHandle> GetByPlaceholderAsync(this IFrame frame, string text, bool? exact = null, float? timeout = null)
            => frame.GetByPlaceholder(text, new FrameGetByPlaceholderOptions { Exact = exact })
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        /// <summary>Legacy async getByTitle returning an element handle.</summary>
        public static Task<IElementHandle> GetByTitleAsync(this IFrame frame, string text, bool? exact = null, float? timeout = null)
            => frame.GetByTitle(text, new FrameGetByTitleOptions { Exact = exact })
                .ElementHandleAsync(new LocatorElementHandleOptions { Timeout = timeout });

        /// <summary>Legacy aria snapshot on a frame.</summary>
        public static Task<string> AriaSnapshotAsync(this IFrame frame, float? timeout = default)
            => frame.Locator("body").AriaSnapshotAsync(new LocatorAriaSnapshotOptions { Timeout = timeout });

        /// <summary>Legacy IBy binding.</summary>
        public static ILocator Get(this IFrame frame, IBy by)
            => LocatorBy.Bind(frame, by);
    }
}
