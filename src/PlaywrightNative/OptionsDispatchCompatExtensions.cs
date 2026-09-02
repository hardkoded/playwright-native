/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Compat;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Dispatches compat option bags that carry PlaywrightNative-only fields.
    /// </summary>
    public static class OptionsDispatchCompatExtensions
    {
        /// <summary>Legacy click with PlaywrightNative steps option.</summary>
        [OverloadResolutionPriority(1)]
        public static Task ClickAsync(this IPage page, string selector, LegacyPageClickOptions options)
            => page.ClickAsync(selector, new Microsoft.Playwright.PageClickOptions
            {
                Button = options?.Button,
                ClickCount = options?.ClickCount,
                Delay = options?.Delay,
                Force = options?.Force,
                Modifiers = options?.Modifiers,
                NoWaitAfter = options?.NoWaitAfter,
                Position = options?.Position,
                Strict = options?.Strict,
                Timeout = options?.Timeout,
                Trial = options?.Trial,
            });

        /// <summary>Legacy frame click with PlaywrightNative steps option.</summary>
        [OverloadResolutionPriority(1)]
        public static Task ClickAsync(this IFrame frame, string selector, LegacyFrameClickOptions options)
            => frame.ClickAsync(selector, new Microsoft.Playwright.FrameClickOptions
            {
                Button = options?.Button,
                ClickCount = options?.ClickCount,
                Delay = options?.Delay,
                Force = options?.Force,
                Modifiers = options?.Modifiers,
                NoWaitAfter = options?.NoWaitAfter,
                Position = options?.Position,
                Strict = options?.Strict,
                Timeout = options?.Timeout,
                Trial = options?.Trial,
            });

        /// <summary>Legacy focus with scroll option.</summary>
        [OverloadResolutionPriority(1)]
        public static Task FocusAsync(this IPage page, string selector, LegacyPageFocusOptions options)
            => page.FocusAsync(selector, new Microsoft.Playwright.PageFocusOptions
            {
                Strict = options?.Strict,
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy frame focus with scroll option.</summary>
        [OverloadResolutionPriority(1)]
        public static Task FocusAsync(this IFrame frame, string selector, LegacyFrameFocusOptions options)
            => frame.FocusAsync(selector, new Microsoft.Playwright.FrameFocusOptions
            {
                Strict = options?.Strict,
                Timeout = options?.Timeout,
            });

        /// <summary>Legacy wait-for-load-state options bag with embedded state.</summary>
        [OverloadResolutionPriority(1)]
        public static Task WaitForLoadStateAsync(this IPage page, LegacyPageWaitForLoadStateOptions options)
        {
            if (options?.State is string stateText)
            {
                if (string.Equals(stateText, "load", StringComparison.OrdinalIgnoreCase))
                {
                    return page.WaitForLoadStateAsync(LoadState.Load, options);
                }

                if (string.Equals(stateText, "domcontentloaded", StringComparison.OrdinalIgnoreCase))
                {
                    return page.WaitForLoadStateAsync(LoadState.DOMContentLoaded, options);
                }

                if (string.Equals(stateText, "networkidle", StringComparison.OrdinalIgnoreCase))
                {
                    return page.WaitForLoadStateAsync(LoadState.NetworkIdle, options);
                }

                throw new PlaywrightNativeException("state: expected one of (load|loadstate|domcontentloaded|networkidle|commit)");
            }

            if (options?.State is LoadState loadState)
            {
                return page.WaitForLoadStateAsync(loadState, options);
            }

            return page.WaitForLoadStateAsync(options);
        }

        /// <summary>Legacy wait-for-load-state options bag on frames.</summary>
        [OverloadResolutionPriority(1)]
        public static Task WaitForLoadStateAsync(this IFrame frame, LegacyPageWaitForLoadStateOptions options)
            => frame.WaitForLoadStateAsync(
                options?.State is LoadState loadState ? loadState : default,
                new FrameWaitForLoadStateOptions { Timeout = options?.Timeout });

        /// <summary>Legacy element-handle press with force.</summary>
        [OverloadResolutionPriority(1)]
        public static Task PressAsync(this IElementHandle handle, string key, LegacyElementHandlePressOptions options)
        {
            if (options?.Force != null)
            {
                return handle.PressAsync(key, options.Delay, options.NoWaitAfter, options.Timeout, force: options.Force);
            }

            return handle.PressAsync(key, options);
        }

        /// <summary>Legacy element-handle type with force.</summary>
        [OverloadResolutionPriority(1)]
        public static Task TypeAsync(this IElementHandle handle, string text, LegacyElementHandleTypeOptions options)
        {
            if (options?.Force != null)
            {
                return handle.TypeAsync(text, options.Delay, options.NoWaitAfter, options.Timeout, force: options.Force);
            }

            return handle.TypeAsync(text, options);
        }

        /// <summary>Legacy element-handle set-input-files with force.</summary>
        [OverloadResolutionPriority(1)]
        public static Task SetInputFilesAsync(
            this IElementHandle handle,
            string files,
            LegacyElementHandleSetInputFilesOptions options)
        {
            if (options?.Force != null)
            {
                return handle.SetInputFilesAsync(files, options.NoWaitAfter, options.Timeout);
            }

            return handle.SetInputFilesAsync(files, options);
        }

        /// <summary>Legacy wait-for-selector options bag.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IElementHandle> WaitForSelectorAsync(
            this IPage page,
            string selector,
            LegacyPageWaitForSelectorOptions options)
            => page.WaitForSelectorAsync(selector, (Microsoft.Playwright.PageWaitForSelectorOptions)options);

        /// <summary>Legacy frame wait-for-selector options bag.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IElementHandle> WaitForSelectorAsync(
            this IFrame frame,
            string selector,
            LegacyFrameWaitForSelectorOptions options)
            => frame.WaitForSelectorAsync(selector, (Microsoft.Playwright.FrameWaitForSelectorOptions)options);

        /// <summary>Legacy wait-for-file-chooser options bag.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IFileChooser> WaitForFileChooserAsync(
            this IPage page,
            LegacyPageWaitForFileChooserOptions options)
            => page.WaitForFileChooserAsync(options);

        /// <summary>Legacy locator wait-for-function options bag.</summary>
        [OverloadResolutionPriority(1)]
        public static Task<IJSHandle> WaitForFunctionAsync(
            this ILocator locator,
            string expression,
            LegacyLocatorWaitForFunctionOptions options)
            => locator is Locator concrete
                ? concrete.WaitForFunctionAsync(expression, options?.Arg, pollingInterval: null, timeout: options?.Timeout)
                : throw new NotSupportedException("WaitForFunctionAsync requires a PlaywrightNative locator.");

        /// <summary>Legacy locator click with PlaywrightNative signal/steps options.</summary>
        [OverloadResolutionPriority(1)]
        public static Task ClickAsync(this ILocator locator, LegacyLocatorClickOptions options)
            => locator.ClickAsync(new Microsoft.Playwright.LocatorClickOptions
            {
                Button = options?.Button,
                ClickCount = options?.ClickCount,
                Delay = options?.Delay,
                Force = options?.Force,
                Modifiers = options?.Modifiers,
                NoWaitAfter = options?.NoWaitAfter,
                Position = options?.Position,
                Steps = options?.Steps,
                Timeout = options?.Timeout,
                Trial = options?.Trial,
                Scroll = ActionScrollBridge.ToScrollOption(options == null ? default : options.Scroll),
            });

        /// <summary>Legacy locator hover with scroll option.</summary>
        [OverloadResolutionPriority(1)]
        public static Task HoverAsync(this ILocator locator, LegacyLocatorHoverOptions options)
            => locator.HoverAsync(new Microsoft.Playwright.LocatorHoverOptions
            {
                Force = options?.Force,
                Modifiers = options?.Modifiers,
                NoWaitAfter = options?.NoWaitAfter,
                Position = options?.Position,
                Timeout = options?.Timeout,
                Trial = options?.Trial,
                Scroll = ActionScrollBridge.ToScrollOption(options == null ? default : options.Scroll),
            });

        /// <summary>Legacy page drag-and-drop with scroll/steps options.</summary>
        [OverloadResolutionPriority(1)]
        public static Task DragAndDropAsync(
            this IPage page,
            string source,
            string target,
            LegacyPageDragAndDropOptions options)
            => page.DragAndDropAsync(source, target, new Microsoft.Playwright.PageDragAndDropOptions
            {
                Force = options?.Force,
                NoWaitAfter = options?.NoWaitAfter,
                SourcePosition = options?.SourcePosition,
                Steps = options?.Steps,
                TargetPosition = options?.TargetPosition,
                Timeout = options?.Timeout,
                Trial = options?.Trial,
                Scroll = options?.Scroll,
            });
    }
}
