/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using System.Text.Json;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Shared JS for <c>IElementHandle.DispatchEventAsync</c>.
    /// </summary>
    internal static class DispatchEventScript
    {
        /// <summary>
        /// Official error when a JSHandle is evaluated in another frame's world.
        /// </summary>
        internal const string DifferentContextMessage =
            "JSHandles can be evaluated only in the context they were created!";

        /// <summary>
        /// JavaScript that creates and dispatches a DOM event from <c>type</c> + <c>eventInit</c>.
        /// Expects locals <c>el</c>, <c>type</c>, and <c>eventInit</c>.
        /// </summary>
        internal const string DispatchBody = @"
    let event;
    if (type === 'click' || type === 'dblclick' || type === 'contextmenu' || type === 'auxclick' || type.startsWith('mouse')) {
        event = new MouseEvent(type, eventInit);
    } else if (type.startsWith('key')) {
        event = new KeyboardEvent(type, eventInit);
    } else if (type.startsWith('pointer')) {
        event = new PointerEvent(type, eventInit);
    } else if (type === 'touchstart' || type === 'touchend' || type === 'touchmove' || type === 'touchcancel' || type.startsWith('touch')) {
        eventInit.target = eventInit.target || el;
        if (typeof document.createTouch === 'function') {
            const createTouch = (t) => {
                if (typeof Touch !== 'undefined' && t instanceof Touch)
                    return t;
                let pageX = t.pageX;
                if (pageX === undefined && t.clientX !== undefined)
                    pageX = t.clientX + ((document.scrollingElement && document.scrollingElement.scrollLeft) || 0);
                let pageY = t.pageY;
                if (pageY === undefined && t.clientY !== undefined)
                    pageY = t.clientY + ((document.scrollingElement && document.scrollingElement.scrollTop) || 0);
                return document.createTouch(window, t.target || el, t.identifier, pageX, pageY, t.screenX, t.screenY, t.radiusX, t.radiusY, t.rotationAngle, t.force);
            };
            const createTouchList = (touches) => {
                if ((typeof TouchList !== 'undefined' && touches instanceof TouchList) || !touches)
                    return touches;
                return document.createTouchList.apply(document, touches.map(createTouch));
            };
            eventInit.touches = createTouchList(eventInit.touches);
            eventInit.targetTouches = createTouchList(eventInit.targetTouches);
            eventInit.changedTouches = createTouchList(eventInit.changedTouches);
        } else {
            const asTouch = (t) => (typeof Touch !== 'undefined' && t instanceof Touch) ? t : new Touch(Object.assign({}, t, { target: t.target || el }));
            if (eventInit.touches)
                eventInit.touches = eventInit.touches.map(asTouch);
            if (eventInit.targetTouches)
                eventInit.targetTouches = eventInit.targetTouches.map(asTouch);
            if (eventInit.changedTouches)
                eventInit.changedTouches = eventInit.changedTouches.map(asTouch);
        }
        event = new TouchEvent(type, eventInit);
    } else if (type === 'wheel') {
        event = new WheelEvent(type, eventInit);
    } else if (type === 'drag' || type === 'dragstart' || type === 'dragend' || type === 'dragover' || type === 'dragenter' || type === 'dragleave' || type === 'drop') {
        event = new DragEvent(type, eventInit);
    } else if (type === 'deviceorientation' || type === 'deviceorientationabsolute') {
        event = new DeviceOrientationEvent(type, eventInit);
    } else if (type === 'devicemotion') {
        event = new DeviceMotionEvent(type, eventInit);
    } else if (type === 'focus' || type === 'blur' || type === 'focusin' || type === 'focusout') {
        event = new FocusEvent(type, eventInit);
    } else if (type === 'input') {
        event = new InputEvent(type, eventInit);
    } else if (Object.prototype.hasOwnProperty.call(eventInit, 'detail')) {
        event = new CustomEvent(type, eventInit);
    } else {
        event = new Event(type, eventInit);
    }
    el.dispatchEvent(event);
";

        /// <summary>
        /// JavaScript function <c>(el, payloadJson) => boolean</c> that dispatches a DOM event.
        /// </summary>
        internal const string FromPayloadFunction = @"(el, payloadJson) => {
    const payload = typeof payloadJson === 'string' ? JSON.parse(payloadJson) : (payloadJson || {});
    const type = payload.type || '';
    const eventInit = Object.assign({ bubbles: true, cancelable: true, composed: true }, payload.eventInit || {});
" + DispatchBody + @"
    return true;
}";

        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        };

        /// <summary>
        /// Serializes <paramref name="type"/> and <paramref name="eventInit"/> for
        /// <see cref="FromPayloadFunction"/>.
        /// </summary>
        /// <param name="type">The DOM event type.</param>
        /// <param name="eventInit">Optional event-init dictionary, or <see langword="null"/>.</param>
        /// <returns>A JSON payload string.</returns>
        internal static string ToPayload(string type, object eventInit)
        {
            Dictionary<string, object> payload = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["type"] = type ?? string.Empty,
                ["eventInit"] = eventInit,
            };
            return JsonSerializer.Serialize(payload, JsonOptions);
        }

        /// <summary>
        /// Builds <c>(el, handle) => boolean</c> that merges one live JSHandle into eventInit.
        /// </summary>
        /// <param name="type">The DOM event type.</param>
        /// <param name="jsonInit">JSON-serializable event-init properties.</param>
        /// <param name="handleKey">Event-init property that receives the handle.</param>
        /// <returns>A function declaration.</returns>
        internal static string WithSingleHandle(string type, object jsonInit, string handleKey)
        {
            string typeJson = JsonSerializer.Serialize(type ?? string.Empty);
            string initJson = jsonInit == null ? "{}" : JsonSerializer.Serialize(jsonInit, JsonOptions);
            string keyJson = JsonSerializer.Serialize(handleKey ?? string.Empty);
            return @"(el, handle) => {
    const type = " + typeJson + @";
    const eventInit = Object.assign({ bubbles: true, cancelable: true, composed: true }, " + initJson + @", { [" + keyJson + @"]: handle });
" + DispatchBody + @"
    return true;
}";
        }

        /// <summary>
        /// Splits <paramref name="eventInit"/> into JSON properties and live <see cref="IJSHandle"/> values.
        /// </summary>
        /// <param name="eventInit">The event-init object passed to dispatch.</param>
        /// <param name="handles">Handle properties, when any exist.</param>
        /// <param name="jsonInit">Remaining JSON-serializable properties.</param>
        /// <returns><see langword="true"/> when at least one property is a JSHandle.</returns>
        internal static bool TryExtractHandles(
            object eventInit,
            out IReadOnlyList<KeyValuePair<string, IJSHandle>> handles,
            out object jsonInit)
        {
            handles = Array.Empty<KeyValuePair<string, IJSHandle>>();
            jsonInit = eventInit;
            if (eventInit == null)
            {
                return false;
            }

            if (eventInit is IJSHandle)
            {
                return false;
            }

            List<KeyValuePair<string, IJSHandle>> found = new List<KeyValuePair<string, IJSHandle>>();
            Dictionary<string, object> json = new Dictionary<string, object>(StringComparer.Ordinal);

            if (eventInit is IDictionary<string, object> typed)
            {
                foreach (KeyValuePair<string, object> pair in typed)
                {
                    AddProperty(pair.Key, pair.Value, found, json);
                }
            }
            else if (eventInit is IDictionary untyped)
            {
                foreach (DictionaryEntry entry in untyped)
                {
                    string key = Convert.ToString(entry.Key, CultureInfo.InvariantCulture);
                    AddProperty(key, entry.Value, found, json);
                }
            }
            else
            {
                Type type = eventInit.GetType();
                if (type.IsPrimitive || eventInit is string)
                {
                    return false;
                }

                foreach (PropertyInfo property in type.GetProperties(BindingFlags.Instance | BindingFlags.Public))
                {
                    if (!property.CanRead || property.GetIndexParameters().Length > 0)
                    {
                        continue;
                    }

                    AddProperty(property.Name, property.GetValue(eventInit), found, json);
                }
            }

            if (found.Count == 0)
            {
                return false;
            }

            handles = found;
            jsonInit = json;
            return true;
        }

        /// <summary>
        /// Throws when <paramref name="argument"/> was created in a different execution world
        /// than <paramref name="target"/>.
        /// </summary>
        /// <param name="target">The element that will evaluate the argument.</param>
        /// <param name="argument">A JSHandle taken from eventInit.</param>
        internal static void EnsureSameContext(IJSHandle target, IJSHandle argument)
        {
            if (target == null || argument == null)
            {
                return;
            }

            if (target is ChromiumJSHandle crTarget && argument is ChromiumJSHandle crArgument)
            {
                if (crTarget.ExecutionContext.ContextId != crArgument.ExecutionContext.ContextId)
                {
                    throw new PlaywrightNativeException(DifferentContextMessage);
                }

                return;
            }

            if (target is WKJSHandle wkTarget && argument is WKJSHandle wkArgument)
            {
                if (wkTarget.ExecutionContext.ContextId != wkArgument.ExecutionContext.ContextId)
                {
                    throw new PlaywrightNativeException(DifferentContextMessage);
                }

                return;
            }

            if (target is FFJSHandle ffTarget && argument is FFJSHandle ffArgument)
            {
                if (!ReferenceEquals(ffTarget.ExecutionContext, ffArgument.ExecutionContext))
                {
                    throw new PlaywrightNativeException(DifferentContextMessage);
                }
            }
        }

        private static void AddProperty(
            string key,
            object value,
            List<KeyValuePair<string, IJSHandle>> handles,
            Dictionary<string, object> json)
        {
            if (string.IsNullOrEmpty(key))
            {
                return;
            }

            if (value is IJSHandle handle)
            {
                handles.Add(new KeyValuePair<string, IJSHandle>(key, handle));
                return;
            }

            json[key] = value;
        }
    }
}
