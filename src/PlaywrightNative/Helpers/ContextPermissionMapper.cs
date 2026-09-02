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

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Maps Playwright permission names to Chromium and WebKit protocol values.
    /// </summary>
    internal static class ContextPermissionMapper
    {
        /// <summary>
        /// Maps Playwright permission names to CDP <c>Browser.PermissionType</c>
        /// values. Unknown names throw the official
        /// <c>Unknown permission: …</c> error.
        /// </summary>
        /// <param name="permissions">Playwright permission names.</param>
        /// <param name="localNetworkFallback">
        /// When <see langword="true"/>, map <c>local-network-access</c> to
        /// <c>localNetworkAccess</c> only (older Chrome).
        /// </param>
        /// <returns>Protocol permission names.</returns>
        internal static string[] ToChromium(IEnumerable<string> permissions, bool localNetworkFallback = false)
        {
            if (permissions == null)
            {
                return Array.Empty<string>();
            }

            List<string> mapped = new List<string>();
            foreach (string permission in permissions)
            {
                if (string.IsNullOrEmpty(permission))
                {
                    continue;
                }

                switch (permission)
                {
                    case ContextPermissions.Geolocation:
                    case ContextPermissions.MIDI:
                    case ContextPermissions.Notifications:
                        mapped.Add(permission);
                        break;
                    case ContextPermissions.Camera:
                        mapped.Add("videoCapture");
                        break;
                    case ContextPermissions.Microphone:
                        mapped.Add("audioCapture");
                        break;
                    case ContextPermissions.MIDISysex:
                        mapped.Add("midiSysex");
                        break;
                    case ContextPermissions.BackgroundSync:
                        mapped.Add("backgroundSync");
                        break;
                    case ContextPermissions.AmbientLightSensor:
                    case ContextPermissions.Accelerometer:
                    case ContextPermissions.Gyroscope:
                    case ContextPermissions.Magnetometer:
                        mapped.Add("sensors");
                        break;
                    case ContextPermissions.AccessibilityEvents:
                        mapped.Add("accessibilityEvents");
                        break;
                    case ContextPermissions.ClipboardRead:
                        mapped.Add("clipboardReadWrite");
                        break;
                    case ContextPermissions.ClipboardWrite:
                        mapped.Add("clipboardSanitizedWrite");
                        break;
                    case ContextPermissions.PaymentHandler:
                        mapped.Add("paymentHandler");
                        break;
                    case ContextPermissions.StorageAccess:
                        mapped.Add("storageAccess");
                        break;
                    case ContextPermissions.LocalFonts:
                        mapped.Add("localFonts");
                        break;
                    case ContextPermissions.LocalNetworkAccess:
                        mapped.Add("localNetworkAccess");
                        if (!localNetworkFallback)
                        {
                            mapped.Add("localNetwork");
                            mapped.Add("loopbackNetwork");
                        }

                        break;
                    case ContextPermissions.ScreenWakeLock:
                        mapped.Add("wakeLockScreen");
                        break;
                    default:
                        throw new PlaywrightNativeException("Unknown permission: " + permission);
                }
            }

            return mapped.ToArray();
        }

        /// <summary>
        /// Maps Playwright permission names to WebKit
        /// <c>Emulation.grantPermissions</c> values. Unknown names throw the
        /// official <c>Unknown permission: …</c> error.
        /// </summary>
        /// <param name="permissions">Playwright permission names.</param>
        /// <returns>Protocol permission names supported by this WebKit build.</returns>
        internal static string[] ToWebKit(IEnumerable<string> permissions)
        {
            if (permissions == null)
            {
                return Array.Empty<string>();
            }

            List<string> mapped = new List<string>();
            foreach (string permission in permissions)
            {
                if (string.IsNullOrEmpty(permission))
                {
                    continue;
                }

                switch (permission)
                {
                    case ContextPermissions.Geolocation:
                    case ContextPermissions.Notifications:
                    case ContextPermissions.ClipboardRead:
                    case ContextPermissions.ScreenWakeLock:
                    case ContextPermissions.Camera:
                    case ContextPermissions.Microphone:
                        mapped.Add(permission);
                        break;
                    case ContextPermissions.ClipboardWrite:
                        // Official WebKit has no clipboard-write mapping.
                        // Leftover keyboard tests grant it alongside clipboard-read.
                        break;
                    default:
                        throw new PlaywrightNativeException("Unknown permission: " + permission);
                }
            }

            return mapped.ToArray();
        }
    }
}
