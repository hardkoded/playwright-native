/*
 * Copyright (c) Microsoft Corporation.
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
    /// System Chrome 148 device metrics reset launch
    /// <c>blink-settings</c> hover/pointer. Restore official desktop
    /// <c>matchMedia</c> values.
    /// </summary>
    internal static class DesktopHoverMedia
    {
        /// <summary>
        /// Official desktop <c>(hover: hover)</c> / <c>(pointer: fine)</c>.
        /// </summary>
        internal const string Script = @"(() => {
    const original = window.matchMedia.bind(window);
    const forced = {
        '(hover:hover)': true,
        '(hover:none)': false,
        '(any-hover:hover)': true,
        '(any-hover:none)': false,
        '(pointer:fine)': true,
        '(pointer:coarse)': false,
        '(pointer:none)': false,
        '(any-pointer:fine)': true,
        '(any-pointer:coarse)': false,
        '(any-pointer:none)': false,
    };
    window.matchMedia = query => {
        const mq = original(query);
        const key = String(query).toLowerCase().replace(/\s+/g, '');
        if (!Object.prototype.hasOwnProperty.call(forced, key)) {
            return mq;
        }
        const matches = forced[key];
        return {
            matches,
            media: mq.media,
            onchange: null,
            addListener() {},
            removeListener() {},
            addEventListener() {},
            removeEventListener() {},
            dispatchEvent() { return false; },
        };
    };
})()";
    }
}
