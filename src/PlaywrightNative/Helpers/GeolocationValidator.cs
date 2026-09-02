/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official Playwright geolocation option checks.
    /// </summary>
    internal static class GeolocationValidator
    {
        /// <summary>
        /// Throws the official protocol error when <paramref name="geolocation"/>
        /// omits latitude/longitude or is out of range. <see langword="null"/>
        /// clears the override.
        /// </summary>
        /// <param name="geolocation">The override, or <see langword="null"/>.</param>
        internal static void Validate(Geolocation geolocation)
        {
            if (geolocation == null)
            {
                return;
            }

            if (geolocation.Longitude < -180 || geolocation.Longitude > 180)
            {
                throw new PlaywrightNativeException(
                    "geolocation.longitude: precondition -180 <= LONGITUDE <= 180 failed.");
            }

            if (geolocation.Latitude < -90 || geolocation.Latitude > 90)
            {
                throw new PlaywrightNativeException(
                    "geolocation.latitude: precondition -90 <= LATITUDE <= 90 failed.");
            }
        }
    }
}
