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
using System.Collections.Generic;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy <see cref="IJSHandle"/> helpers.
    /// </summary>
    public static class JSHandleCompatExtensions
    {
        /// <summary>Legacy property spelling of <see cref="IJSHandle.AsElement"/>.</summary>
        public static IElementHandle AsElement(this IJSHandle handle)
            => handle?.AsElement();

        /// <summary>Legacy evaluate with exposeFunctions.</summary>
        /// <typeparam name="T">Result type.</typeparam>
        public static Task<T> EvaluateExposingFunctionsAsync<T>(this IJSHandle handle, string expression, object arg = default)
            => EvaluateCallbacks.EvaluateTargetAsync<T>(handle, expression, arg, exposeFunctions: true);

        /// <summary>Legacy property spelling.</summary>
        public static Task<IJSHandle> PropertyAsync(this IJSHandle handle, string propertyName)
            => handle.GetPropertyAsync(propertyName);

        /// <summary>Legacy properties spelling.</summary>
        public static Task<Dictionary<string, IJSHandle>> PropertiesAsync(this IJSHandle handle)
            => handle.GetPropertiesAsync();

        /// <summary>Legacy JSON alias.</summary>
        /// <typeparam name="T">JSON result type.</typeparam>
        public static Task<T> JsonAsync<T>(this IJSHandle handle)
        {
            switch (handle)
            {
                case ChromiumJSHandle chromium:
                    return chromium.JsonValueAsync<T>();
                case FFJSHandle firefox:
                    return firefox.JsonValueAsync<T>();
                case WKJSHandle webkit:
                    return webkit.JsonValueAsync<T>();
                default:
                    return handle.EvaluateAsync<T>("object => object");
            }
        }
    }
}
