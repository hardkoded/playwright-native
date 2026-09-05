/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
using System.Collections.Generic;
using System.Threading.Tasks;

using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Compat;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;
using static PlaywrightNative.Helpers.CompatCollections;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy expanded-parameter helpers over official <see cref="IElementHandle"/>.
    /// </summary>
    public static class ElementHandleCompatExtensions
    {
        /// <summary>Legacy expanded-parameter check.</summary>
        public static Task CheckAsync(
            this IElementHandle handle,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
            => handle.CheckAsync(new ElementHandleCheckOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
            });

        /// <summary>Legacy expanded-parameter click.</summary>
        public static Task ClickAsync(
            this IElementHandle handle,
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
            int? steps = default)
            => handle.ClickAsync(new ElementHandleClickOptions
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
                Steps = steps,
            });

        /// <summary>Legacy expanded-parameter double click.</summary>
        public static Task DblClickAsync(
            this IElementHandle handle,
            MouseButton button = default,
            float? delay = default,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default,
            int? steps = default)
            => handle.DblClickAsync(new ElementHandleDblClickOptions
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
                Steps = steps,
            });

        /// <summary>Legacy expanded-parameter fill.</summary>
        public static Task FillAsync(
            this IElementHandle handle,
            string value,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => handle.FillAsync(value, new ElementHandleFillOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            });

        /// <summary>Legacy expanded-parameter focus (scroll/timeout ignored on element handles).</summary>
        public static Task FocusAsync(
            this IElementHandle handle,
            float? timeout = default,
            ActionScroll scroll = default)
            => handle switch
            {
                ChromiumElementHandle chromium => chromium.FocusAsync(timeout, scroll),
                WKElementHandle webkit => webkit.FocusAsync(timeout, scroll),
                _ => handle.FocusAsync(),
            };

        /// <summary>Legacy expanded-parameter hover.</summary>
        public static Task HoverAsync(
            this IElementHandle handle,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
            => handle.HoverAsync(new ElementHandleHoverOptions
            {
                Position = position,
                Modifiers = modifiers,
                Force = force,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
            });

        /// <summary>Legacy expanded-parameter press.</summary>
        public static Task PressAsync(
            this IElementHandle handle,
            string key,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => handle.PressAsync(key, new LegacyElementHandlePressOptions
            {
                Delay = delay,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            });

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            IEnumerable<SelectOptionValue> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            string values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            IEnumerable<string> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            IElementHandle values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            IEnumerable<IElementHandle> values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            SelectOptionValue values,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            }));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            params string[] values)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions()));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            params SelectOptionValue[] values)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions()));

        /// <summary>Legacy expanded-parameter select option.</summary>
        public static Task<IReadOnlyCollection<string>> SelectOptionAsync(
            this IElementHandle handle,
            params IElementHandle[] values)
            => CompatCollections.AsCollectionAsync(handle.SelectOptionAsync(values, new ElementHandleSelectOptionOptions()));

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IElementHandle handle,
            IEnumerable<FilePayload> files,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => handle.SetInputFilesAsync(files, new LegacyElementHandleSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IElementHandle handle,
            string files,
            bool? noWaitAfter = default,
            float? timeout = default)
            => handle.SetInputFilesAsync(files, new ElementHandleSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IElementHandle handle,
            IEnumerable<string> files,
            bool? noWaitAfter = default,
            float? timeout = default)
            => handle.SetInputFilesAsync(files, new ElementHandleSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
            });

        /// <summary>Legacy expanded-parameter set input files.</summary>
        public static Task SetInputFilesAsync(
            this IElementHandle handle,
            FilePayload files,
            bool? noWaitAfter = default,
            float? timeout = default)
            => handle.SetInputFilesAsync(files, new ElementHandleSetInputFilesOptions
            {
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
            });

        /// <summary>Legacy expanded-parameter tap.</summary>
        public static Task TapAsync(
            this IElementHandle handle,
            Position position = default,
            IEnumerable<KeyboardModifier> modifiers = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
            => handle.TapAsync(new ElementHandleTapOptions
            {
                Position = position,
                Modifiers = modifiers,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
            });

        /// <summary>Legacy expanded-parameter type.</summary>
        public static Task TypeAsync(
            this IElementHandle handle,
            string text,
            float? delay = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default)
            => handle.TypeAsync(text, new LegacyElementHandleTypeOptions
            {
                Delay = delay,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Force = force,
            });

        /// <summary>Legacy expanded-parameter uncheck.</summary>
        public static Task UncheckAsync(
            this IElementHandle handle,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
            => handle.UncheckAsync(new ElementHandleUncheckOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
            });

        /// <summary>Legacy expanded-parameter set checked.</summary>
        public static Task SetCheckedAsync(
            this IElementHandle handle,
            bool checkedState,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
            => handle.SetCheckedAsync(checkedState, new ElementHandleSetCheckedOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
            });

        /// <summary>Legacy expanded-parameter input value.</summary>
        public static Task<string> InputValueAsync(this IElementHandle handle, float? timeout = default)
            => handle.InputValueAsync(new ElementHandleInputValueOptions { Timeout = timeout });

        /// <summary>Legacy expanded-parameter wait for element state.</summary>
        public static Task WaitForElementStateAsync(this IElementHandle handle, ElementState state, float? timeout = default)
            => handle.WaitForElementStateAsync(state, new ElementHandleWaitForElementStateOptions { Timeout = timeout });

        /// <summary>Legacy expanded-parameter screenshot.</summary>
        public static async Task<byte[]> ScreenshotAsync(
            this IElementHandle handle,
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
            string maskColor = default)
        {
            IFrame frame = await handle.OwnerFrameAsync().ConfigureAwait(false);
            IPage page = frame?.Page;
            if (page != null)
            {
                return await ElementScreenshot.CaptureAsync(
                    handle,
                    page,
                    path,
                    type,
                    quality,
                    omitBackground,
                    timeout,
                    scale,
                    animations,
                    caret,
                    style,
                    mask,
                    maskColor).ConfigureAwait(false);
            }

            return await handle.ScreenshotAsync(new ElementHandleScreenshotOptions
            {
                Path = path,
                Type = type == EnumCompat.UndefinedScreenshotType ? null : type,
                Quality = quality,
                OmitBackground = omitBackground,
                Timeout = timeout,
                Style = style,
                Mask = mask,
                MaskColor = maskColor,
            }).ConfigureAwait(false);
        }

        /// <summary>Legacy expanded-parameter dispatch event.</summary>
        public static Task DispatchEventAsync(this IElementHandle handle, string type, object eventInit = default, float? timeout = default)
            => ElementDispatchEventAction.RunAsync(handle, type, eventInit, timeout);

        /// <summary>Legacy expanded-parameter select text.</summary>
        public static Task SelectTextAsync(this IElementHandle handle, float? timeout = default, bool? force = default, ActionScroll scroll = default)
            => handle.SelectTextAsync(new ElementHandleSelectTextOptions
            {
                Timeout = timeout,
                Force = force,
            });

        /// <summary>Legacy element aria snapshot YAML.</summary>
        public static Task<string> AriaSnapshotAsync(this IElementHandle handle, AriaSnapshotMode mode = AriaSnapshotMode.Default, int? depth = default, bool? boxes = default)
        {
            bool renderBoxes = boxes ?? false;
            if (mode == AriaSnapshotMode.Ai)
            {
                return AriaSnapshotOfficialAi.CaptureYamlAsync(handle, depth, renderBoxes, string.Empty);
            }

            return AriaSnapshotOfficial.CaptureYamlAsync(handle, depth, renderBoxes);
        }

        /// <summary>Legacy element aria snapshot JSON.</summary>
        public static Task<string> AriaSnapshotJsonAsync(this IElementHandle handle, AriaSnapshotMode mode = AriaSnapshotMode.Default, int? depth = default, bool? boxes = default)
        {
            bool renderBoxes = boxes ?? false;
            if (mode == AriaSnapshotMode.Ai)
            {
                return AriaSnapshotOfficialAi.CaptureJsonAsync(handle, depth, renderBoxes, string.Empty);
            }

            return AriaSnapshotOfficial.CaptureJsonAsync(handle, depth, renderBoxes);
        }

        /// <summary>Legacy expanded-parameter wait for selector.</summary>
        public static Task<IElementHandle> WaitForSelectorAsync(
            this IElementHandle handle,
            string selector,
            WaitForSelectorState state = default,
            float? timeout = default,
            bool? strict = default)
            => handle.WaitForSelectorAsync(selector, new ElementHandleWaitForSelectorOptions
            {
                State = state == EnumCompat.UndefinedWaitForSelectorState ? null : state,
                Timeout = timeout,
                Strict = strict,
            });
    }
}
