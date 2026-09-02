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
#pragma warning disable CA1034, CA2225, SA1402
using System;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Legacy browser-context event subscription helpers.
    /// </summary>
    public static class BrowserContextEventCompatExtensions
    {
        /// <summary>Legacy service-worker event subscription helper.</summary>
        public static ServiceWorkerEventAccessor ServiceWorker(this IBrowserContext context)
            => new ServiceWorkerEventAccessor(context);

        /// <summary>Supports <c>context.ServiceWorker() += handler</c>.</summary>
        public readonly struct ServiceWorkerEventAccessor
        {
            private readonly IBrowserContext _context;

            internal ServiceWorkerEventAccessor(IBrowserContext context)
                => _context = context;

            /// <summary>Subscribes a service-worker handler.</summary>
            public static ServiceWorkerEventAccessor operator +(
                ServiceWorkerEventAccessor accessor,
                EventHandler<IWorker> handler)
            {
                if (accessor._context is IHasBrowserContextExtras extras)
                {
                    extras.ServiceWorker += handler;
                }

                return accessor;
            }

            /// <summary>Unsubscribes a service-worker handler.</summary>
            public static ServiceWorkerEventAccessor operator -(
                ServiceWorkerEventAccessor accessor,
                EventHandler<IWorker> handler)
            {
                if (accessor._context is IHasBrowserContextExtras extras)
                {
                    extras.ServiceWorker -= handler;
                }

                return accessor;
            }
        }
    }

    /// <summary>
    /// Legacy page event subscription helpers.
    /// </summary>
    public static class PageEventCompatExtensions
    {
        /// <summary>Legacy page-error event subscription helper.</summary>
        public static PageErrorEventAccessor PageError(this IPage page)
            => new PageErrorEventAccessor(page);

        /// <summary>Legacy dialog-closed event subscription helper.</summary>
        public static DialogClosedEventAccessor DialogClosed(this IPage page)
            => new DialogClosedEventAccessor(page);

        /// <summary>Supports <c>page.DialogClosed() += handler</c> (legacy spelling).</summary>
        public readonly struct DialogClosedEventAccessor
        {
            private readonly IPage _page;

            internal DialogClosedEventAccessor(IPage page)
                => _page = page;

            /// <summary>Subscribes a dialog-closed handler.</summary>
            public static DialogClosedEventAccessor operator +(
                DialogClosedEventAccessor accessor,
                EventHandler<IDialog> handler)
            {
                if (accessor._page is IHasPageExtras extras)
                {
                    extras.DialogClosed += handler;
                }

                return accessor;
            }

            /// <summary>Unsubscribes a dialog-closed handler.</summary>
            public static DialogClosedEventAccessor operator -(
                DialogClosedEventAccessor accessor,
                EventHandler<IDialog> handler)
            {
                if (accessor._page is IHasPageExtras extras)
                {
                    extras.DialogClosed -= handler;
                }

                return accessor;
            }
        }

        /// <summary>Supports <c>page.PageError() + handler</c>.</summary>
        public readonly struct PageErrorEventAccessor
        {
            private readonly IPage _page;

            internal PageErrorEventAccessor(IPage page)
                => _page = page;

            /// <summary>Subscribes a page-error handler.</summary>
            public static PageErrorEventAccessor operator +(
                PageErrorEventAccessor accessor,
                EventHandler<PageErrorEventArgs> handler)
            {
                if (accessor._page != null)
                {
                    EventHandler<string> bridge = (_, message) =>
                    {
                        PageErrorEventArgs args = PageErrorText.Parse(message);
                        handler(accessor._page, args);
                    };
                    accessor._page.PageError += bridge;
                }

                return accessor;
            }

            /// <summary>Unsubscribes a page-error handler.</summary>
            public static PageErrorEventAccessor operator -(
                PageErrorEventAccessor accessor,
                EventHandler<PageErrorEventArgs> handler)
            {
                _ = accessor;
                _ = handler;
                return accessor;
            }
        }
    }
}
