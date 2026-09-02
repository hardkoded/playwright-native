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
using System.Globalization;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy numeric helpers over official <see cref="IFormData"/>.
    /// </summary>
    public static class FormDataCompatExtensions
    {
        /// <summary>Legacy set with long value.</summary>
        public static IFormData Set(this IFormData form, string name, long value)
            => form is FormData sharp
                ? sharp.Set(name, value)
                : form.Set(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy set with double value.</summary>
        public static IFormData Set(this IFormData form, string name, double value)
            => form is FormData sharp
                ? sharp.Set(name, value)
                : form.Set(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy set with decimal value.</summary>
        public static IFormData Set(this IFormData form, string name, decimal value)
            => form is FormData sharp
                ? sharp.Set(name, value)
                : form.Set(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy set with float value.</summary>
        public static IFormData Set(this IFormData form, string name, float value)
            => form is FormData sharp
                ? sharp.Set(name, value)
                : form.Set(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy append with long value.</summary>
        public static IFormData Append(this IFormData form, string name, long value)
            => form is FormData sharp
                ? sharp.Append(name, value)
                : form.Append(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy append with double value.</summary>
        public static IFormData Append(this IFormData form, string name, double value)
            => form is FormData sharp
                ? sharp.Append(name, value)
                : form.Append(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy append with decimal value.</summary>
        public static IFormData Append(this IFormData form, string name, decimal value)
            => form is FormData sharp
                ? sharp.Append(name, value)
                : form.Append(name, value.ToString(CultureInfo.InvariantCulture));

        /// <summary>Legacy append with float value.</summary>
        public static IFormData Append(this IFormData form, string name, float value)
            => form is FormData sharp
                ? sharp.Append(name, value)
                : form.Append(name, value.ToString(CultureInfo.InvariantCulture));
    }
}
