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
using System.Globalization;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Parses Playwright-style PDF paper dimensions into inches.
    /// </summary>
    internal static class PdfPaperSize
    {
        /// <summary>
        /// Converts a CSS-like length (<c>8.5in</c>, <c>210mm</c>, <c>800px</c>, or a
        /// unitless pixel value) to inches. Returns <see langword="null"/> when
        /// <paramref name="value"/> is empty.
        /// </summary>
        /// <param name="value">The paper dimension.</param>
        /// <returns>Inches, or <see langword="null"/> when unset.</returns>
        internal static double? ToInches(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            string text = value.Trim();
            double pixelsPerUnit = 1;
            if (text.Length >= 2)
            {
                string unit = text.Substring(text.Length - 2).ToUpperInvariant();
                if (unit == "IN")
                {
                    pixelsPerUnit = 96;
                    text = text.Substring(0, text.Length - 2);
                }
                else if (unit == "CM")
                {
                    pixelsPerUnit = 37.8;
                    text = text.Substring(0, text.Length - 2);
                }
                else if (unit == "MM")
                {
                    pixelsPerUnit = 3.78;
                    text = text.Substring(0, text.Length - 2);
                }
                else if (unit == "PX")
                {
                    pixelsPerUnit = 1;
                    text = text.Substring(0, text.Length - 2);
                }
            }

            if (!double.TryParse(text.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double number))
            {
                throw new PlaywrightNativeException($"Cannot parse PDF paper size '{value}'.");
            }

            return number * pixelsPerUnit / 96.0;
        }

        /// <summary>
        /// Maps a named paper format (Letter, A4, …) to width and height in inches.
        /// </summary>
        /// <param name="format">The format name.</param>
        /// <returns>Width and height in inches.</returns>
        internal static (double Width, double Height) FormatToInches(string format)
        {
            if (string.IsNullOrWhiteSpace(format))
            {
                throw new ArgumentException("Format must not be empty.", nameof(format));
            }

            return format.Trim().ToUpperInvariant() switch
            {
                "LETTER" => (8.5, 11),
                "LEGAL" => (8.5, 14),
                "TABLOID" => (11, 17),
                "LEDGER" => (17, 11),
                "A0" => (33.1, 46.8),
                "A1" => (23.4, 33.1),
                "A2" => (16.54, 23.4),
                "A3" => (11.7, 16.54),
                "A4" => (8.27, 11.7),
                "A5" => (5.83, 8.27),
                "A6" => (4.13, 5.83),
                _ => throw new PlaywrightNativeException($"Unknown paper format: '{format}'."),
            };
        }
    }
}
