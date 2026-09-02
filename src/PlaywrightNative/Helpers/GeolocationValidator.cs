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
