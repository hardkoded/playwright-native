/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
