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
using System.Text.RegularExpressions;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy expanded-parameter helpers over official <see cref="IFrameLocator"/>.
    /// </summary>
    public static class FrameLocatorCompatExtensions
    {
        /// <summary>Legacy string-role <c>getByRole</c>.</summary>
        public static ILocator GetByRole(
            this IFrameLocator frameLocator,
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
            => frameLocator.Locator(RoleSelector.Build(
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
        public static ILocator GetByText(this IFrameLocator frameLocator, string text, bool? exact = null)
            => frameLocator.GetByText(text, new FrameLocatorGetByTextOptions { Exact = exact });

        /// <summary>Legacy getByLabel with exact flag.</summary>
        public static ILocator GetByLabel(this IFrameLocator frameLocator, string text, bool? exact = null)
            => frameLocator.GetByLabel(text, new FrameLocatorGetByLabelOptions { Exact = exact });

        /// <summary>Legacy getByPlaceholder with exact flag.</summary>
        public static ILocator GetByPlaceholder(this IFrameLocator frameLocator, string text, bool? exact = null)
            => frameLocator.GetByPlaceholder(text, new FrameLocatorGetByPlaceholderOptions { Exact = exact });

        /// <summary>Legacy getByAltText with exact flag.</summary>
        public static ILocator GetByAltText(this IFrameLocator frameLocator, string text, bool? exact = null)
            => frameLocator.GetByAltText(text, new FrameLocatorGetByAltTextOptions { Exact = exact });

        /// <summary>Legacy getByTitle with exact flag.</summary>
        public static ILocator GetByTitle(this IFrameLocator frameLocator, string text, bool? exact = null)
            => frameLocator.GetByTitle(text, new FrameLocatorGetByTitleOptions { Exact = exact });

        /// <summary>Legacy IBy binding.</summary>
        public static ILocator Get(this IFrameLocator frameLocator, IBy by)
            => LocatorBy.Bind(frameLocator, by);
    }
}
