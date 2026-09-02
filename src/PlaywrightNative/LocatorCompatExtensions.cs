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
#pragma warning disable CA1062
using System;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy PlaywrightNative <see cref="ILocator"/> helpers missing from official API.
    /// </summary>
    public static class LocatorCompatExtensions
    {
        /// <summary>Gets the owning frame for a PlaywrightNative locator.</summary>
        public static IFrame Frame(this ILocator locator) => RequireLocator(locator).Frame;

        /// <summary>Legacy highlight with style dictionary.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task HighlightAsync(this ILocator locator, System.Collections.Generic.IReadOnlyDictionary<string, string> style, float? timeout = default)
            => locator is Locator sharp
                ? sharp.HighlightAsync(style, timeout)
                : locator.HighlightAsync(new LocatorHighlightOptions());

        /// <summary>Legacy highlight with style named parameter.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task HighlightAsync(this ILocator locator, float? timeout = default, string style = default)
            => RequireLocator(locator).HighlightAsync(timeout, style);

        /// <summary>Legacy wait-for-function returning a handle.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(2)]
        public static Task<IJSHandle> WaitForFunctionAsync(
            this ILocator locator,
            string expression,
            object arg = default)
            => locator.WaitForFunctionAsync(expression, arg, pollingInterval: default, timeout: default);

        /// <summary>Legacy wait-for-function returning a handle.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static async Task<IJSHandle> WaitForFunctionAsync(
            this ILocator locator,
            string expression,
            object arg = default,
            float? pollingInterval = default,
            float? timeout = default)
        {
            if (locator is Locator sharp)
            {
                return await sharp.WaitForFunctionAsync(expression, arg, pollingInterval, timeout).ConfigureAwait(false);
            }

            await locator.WaitForFunctionAsync(expression, arg, new LocatorWaitForFunctionOptions
            {
                Timeout = timeout,
            }).ConfigureAwait(false);
            return null;
        }

        /// <summary>Legacy evaluate with timeout as third positional argument.</summary>
        /// <typeparam name="T">Result type.</typeparam>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task<T> EvaluateAsync<T>(
            this ILocator locator,
            string expression,
            object arg,
            float timeout)
            => locator.EvaluateAsync<T>(expression, arg, new LocatorEvaluateOptions { Timeout = timeout });

        /// <summary>Legacy highlight with style string.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task HighlightAsync(this ILocator locator, string style = default)
            => RequireLocator(locator).HighlightAsync(style);

        /// <summary>Legacy <c>has</c> filter.</summary>
        public static ILocator Has(this ILocator locator, ILocator has)
            => RequireLocator(locator).Has(has);

        /// <summary>Legacy <c>hasNot</c> filter.</summary>
        public static ILocator HasNot(this ILocator locator, ILocator hasNot)
            => RequireLocator(locator).HasNot(hasNot);

        /// <summary>Legacy <c>hasNotText</c> filter.</summary>
        public static ILocator HasNotText(this ILocator locator, string hasNotText)
            => RequireLocator(locator).HasNotText(hasNotText);

        /// <summary>Legacy <c>hasNotText</c> filter.</summary>
        public static ILocator HasNotText(this ILocator locator, Regex hasNotText)
            => RequireLocator(locator).HasNotText(hasNotText);

        /// <summary>Legacy string-role <c>getByRole</c>.</summary>
        public static ILocator GetByRole(
            this ILocator locator,
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
            => RequireLocator(locator).GetByRole(
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

        /// <summary>Legacy text filter.</summary>
        public static ILocator Filter(this ILocator locator, string hasText)
            => RequireLocator(locator).Filter(hasText);

        /// <summary>Legacy regex text filter.</summary>
        public static ILocator Filter(this ILocator locator, Regex hasText)
            => RequireLocator(locator).Filter(hasText);

        /// <summary>Legacy visibility filter.</summary>
        public static ILocator Filter(this ILocator locator, bool visible)
            => RequireLocator(locator).Filter(visible);

        /// <summary>Legacy has filter.</summary>
        public static ILocator Filter(this ILocator locator, ILocator has)
            => RequireLocator(locator).Has(has);

        /// <summary>Legacy drag-to with steps.</summary>
        public static Task DragToAsync(
            this ILocator locator,
            ILocator target,
            int? steps = default,
            float? timeout = default,
            bool? force = default,
            bool? trial = default)
            => locator.DragToAsync(target, new LocatorDragToOptions
            {
                Steps = steps,
                Timeout = timeout,
                Force = force,
                Trial = trial,
            });

        /// <summary>Legacy dispatch-event with strict flag.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static Task DispatchEventAsync(
            this ILocator locator,
            string type,
            object eventInit = default,
            bool? strict = default,
            float? timeout = default)
            => RequireLocator(locator).DispatchEventAsync(type, eventInit, strict, timeout);

        /// <summary>Legacy click with button named parameter.</summary>
        public static Task ClickAsync(
            this ILocator locator,
            MouseButton button,
            float? timeout = default,
            bool? force = default,
            bool? trial = default)
            => locator.ClickAsync(new LocatorClickOptions
            {
                Button = button,
                Timeout = timeout,
                Force = force,
                Trial = trial,
            });

        /// <summary>Legacy getByText with exact flag.</summary>
        public static ILocator GetByText(this ILocator locator, string text, bool? exact = null)
            => RequireLocator(locator).GetByText(text, exact);

        /// <summary>Legacy getByLabel with exact flag.</summary>
        public static ILocator GetByLabel(this ILocator locator, string text, bool? exact = null)
            => RequireLocator(locator).GetByLabel(text, exact);

        /// <summary>Legacy getByPlaceholder with exact flag.</summary>
        public static ILocator GetByPlaceholder(this ILocator locator, string text, bool? exact = null)
            => RequireLocator(locator).GetByPlaceholder(text, exact);

        /// <summary>Legacy getByAltText with exact flag.</summary>
        public static ILocator GetByAltText(this ILocator locator, string text, bool? exact = null)
            => RequireLocator(locator).GetByAltText(text, exact);

        /// <summary>Legacy getByTitle with exact flag.</summary>
        public static ILocator GetByTitle(this ILocator locator, string text, bool? exact = null)
            => RequireLocator(locator).GetByTitle(text, exact);

        /// <summary>Legacy expanded-parameter element handle.</summary>
        public static Task<IElementHandle> ElementHandleAsync(this ILocator locator, float? timeout = default)
            => RequireLocator(locator).ElementHandleAsync(timeout);

        /// <summary>Legacy evaluate with exposeFunctions.</summary>
        /// <typeparam name="T">Result type.</typeparam>
        public static Task<T> EvaluateExposingFunctionsAsync<T>(this ILocator locator, string expression, object arg = default)
            => EvaluateCallbacks.EvaluateTargetAsync<T>(locator, expression, arg, exposeFunctions: true);

        /// <summary>Legacy evaluate handle with exposeFunctions.</summary>
        public static Task<IJSHandle> EvaluateHandleExposingFunctionsAsync(this ILocator locator, string expression, object arg = default)
            => EvaluateCallbacks.EvaluateHandleTargetAsync(locator, expression, arg, exposeFunctions: true);

        /// <summary>Legacy set-checked with expanded parameters.</summary>
        public static Task SetCheckedAsync(
            this ILocator locator,
            bool checkedState,
            Position position = default,
            bool? force = default,
            bool? noWaitAfter = default,
            float? timeout = default,
            bool? trial = default,
            ActionScroll scroll = default)
            => locator.SetCheckedAsync(checkedState, new LocatorSetCheckedOptions
            {
                Position = position,
                Force = force,
                NoWaitAfter = noWaitAfter,
                Timeout = timeout,
                Trial = trial,
                Scroll = ActionScrollBridge.ToScrollOption(scroll),
            });

        /// <summary>Legacy IBy binding.</summary>
        public static ILocator Get(this ILocator locator, IBy by)
            => LocatorBy.Bind(RequireLocator(locator), by);

        private static Locator RequireLocator(ILocator locator)
            => locator as Locator
                ?? throw new NotSupportedException("This locator does not support PlaywrightNative extensions.");
    }
}
