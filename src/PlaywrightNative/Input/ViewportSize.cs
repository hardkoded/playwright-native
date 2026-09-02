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

namespace PlaywrightNative.Input
{
    /// <summary>
    /// Simple width/height pair in CSS pixels.
    /// </summary>
    /// <param name="Width">The viewport width in CSS pixels.</param>
    /// <param name="Height">The viewport height in CSS pixels.</param>
    internal readonly record struct ViewportSize(int Width, int Height);
}
