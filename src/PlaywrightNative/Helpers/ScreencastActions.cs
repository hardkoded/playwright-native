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
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>screencast.showActions</c> annotations shown before input.
    /// </summary>
    internal static class ScreencastActions
    {
        private const string PaintFunction =
            "(function(arg){" +
            "var host=document.getElementById('pw-screencast-actions');" +
            "if(!host){host=document.createElement('div');host.id='pw-screencast-actions';" +
            "host.setAttribute('data-pw-screencast-actions','1');" +
            "host.style.cssText='position:fixed;inset:0;pointer-events:none;z-index:2147483647;';" +
            "document.documentElement.appendChild(host);}" +
            "host.style.visibility='visible';host.removeAttribute('hidden');" +
            "function show(tag,attr){var n=host.querySelector(tag);if(!n){n=document.createElement(tag);" +
            "if(attr)n.setAttribute(attr,'1');host.appendChild(n);}n.removeAttribute('hidden');" +
            "n.style.visibility='visible';n.style.display='block';return n;}" +
            "function hide(n){if(!n)return;n.setAttribute('hidden','true');n.style.display='none';n.style.visibility='hidden';}" +
            "var cx=0,cy=0;" +
            "if(arg.box){cx=arg.box.x+arg.box.width/2;cy=arg.box.y+arg.box.height/2;" +
            "var hl=show('x-pw-highlight','data-pw-screencast-action-box');" +
            "hl.style.position='absolute';hl.style.left=arg.box.x+'px';hl.style.top=arg.box.y+'px';" +
            "hl.style.width=arg.box.width+'px';hl.style.height=arg.box.height+'px';" +
            "hl.style.backgroundColor='rgba(0, 128, 255, 0.15)';" +
            "hl.style.border='2px solid rgba(0, 128, 255, 0.6)';" +
            "hl.style.borderColor='rgba(0, 128, 255, 0.6)';" +
            "hl.style.boxSizing='border-box';hl.style.pointerEvents='none';" +
            "var ap=show('x-pw-action-point',null);" +
            "ap.style.position='absolute';ap.style.width='20px';ap.style.height='20px';" +
            "ap.style.background='red';ap.style.borderRadius='10px';" +
            "ap.style.margin='-10px 0 0 -10px';ap.style.left=cx+'px';ap.style.top=cy+'px';ap.style.zIndex='2';}" +
            "else{hide(host.querySelector('x-pw-highlight'));hide(host.querySelector('x-pw-action-point'));}" +
            "var cur=host.querySelector('x-pw-action-cursor');" +
            "if(arg.cursor&&arg.point){if(!cur){cur=document.createElement('x-pw-action-cursor');" +
            "cur.setAttribute('data-pw-screencast-action-cursor','1');" +
            "cur.style.position='absolute';cur.style.width='18px';cur.style.height='22px';" +
            "cur.style.background='#fff';cur.style.border='2px solid #000';cur.style.boxSizing='border-box';" +
            "cur.style.zIndex='4';cur.style.pointerEvents='none';host.appendChild(cur);}" +
            "cur.removeAttribute('hidden');cur.style.display='block';cur.style.visibility='visible';" +
            "cur.style.transition=arg.restore?'':'top 200ms ease, left 200ms ease';" +
            "cur.style.left=Math.round(arg.point.x)+'px';cur.style.top=Math.round(arg.point.y)+'px';}" +
            "else if(!arg.cursor){hide(cur);}" +
            "if(arg.title){var t=show('x-pw-title','data-pw-screencast-action-title');t.textContent=arg.title;" +
            "t.style.position='absolute';t.style.color='#fff';t.style.backgroundColor='rgba(0, 0, 0, 0.5)';" +
            "t.style.borderRadius='6px';t.style.padding='6px';t.style.fontFamily='sans-serif';" +
            "t.style.fontSize=arg.fontSize+'px';t.style.lineHeight='1.4';t.style.whiteSpace='nowrap';t.style.zIndex='3';" +
            "t.style.top='';t.style.bottom='';t.style.left='';t.style.right='';t.style.transform='';" +
            "var p=arg.pos||'top-right';" +
            "if(p==='top-left'){t.style.top='6px';t.style.left='6px';}" +
            "else if(p==='top'){t.style.top='6px';t.style.left='50%';t.style.transform='translateX(-50%)';}" +
            "else if(p==='bottom-left'){t.style.bottom='6px';t.style.left='6px';}" +
            "else if(p==='bottom'){t.style.bottom='6px';t.style.left='50%';t.style.transform='translateX(-50%)';}" +
            "else if(p==='bottom-right'){t.style.bottom='6px';t.style.right='6px';}" +
            "else{t.style.top='6px';t.style.right='6px';}}" +
            "})";

        private const string HideAnnotationsFunction =
            "(function(){var host=document.getElementById('pw-screencast-actions');if(!host)return;" +
            "var tags=['x-pw-highlight','x-pw-action-point','x-pw-title'];" +
            "for(var i=0;i<tags.length;i++){var n=host.querySelector(tags[i]);" +
            "if(n){n.setAttribute('hidden','true');n.style.display='none';n.style.visibility='hidden';}}})()";

        private const string HideCursorFunction =
            "(function(){var host=document.getElementById('pw-screencast-actions');if(!host)return;" +
            "var n=host.querySelector('x-pw-action-cursor');" +
            "if(n){n.setAttribute('hidden','true');n.style.display='none';n.style.visibility='hidden';}})()";

        private static readonly ConditionalWeakTable<IPage, Options> Sessions = new();

        // Official screencast.ts _cursorPoint: the cursor outlives the annotation and
        // stays at the last action point until hideActions, across navigations.
        private static readonly ConditionalWeakTable<IPage, CursorState> Cursors = new();

        internal static void Show(IPage page, float? duration, AnnotatePosition position, int fontSize, ScreencastCursor cursor)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (Sessions.TryGetValue(page, out Options existing))
            {
                existing.BumpGeneration();
                Sessions.Remove(page);
            }

            Sessions.Add(page, new Options
            {
                Duration = duration.HasValue && duration.Value > 0 ? duration.Value : 500,
                Position = position == EnumCompat.UndefinedAnnotatePosition ? AnnotatePosition.TopRight : position,
                FontSize = fontSize > 0 ? fontSize : 24,
                Cursor = cursor == EnumCompat.UndefinedScreencastCursor ? ScreencastCursor.Pointer : cursor,
            });

            if (cursor == ScreencastCursor.None)
            {
                _ = HideCursorAsync(page);
            }
        }

        internal static Task HideAsync(IPage page)
        {
            if (page == null)
            {
                return Task.CompletedTask;
            }

            if (Sessions.TryGetValue(page, out Options options))
            {
                options.BumpGeneration();
                Sessions.Remove(page);
            }

            if (Cursors.TryGetValue(page, out CursorState cursor))
            {
                cursor.Point = null;
            }

            return RemoveOverlayAsync(page);
        }

        internal static async Task AnnotateIfEnabledAsync(IElementHandle handle, string apiName)
        {
            if (handle == null || !IsInputAction(apiName))
            {
                return;
            }

            IFrame frame;
            try
            {
                frame = await handle.OwnerFrameAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
                return;
            }

            IPage page = frame?.Page;
            if (page == null || !Sessions.TryGetValue(page, out Options options))
            {
                return;
            }

            ElementHandleBoundingBoxResult box = null;
            try
            {
                box = await handle.BoundingBoxAsync().ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }

            Position point = box == null
                ? null
                : new Position { X = box.X + (box.Width / 2), Y = box.Y + (box.Height / 2) };
            if (point != null && options.Cursor == ScreencastCursor.Pointer)
            {
                RememberCursor(page, point);
            }

            int generation = options.BumpGeneration();
            await PaintAsync(page, TitleFromApiName(apiName), box, point, options).ConfigureAwait(false);
            await Task.Delay(TimeSpan.FromMilliseconds(options.Duration)).ConfigureAwait(false);
            if (options.CurrentGeneration == generation)
            {
                await HideAnnotationsAsync(page).ConfigureAwait(false);
            }
        }

        private static bool IsInputAction(string apiName)
        {
            if (string.IsNullOrEmpty(apiName))
            {
                return false;
            }

            return apiName.EndsWith(".click", StringComparison.Ordinal)
                || apiName.EndsWith(".dblclick", StringComparison.Ordinal)
                || apiName.EndsWith(".fill", StringComparison.Ordinal)
                || apiName.EndsWith(".hover", StringComparison.Ordinal)
                || apiName.EndsWith(".tap", StringComparison.Ordinal)
                || apiName.EndsWith(".check", StringComparison.Ordinal)
                || apiName.EndsWith(".uncheck", StringComparison.Ordinal)
                || apiName.EndsWith(".press", StringComparison.Ordinal)
                || apiName.EndsWith(".type", StringComparison.Ordinal)
                || apiName.EndsWith(".setInputFiles", StringComparison.Ordinal);
        }

        private static string TitleFromApiName(string apiName)
        {
            int dot = apiName.LastIndexOf('.');
            string last = dot >= 0 ? apiName.Substring(dot + 1) : apiName;
            if (last.Length == 0)
            {
                return apiName;
            }

            return char.ToUpperInvariant(last[0]) + last.Substring(1);
        }

        private static void RememberCursor(IPage page, Position point)
        {
            CursorState state = Cursors.GetValue(page, static target =>
            {
                target.DOMContentLoaded += (_, _) => _ = RestoreCursorAsync(target);
                return new CursorState();
            });
            state.Point = point;
        }

        /// <summary>
        /// Official <c>restoreScreencastCursor</c>: a new document starts without the
        /// cursor, so paint it back at the last action point.
        /// </summary>
        private static async Task RestoreCursorAsync(IPage page)
        {
            if (!Cursors.TryGetValue(page, out CursorState state) || state.Point is not Position point)
            {
                return;
            }

            string payload = JsonSerializer.Serialize(new
            {
                cursor = true,
                restore = true,
                point = new { x = point.X, y = point.Y },
            });
            try
            {
                await page.EvaluateAsync(PaintFunction + "(" + payload + ")").ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        private static async Task HideCursorAsync(IPage page)
        {
            if (Cursors.TryGetValue(page, out CursorState state))
            {
                state.Point = null;
            }

            try
            {
                await page.EvaluateAsync(HideCursorFunction).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        private static async Task PaintAsync(IPage page, string title, ElementHandleBoundingBoxResult box, Position point, Options options)
        {
            object boxPayload = box == null
                ? null
                : new { x = box.X, y = box.Y, width = box.Width, height = box.Height };
            object pointPayload = point == null
                ? null
                : new { x = point.X, y = point.Y };
            string payload = JsonSerializer.Serialize(new
            {
                title,
                pos = PositionName(options.Position),
                fontSize = options.FontSize,
                cursor = options.Cursor == ScreencastCursor.Pointer,
                box = boxPayload,
                point = pointPayload,
            });

            try
            {
                await page.EvaluateAsync(PaintFunction + "(" + payload + ")").ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        private static async Task HideAnnotationsAsync(IPage page)
        {
            try
            {
                await page.EvaluateAsync(HideAnnotationsFunction).ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        private static async Task RemoveOverlayAsync(IPage page)
        {
            try
            {
                await page.EvaluateAsync(
                    "(function(){var el=document.getElementById('pw-screencast-actions');if(el)el.remove();})()").ConfigureAwait(false);
            }
            catch (PlaywrightException)
            {
            }
        }

        private static string PositionName(AnnotatePosition position)
        {
            switch (position)
            {
                case AnnotatePosition.TopLeft:
                    return "top-left";
                case AnnotatePosition.Top:
                    return "top";
                case AnnotatePosition.BottomLeft:
                    return "bottom-left";
                case AnnotatePosition.Bottom:
                    return "bottom";
                case AnnotatePosition.BottomRight:
                    return "bottom-right";
                default:
                    return "top-right";
            }
        }

        private sealed class Options
        {
            private int _generation;

            internal float Duration { get; set; }

            internal AnnotatePosition Position { get; set; }

            internal int FontSize { get; set; }

            internal ScreencastCursor Cursor { get; set; }

            internal int CurrentGeneration => Volatile.Read(ref _generation);

            internal int BumpGeneration() => Interlocked.Increment(ref _generation);
        }

        private sealed class CursorState
        {
            /// <summary>Gets or sets the last action point, or <see langword="null"/> when hidden.</summary>
            internal Position Point { get; set; }
        }
    }
}
