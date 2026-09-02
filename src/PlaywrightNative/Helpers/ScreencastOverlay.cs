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
using System.Net;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Injects official screencast overlay HTML into a page.
    /// </summary>
    internal static class ScreencastOverlay
    {
        private const string InstallFunction =
            "function(arg){" +
            "function sanitize(html){" +
            "var t=document.createElement('template');t.innerHTML=html;" +
            "var scripts=t.content.querySelectorAll('script');" +
            "for(var i=0;i<scripts.length;i++)scripts[i].remove();" +
            "var nodes=t.content.querySelectorAll('*');" +
            "for(var i=0;i<nodes.length;i++){" +
            "var el=nodes[i];" +
            "for(var j=el.attributes.length-1;j>=0;j--){" +
            "var n=el.attributes[j].name;" +
            "if(n.length>2&&n.charAt(0).toLowerCase()==='o'&&n.charAt(1).toLowerCase()==='n')" +
            "el.removeAttribute(n);}}" +
            "return t.innerHTML;}" +
            "function ensureHost(visible){" +
            "var host=document.querySelector('x-pw-user-overlays');" +
            "if(!host){" +
            "host=document.createElement('x-pw-user-overlays');" +
            "host.setAttribute('data-pw-screencast-overlay','1');" +
            "host.style.cssText='position:fixed;inset:0;z-index:2147483647;pointer-events:none';" +
            "(document.documentElement||document.body).appendChild(host);}" +
            "host.style.visibility=visible?'visible':'hidden';" +
            "return host;}" +
            "if(arg.replace){" +
            "var prev=document.querySelector('x-pw-user-overlays');" +
            "if(prev)prev.remove();}" +
            "if(!arg.overlays||!arg.overlays.length)return;" +
            "var host=ensureHost(arg.visible!==false);" +
            "for(var i=0;i<arg.overlays.length;i++){" +
            "var item=arg.overlays[i];" +
            "if(document.getElementById(item.id))continue;" +
            "var wrap=document.createElement('div');" +
            "wrap.className='x-pw-user-overlay';" +
            "wrap.id=item.id;" +
            "wrap.innerHTML=sanitize(item.html);" +
            "host.appendChild(wrap);}}";

        private const string RemoveFunction =
            "function(id){" +
            "var el=document.getElementById(id);" +
            "if(el)el.remove();" +
            "var host=document.querySelector('x-pw-user-overlays');" +
            "if(host&&!host.querySelector('.x-pw-user-overlay'))host.remove();}";

        private static readonly ConditionalWeakTable<IPage, PageState> States = new();
        private static readonly object StatesGate = new();

        internal static async Task<IAsyncDisposable> ShowAsync(IPage page, string html, float? duration = default)
        {
            Session session = await InjectAsync(page, html, duration).ConfigureAwait(false);
            return new DisposableSession(session);
        }

        internal static async Task ShowChapterAsync(IPage page, string title, string description = default, float? duration = default)
        {
            if (string.IsNullOrEmpty(title))
            {
                throw new ArgumentException("title must be non-empty", nameof(title));
            }

            string titleHtml = WebUtility.HtmlEncode(title);
            string descriptionHtml = string.IsNullOrEmpty(description)
                ? string.Empty
                : "<div data-pw-screencast-chapter-description=\"1\">" + WebUtility.HtmlEncode(description) + "</div>";
            string html =
                "<div style=\"position:absolute;inset:0;display:flex;align-items:center;justify-content:center;" +
                "background:rgba(0,0,0,0.45);backdrop-filter:blur(8px)\">" +
                "<div style=\"color:#fff;text-align:center;font:24px sans-serif\">" +
                "<div data-pw-screencast-chapter-title=\"1\">" + titleHtml + "</div>" +
                descriptionHtml + "</div></div>";
            await InjectAsync(page, html, duration ?? 2000).ConfigureAwait(false);
        }

        internal static async Task SetVisibleAsync(IPage page, bool visible)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            PageState state = GetState(page);
            lock (state.Gate)
            {
                state.Visible = visible;
            }

            string value = visible ? "visible" : "hidden";
            string script =
                "(function(){var nodes=document.querySelectorAll('[data-pw-screencast-overlay],[data-pw-screencast-actions]');" +
                "for(var i=0;i<nodes.length;i++)nodes[i].style.visibility='" + value + "';})()";
            await page.EvaluateAsync(script).ConfigureAwait(false);
            await SyncInitScriptAsync(page, state).ConfigureAwait(false);
        }

        private static async Task<Session> InjectAsync(IPage page, string html, float? duration)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (html == null)
            {
                throw new ArgumentNullException(nameof(html));
            }

            string id = "pw-screencast-ov-" + Guid.NewGuid().ToString("N");
            PageState state = GetState(page);
            OverlayEntry entry = new OverlayEntry(id, html);
            lock (state.Gate)
            {
                state.Overlays.Add(entry);
            }

            state.EnsureHooked(page);
            await EvaluateInstallAsync(page, new[] { entry }, state.Visible, replace: false).ConfigureAwait(false);
            await SyncInitScriptAsync(page, state).ConfigureAwait(false);
            return new Session(page, state, entry, duration);
        }

        private static PageState GetState(IPage page)
        {
            lock (StatesGate)
            {
                if (!States.TryGetValue(page, out PageState state))
                {
                    state = new PageState();
                    States.Add(page, state);
                }

                return state;
            }
        }

        private static async Task ReinstallAsync(IPage page)
        {
            if (!TryGetState(page, out PageState state))
            {
                return;
            }

            OverlayEntry[] snapshot;
            bool visible;
            lock (state.Gate)
            {
                snapshot = state.Overlays.ToArray();
                visible = state.Visible;
            }

            await EvaluateInstallAsync(page, snapshot, visible, replace: true).ConfigureAwait(false);
        }

        private static bool TryGetState(IPage page, out PageState state)
        {
            lock (StatesGate)
            {
                return States.TryGetValue(page, out state);
            }
        }

        private static async Task SyncInitScriptAsync(IPage page, PageState state)
        {
            OverlayEntry[] snapshot;
            bool visible;
            IAsyncDisposable previous;
            lock (state.Gate)
            {
                snapshot = state.Overlays.ToArray();
                visible = state.Visible;
                previous = state.InitScript;
                state.InitScript = null;
            }

            if (previous != null)
            {
                try
                {
                    await previous.DisposeAsync().ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }

            if (snapshot.Length == 0)
            {
                return;
            }

            try
            {
                IAsyncDisposable installed = await page.AddInitScriptAsync(BuildInstallScript(snapshot, visible, replace: true))
                    .ConfigureAwait(false);
                bool keep = false;
                lock (state.Gate)
                {
                    if (state.InitScript == null)
                    {
                        state.InitScript = installed;
                        keep = true;
                    }
                }

                if (!keep)
                {
                    await installed.DisposeAsync().ConfigureAwait(false);
                }
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task EvaluateInstallAsync(IPage page, IReadOnlyList<OverlayEntry> overlays, bool visible, bool replace)
        {
            try
            {
                await page.EvaluateAsync(BuildInstallScript(overlays, visible, replace)).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static string BuildInstallScript(IReadOnlyList<OverlayEntry> overlays, bool visible, bool replace)
        {
            object[] payloadOverlays = new object[overlays.Count];
            for (int i = 0; i < overlays.Count; i++)
            {
                payloadOverlays[i] = new { id = overlays[i].Id, html = overlays[i].Html };
            }

            string payload = JsonSerializer.Serialize(new
            {
                overlays = payloadOverlays,
                visible,
                replace,
            });
            return "(" + InstallFunction + ")(" + payload + ")";
        }

        private static async Task RemoveOverlayAsync(IPage page, PageState state, OverlayEntry entry)
        {
            lock (state.Gate)
            {
                state.Overlays.Remove(entry);
            }

            await SyncInitScriptAsync(page, state).ConfigureAwait(false);

            string idJson = JsonSerializer.Serialize(entry.Id);
            string script = "(" + RemoveFunction + ")(" + idJson + ")";
            try
            {
                await page.EvaluateAsync(script).ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private sealed class OverlayEntry
        {
            internal OverlayEntry(string id, string html)
            {
                Id = id;
                Html = html;
            }

            internal string Id { get; }

            internal string Html { get; }
        }

        private sealed class PageState
        {
            private readonly object _gate = new();
            private readonly List<OverlayEntry> _overlays = new();
            private int _hooked;

            internal object Gate => _gate;

            internal List<OverlayEntry> Overlays => _overlays;

            internal bool Visible { get; set; } = true;

            internal IAsyncDisposable InitScript { get; set; }

            internal void EnsureHooked(IPage page)
            {
                if (Interlocked.Exchange(ref _hooked, 1) != 0)
                {
                    return;
                }

                page.Load += (_, _) =>
                {
                    _ = ReinstallAsync(page);
                };
            }
        }

        private sealed class DisposableSession : IAsyncDisposable
        {
            private readonly Session _session;

            internal DisposableSession(Session session)
            {
                _session = session;
            }

            public ValueTask DisposeAsync() => new ValueTask(_session.RemoveAsync());
        }

        private sealed class Session
        {
            private readonly IPage _page;
            private readonly PageState _state;
            private readonly OverlayEntry _entry;
            private int _removed;

            internal Session(IPage page, PageState state, OverlayEntry entry, float? duration)
            {
                _page = page;
                _state = state;
                _entry = entry;
                if (duration.HasValue && duration.Value > 0)
                {
                    _ = RemoveAfterAsync(duration.Value);
                }
            }

            internal Task RemoveAsync()
            {
                if (Interlocked.Exchange(ref _removed, 1) != 0)
                {
                    return Task.CompletedTask;
                }

                return RemoveOverlayAsync(_page, _state, _entry);
            }

            private async Task RemoveAfterAsync(float milliseconds)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(milliseconds)).ConfigureAwait(false);
                await RemoveAsync().ConfigureAwait(false);
            }
        }
    }
}
