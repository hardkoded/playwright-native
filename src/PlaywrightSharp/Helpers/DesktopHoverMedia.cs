/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp.Helpers
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
