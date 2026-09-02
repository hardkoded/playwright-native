/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
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
