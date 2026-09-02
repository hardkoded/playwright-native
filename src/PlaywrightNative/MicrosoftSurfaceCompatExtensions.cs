/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Chromium;
using PlaywrightNative.Firefox;
using PlaywrightNative.Helpers;
using PlaywrightNative.WebKit;

namespace PlaywrightNative
{
    /// <summary>
    /// Surface helpers that exist on PlaywrightNative implementations but are missing from
    /// official <c>Microsoft.Playwright</c> interfaces, plus local overloads that
    /// options-bag instance methods would otherwise shadow.
    /// </summary>
    public static class MicrosoftSurfaceCompatExtensions
    {
        /// <summary>Service worker that initiated the request, when any.</summary>
        public static IWorker ServiceWorker(this IRequest request)
            => request is ChromiumRequest chromium ? chromium.ServiceWorker : null;

        /// <summary>Active service workers in the context.</summary>
        public static IReadOnlyCollection<IWorker> ServiceWorkers(this IBrowserContext context)
            => context is IHasBrowserContextExtras extras
                ? extras.ServiceWorkers
                : Array.Empty<IWorker>();

        /// <summary>Legacy alias for <see cref="IVideo.PathAsync"/>.</summary>
        public static Task<string> GetPathAsync(this IVideo video)
            => video.PathAsync();

        /// <summary>Legacy alias for <see cref="IResponse.TextAsync"/>.</summary>
        public static Task<string> GetTextAsync(this IResponse response)
            => response.TextAsync();

        /// <summary>Legacy alias for <see cref="IResponse.JsonAsync{T}"/>.</summary>
        /// <typeparam name="T">Deserialized JSON type.</typeparam>
        public static Task<T> GetJsonAsync<T>(this IResponse response)
            => response.JsonAsync<T>();

        /// <summary>Reads the body as a <see cref="JsonDocument"/>.</summary>
        public static Task<JsonDocument> GetJsonAsync(this IResponse response, JsonDocumentOptions options = default)
            => ResponseContent.ReadJsonDocumentAsync(response.BodyAsync, options);

        /// <summary>Legacy alias for <see cref="IResponse.FinishedAsync"/>.</summary>
        public static Task<string> GetFinishedAsync(this IResponse response)
            => response.FinishedAsync();

        /// <summary>Legacy frame navigation spelling.</summary>
        public static Task<IResponse> GoToAsync(
            this IFrame frame,
            string url,
            WaitUntilState waitUntil = default,
            float? timeout = default,
            string referer = default)
        {
            switch (frame)
            {
                case ChromiumFrame chromium:
                    return chromium.GoToAsync(url, waitUntil, timeout, referer);
                case WebKitFrame webkit:
                    return webkit.GoToAsync(url, waitUntil, timeout, referer);
                default:
                    return frame.GotoAsync(url, new FrameGotoOptions
                    {
                        WaitUntil = waitUntil,
                        Timeout = timeout,
                        Referer = referer,
                    });
            }
        }

        /// <summary>Page-level drag/drop helper.</summary>
        public static Task DropAsync(
            this IPage page,
            string selector,
            DropPayload payload,
            Position position = default,
            float? timeout = default,
            bool? strict = default)
            => PageDropHelper.RunAsync(page, selector, payload, position, timeout, strict);

        /// <summary>Legacy go-back with wait-until state.</summary>
        public static Task<IResponse> GoBackAsync(this IPage page, WaitUntilState waitUntil = default, float? timeout = default)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.GoBackAsync(waitUntil, timeout);
                case FirefoxPage firefox:
                    return firefox.GoBackAsync(waitUntil, timeout);
                case WKPage webkit:
                    return webkit.GoBackAsync(waitUntil, timeout);
                default:
                    return page.GoBackAsync(new PageGoBackOptions { WaitUntil = waitUntil, Timeout = timeout });
            }
        }

        /// <summary>Legacy drag-and-drop with source/target positions.</summary>
        public static Task DragAndDropAsync(
            this IPage page,
            string source,
            string target,
            Position sourcePosition,
            Position targetPosition)
        {
            switch (page)
            {
                case Page chromium:
                    return chromium.DragAndDropAsync(source, target, sourcePosition, targetPosition);
                case WKPage webkit:
                    return webkit.DragAndDropAsync(source, target, sourcePosition, targetPosition);
                default:
                    return page.DragAndDropAsync(source, target, new PageDragAndDropOptions
                    {
                        SourcePosition = sourcePosition == null ? null : new SourcePosition { X = sourcePosition.X, Y = sourcePosition.Y },
                        TargetPosition = targetPosition == null ? null : new TargetPosition { X = targetPosition.X, Y = targetPosition.Y },
                    });
            }
        }

        /// <summary>Selects text in the first matching element.</summary>
        [System.Runtime.CompilerServices.OverloadResolutionPriority(1)]
        public static async Task SelectTextAsync(
            this IPage page,
            string selector,
            float? timeout = default,
            bool? force = default,
            ActionScroll scroll = default,
            bool? strict = default)
        {
            IElementHandle handle = await page.WaitForSelectorAsync(
                selector,
                new PageWaitForSelectorOptions
                {
                    State = WaitForSelectorState.Visible,
                    Timeout = timeout,
                    Strict = strict,
                }).ConfigureAwait(false);
            if (handle == null)
            {
                throw new PlaywrightNativeException($"Failed to find element matching selector \"{selector}\"");
            }

            await handle.SelectTextAsync(timeout, force, scroll).ConfigureAwait(false);
        }

        /// <summary>JSON aria snapshot for the document element.</summary>
        public static async Task<string> AriaSnapshotJsonAsync(
            this IPage page,
            float? timeout = default,
            AriaSnapshotMode mode = default,
            int? depth = default,
            bool? boxes = default)
        {
            _ = timeout;
            IElementHandle root = await page.QuerySelectorAsync("html").ConfigureAwait(false)
                ?? throw new PlaywrightNativeException("page.ariaSnapshotJSON: no documentElement.");
            return await root.AriaSnapshotJsonAsync(mode, depth, boxes).ConfigureAwait(false);
        }

        /// <summary>JSON aria snapshot for a locator.</summary>
        public static Task<string> AriaSnapshotJsonAsync(
            this ILocator locator,
            float? timeout = default,
            AriaSnapshotMode mode = default,
            int? depth = default,
            bool? boxes = default)
        {
            if (locator is Locator concrete)
            {
                return concrete.AriaSnapshotJsonAsync(timeout, mode, depth, boxes);
            }

            throw new NotSupportedException("AriaSnapshotJsonAsync requires a PlaywrightNative locator.");
        }

        /// <summary>Starts Chromium tracing on the browser.</summary>
        public static Task StartTracingAsync(
            this IBrowser browser,
            IPage page = default,
            string path = default,
            bool screenshots = default,
            IEnumerable<string> categories = default)
            => browser switch
            {
                ChromiumBrowser chromium => chromium.StartTracingAsync(page, path, screenshots, categories),
                FirefoxBrowser firefox => firefox.StartTracingAsync(page, path, screenshots, categories),
                WKBrowser webkit => webkit.StartTracingAsync(page, path, screenshots, categories),
                _ => throw new NotSupportedException("startTracing requires a PlaywrightNative browser."),
            };

        /// <summary>Stops Chromium tracing on the browser.</summary>
        public static Task<byte[]> StopTracingAsync(this IBrowser browser)
            => browser switch
            {
                ChromiumBrowser chromium => chromium.StopTracingAsync(),
                FirefoxBrowser firefox => firefox.StopTracingAsync(),
                WKBrowser webkit => webkit.StopTracingAsync(),
                _ => throw new NotSupportedException("stopTracing requires a PlaywrightNative browser."),
            };

        /// <summary>Local tracing start options overload.</summary>
        public static Task StartAsync(this ITracing tracing, PlaywrightNative.TracingStartOptions options)
            => tracing switch
            {
                CRTracing chromium => chromium.StartAsync(options),
                EmptyTracing empty => empty.StartAsync(options),
                _ => tracing.StartAsync(new Microsoft.Playwright.TracingStartOptions
                {
                    Screenshots = options?.Screenshots,
                    Snapshots = options?.Snapshots,
                    Sources = options?.Sources,
                    Name = options?.Name,
                    Title = options?.Title,
                }),
            };

        /// <summary>Expanded-parameter stop overload.</summary>
        public static Task StopAsync(this ITracing tracing, string path)
            => tracing.StopAsync(new TracingStopOptions { Path = path });

        /// <summary>Expanded-parameter stop-chunk overload.</summary>
        public static Task StopChunkAsync(this ITracing tracing, string path = default)
            => tracing switch
            {
                CRTracing chromium => chromium.StopChunkAsync(path),
                EmptyTracing empty => empty.StopChunkAsync(path),
                _ => tracing.StopChunkAsync(new TracingStopChunkOptions { Path = path }),
            };

        /// <summary>Expanded-parameter start-chunk overload.</summary>
        public static Task StartChunkAsync(this ITracing tracing, string name = default, string title = default)
            => tracing switch
            {
                CRTracing chromium => chromium.StartChunkAsync(name, title),
                EmptyTracing empty => empty.StartChunkAsync(name, title),
                _ => tracing.StartChunkAsync(new TracingStartChunkOptions { Name = name, Title = title }),
            };

        /// <summary>HAR recording helper on tracing.</summary>
        public static Task<IAsyncDisposable> StartHarAsync(
            this ITracing tracing,
            string path,
            HarContentPolicy content = default,
            HarMode mode = default,
            string url = default,
            Regex urlRegex = default,
            string resourcesDir = default)
        {
            if (tracing is CRTracing chromium)
            {
                return chromium.StartHarAsync(path, content, mode, url, urlRegex, resourcesDir);
            }

            if (tracing is EmptyTracing empty)
            {
                return empty.StartHarAsync(path, content, mode, url, urlRegex, resourcesDir);
            }

            throw new NotSupportedException("StartHarAsync requires a PlaywrightNative tracing instance.");
        }

        /// <summary>Clock install with fractional numeric types.</summary>
        public static Task InstallAsync(this IClock clock, double time)
            => clock.InstallAsync(time.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Clock install with fractional numeric types.</summary>
        public static Task InstallAsync(this IClock clock, float time)
            => clock.InstallAsync((double)time);

        /// <summary>Clock install with integral numeric types.</summary>
        public static Task InstallAsync(this IClock clock, long time)
            => clock.InstallAsync(time.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Clock install with <see cref="DateTime"/>.</summary>
        public static Task InstallAsync(this IClock clock, DateTime time)
            => clock is Clock concrete
                ? concrete.InstallAsync(time)
                : clock.InstallAsync(new ClockInstallOptions { TimeDate = time });

        /// <summary>Clock install with a parseable time string.</summary>
        public static Task InstallAsync(this IClock clock, string time)
            => clock is Clock concrete
                ? concrete.InstallAsync(time)
                : clock.InstallAsync(new ClockInstallOptions { TimeString = time });

        /// <summary>Expanded-parameter selector registration.</summary>
        public static Task RegisterAsync(
            this ISelectors selectors,
            string name,
            string script = default,
            string path = default,
            bool contentScript = default)
            => selectors is Selectors concrete
                ? concrete.RegisterAsync(name, script, path, contentScript)
                : selectors.RegisterAsync(name, new SelectorsRegisterOptions
                {
                    Script = script,
                    Path = path,
                    ContentScript = contentScript,
                });

        /// <summary>Expanded-parameter grant-permissions overload.</summary>
        public static Task GrantPermissionsAsync(
            this IBrowserContext context,
            IEnumerable<string> permissions,
            string origin)
            => context switch
            {
                ChromiumBrowserContext chromium => chromium.GrantPermissionsAsync(permissions, origin),
                FirefoxBrowserContext firefox => firefox.GrantPermissionsAsync(permissions, origin),
                WKBrowserContext webkit => webkit.GrantPermissionsAsync(permissions, origin),
                _ => context.GrantPermissionsAsync(permissions, new BrowserContextGrantPermissionsOptions { Origin = origin }),
            };

        /// <summary>Screencast show-actions with expanded parameters.</summary>
        public static Task<IAsyncDisposable> ShowActionsAsync(
            this IScreencast screencast,
            float? duration = default,
            AnnotatePosition position = default,
            int fontSize = default,
            ScreencastCursor cursor = default)
        {
            if (screencast is CRScreencast chromium)
            {
                return chromium.ShowActionsAsync(duration, position, fontSize, cursor);
            }

            if (screencast is WKScreencast webkit)
            {
                return webkit.ShowActionsAsync(duration, position, fontSize, cursor);
            }

            if (screencast is EmptyScreencast empty)
            {
                return empty.ShowActionsAsync(duration, position, fontSize, cursor);
            }

            return screencast.ShowActionsAsync(new ScreencastShowActionsOptions
            {
                Duration = duration,
                Position = position,
                FontSize = fontSize == 0 ? null : fontSize,
                Cursor = cursor,
            });
        }

        /// <summary>Evaluate with official <c>exposeFunctions</c> on a page.</summary>
        /// <typeparam name="T">Result type.</typeparam>
        public static Task<T> EvaluateExposingFunctionsAsync<T>(this IPage page, string expression, object arg = default)
            => EvaluateCallbacks.EvaluateTargetAsync<T>(page, expression, arg, exposeFunctions: true);

        /// <summary>Evaluate handle with official <c>exposeFunctions</c>.</summary>
        public static Task<IJSHandle> EvaluateHandleExposingFunctionsAsync(this IPage page, string expression, object arg = default)
            => EvaluateCallbacks.EvaluateHandleTargetAsync(page, expression, arg, exposeFunctions: true);

        /// <summary>Add init script with official <c>exposeFunctions</c>.</summary>
        public static Task<IAsyncDisposable> AddInitScriptExposingFunctionsAsync(this IPage page, string script, object arg = default)
            => EvaluateCallbacks.AddInitScriptTargetAsync(page, script, arg, exposeFunctions: true);

        /// <summary>Context add-init-script with official <c>exposeFunctions</c>.</summary>
        public static Task<IAsyncDisposable> AddInitScriptExposingFunctionsAsync(this IBrowserContext context, string script, object arg = default)
            => context switch
            {
                ChromiumBrowserContext chromium => chromium.AddInitScriptAsync(script, arg, exposeFunctions: true),
                FirefoxBrowserContext firefox => firefox.AddInitScriptAsync(script, arg, exposeFunctions: true),
                WKBrowserContext webkit => webkit.AddInitScriptAsync(script, arg, exposeFunctions: true),
                _ => throw new NotSupportedException("AddInitScriptExposingFunctionsAsync requires a PlaywrightNative context."),
            };

        /// <summary>Screenshot assertion for pages.</summary>
        public static Task ToHaveScreenshotAsync(
            this IPageAssertions assertions,
            byte[] expected,
            int? maxDiffPixels = default,
            float? maxDiffPixelRatio = default,
            float? threshold = default,
            float? timeout = default,
            string animations = default,
            string caret = default,
            bool? omitBackground = default,
            IEnumerable<ILocator> mask = default,
            string maskColor = default)
            => assertions is PageAssertions concrete
                ? concrete.ToHaveScreenshotAsync(
                    expected,
                    maxDiffPixels,
                    maxDiffPixelRatio,
                    threshold,
                    timeout,
                    animations,
                    caret,
                    omitBackground,
                    mask,
                    maskColor)
                : throw new NotSupportedException("ToHaveScreenshotAsync requires PlaywrightNative assertions.");

        /// <summary>Screenshot assertion for pages from a path.</summary>
        public static Task ToHaveScreenshotAsync(
            this IPageAssertions assertions,
            string path,
            int? maxDiffPixels = default,
            float? maxDiffPixelRatio = default,
            float? threshold = default,
            float? timeout = default,
            string animations = default,
            string caret = default,
            bool? omitBackground = default,
            IEnumerable<ILocator> mask = default,
            string maskColor = default)
            => assertions is PageAssertions concrete
                ? concrete.ToHaveScreenshotAsync(
                    path,
                    maxDiffPixels,
                    maxDiffPixelRatio,
                    threshold,
                    timeout,
                    animations,
                    caret,
                    omitBackground,
                    mask,
                    maskColor)
                : throw new NotSupportedException("ToHaveScreenshotAsync requires PlaywrightNative assertions.");

        /// <summary>Screenshot assertion for locators.</summary>
        public static Task ToHaveScreenshotAsync(
            this ILocatorAssertions assertions,
            byte[] expected,
            int? maxDiffPixels = default,
            float? maxDiffPixelRatio = default,
            float? threshold = default,
            float? timeout = default,
            string animations = default,
            string caret = default,
            bool? omitBackground = default,
            IEnumerable<ILocator> mask = default,
            string maskColor = default)
            => assertions is LocatorAssertions concrete
                ? concrete.ToHaveScreenshotAsync(
                    expected,
                    maxDiffPixels,
                    maxDiffPixelRatio,
                    threshold,
                    timeout,
                    animations,
                    caret,
                    omitBackground,
                    mask,
                    maskColor)
                : throw new NotSupportedException("ToHaveScreenshotAsync requires PlaywrightNative assertions.");

        /// <summary>Screenshot assertion for locators from a path.</summary>
        public static Task ToHaveScreenshotAsync(
            this ILocatorAssertions assertions,
            string path,
            int? maxDiffPixels = default,
            float? maxDiffPixelRatio = default,
            float? threshold = default,
            float? timeout = default,
            string animations = default,
            string caret = default,
            bool? omitBackground = default,
            IEnumerable<ILocator> mask = default,
            string maskColor = default)
            => assertions is LocatorAssertions concrete
                ? concrete.ToHaveScreenshotAsync(
                    path,
                    maxDiffPixels,
                    maxDiffPixelRatio,
                    threshold,
                    timeout,
                    animations,
                    caret,
                    omitBackground,
                    mask,
                    maskColor)
                : throw new NotSupportedException("ToHaveScreenshotAsync requires PlaywrightNative assertions.");

        /// <summary>Pass-until assertion helper.</summary>
        public static Task ToPassAsync(this ILocatorAssertions assertions, Func<Task> assertion, float? timeout = default)
            => assertions is LocatorAssertions concrete
                ? concrete.ToPassAsync(assertion, timeout)
                : throw new NotSupportedException("ToPassAsync requires PlaywrightNative assertions.");

        /// <summary>Wait for a console message with a predicate.</summary>
        public static Task<IConsoleMessage> WaitForConsoleMessageAsync(
            this IPage page,
            Func<IConsoleMessage, bool> predicate,
            float? timeout = default)
            => page.WaitForEventAsync(PageEvent.Console, predicate, timeout);

        /// <summary>Wait for any console message.</summary>
        public static Task<IConsoleMessage> WaitForConsoleMessageAsync(this IPage page, float? timeout = default)
            => page.WaitForEventAsync(PageEvent.Console, predicate: null, timeout);

        /// <summary>Wait for a console message with a predicate on the context.</summary>
        public static Task<IConsoleMessage> WaitForConsoleMessageAsync(
            this IBrowserContext context,
            Func<IConsoleMessage, bool> predicate,
            float? timeout = default)
            => context.WaitForEventAsync(BrowserContextEvent.Console, predicate, timeout);

        /// <summary>Clock pause-at with unix milliseconds.</summary>
        public static Task PauseAtAsync(this IClock clock, long time)
            => clock is Clock concrete
                ? concrete.PauseAtAsync(time)
                : clock.PauseAtAsync(DateTimeOffset.FromUnixTimeMilliseconds(time).UtcDateTime);

        /// <summary>Clock pause-at with a parseable time string.</summary>
        public static Task PauseAtAsync(this IClock clock, string time)
            => clock is Clock concrete
                ? concrete.PauseAtAsync(time)
                : clock.PauseAtAsync(DateTime.Parse(time, System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Clock fast-forward by ticks.</summary>
        public static Task FastForwardAsync(this IClock clock, long ticks)
            => clock is Clock concrete
                ? concrete.FastForwardAsync(ticks)
                : clock.FastForwardAsync(ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Clock run-for by ticks.</summary>
        public static Task RunForAsync(this IClock clock, long ticks)
            => clock is Clock concrete
                ? concrete.RunForAsync(ticks)
                : clock.RunForAsync(ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Clock run-for by fractional ticks.</summary>
        public static Task RunForAsync(this IClock clock, double ticks)
            => clock is Clock concrete
                ? concrete.RunForAsync(ticks)
                : clock.RunForAsync(ticks.ToString(System.Globalization.CultureInfo.InvariantCulture));

        /// <summary>Screencast start with frame callback.</summary>
        public static Task<IAsyncDisposable> StartAsync(
            this IScreencast screencast,
            Func<ScreencastFrame, Task> onFrame,
            int quality = default,
            int width = default,
            int height = default,
            string path = default)
        {
            if (screencast is CRScreencast chromium)
            {
                return chromium.StartAsync(onFrame, quality, width, height, path);
            }

            if (screencast is WKScreencast webkit)
            {
                return webkit.StartAsync(onFrame, quality, width, height, path);
            }

            if (screencast is EmptyScreencast empty)
            {
                return empty.StartAsync(onFrame, quality, width, height, path);
            }

            return screencast.StartAsync(new ScreencastStartOptions
            {
                OnFrame = onFrame,
                Quality = quality == 0 ? null : quality,
                Path = path,
                Size = width > 0 || height > 0 ? new ScreencastSize { Width = width, Height = height } : null,
            });
        }

        /// <summary>Clock fixed time from unix milliseconds.</summary>
        public static Task SetFixedTimeAsync(this IClock clock, long time)
            => clock is Clock concrete
                ? concrete.SetFixedTimeAsync(time)
                : clock.SetFixedTimeAsync(DateTimeOffset.FromUnixTimeMilliseconds(time).UtcDateTime);

        /// <summary>Clock system time from unix milliseconds.</summary>
        public static Task SetSystemTimeAsync(this IClock clock, long time)
            => clock is Clock concrete
                ? concrete.SetSystemTimeAsync(time)
                : clock.SetSystemTimeAsync(DateTimeOffset.FromUnixTimeMilliseconds(time).UtcDateTime);

        /// <summary>Clock system time from an integer milliseconds value.</summary>
        public static Task SetSystemTimeAsync(this IClock clock, int time)
            => clock.SetSystemTimeAsync((long)time);

        /// <summary>Page-level CDP session helper.</summary>
        public static Task<ICDPSession> NewCDPSessionAsync(this IPage page)
            => page.Context.NewCDPSessionAsync(page);

        /// <summary>Browser-level CDP session helper.</summary>
        public static Task<ICDPSession> NewBrowserCDPSessionAsync(this IBrowser browser)
        {
            switch (browser)
            {
                case ChromiumBrowser chromium:
                    return chromium.NewBrowserCDPSessionAsync();
                case FirefoxBrowser firefox:
                    return firefox.NewBrowserCDPSessionAsync();
                case WKBrowser webkit:
                    return webkit.NewBrowserCDPSessionAsync();
                default:
                    throw new NotSupportedException("NewBrowserCDPSessionAsync requires a PlaywrightNative browser.");
            }
        }

        /// <summary>Send CDP with an anonymous/object payload.</summary>
        public static Task<JsonElement?> SendAsync(this ICDPSession session, string method, object args)
        {
            switch (session)
            {
                case CRCDPSession chromium:
                    return chromium.SendAsync(method, args);
                default:
                    return session.SendAsync(method, ObjectToDictionary(args));
            }
        }

        /// <summary>Nest a locator under the page root.</summary>
        public static ILocator Locator(this IPage page, ILocator locator)
            => page.Locator(":scope").Locator(locator);

        /// <summary>Request header values (not on official <see cref="IRequest"/>).</summary>
        public static async Task<IReadOnlyList<string>> HeaderValuesAsync(this IRequest request, string name)
        {
            if (request == null)
            {
                throw new ArgumentNullException(nameof(request));
            }

            IReadOnlyList<Header> headers = await request.HeadersArrayAsync().ConfigureAwait(false);
            List<string> values = new List<string>();
            foreach (Header header in headers)
            {
                if (string.Equals(header.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    values.Add(header.Value);
                }
            }

            return values;
        }

        /// <summary>Scroll the first matching element into view.</summary>
        public static Task ScrollIntoViewIfNeededAsync(
            this IPage page,
            string selector,
            LocatorScrollIntoViewIfNeededOptions options = default)
            => page.Locator(selector).ScrollIntoViewIfNeededAsync(options);

        /// <summary>Scroll the first matching element into view.</summary>
        public static Task ScrollIntoViewIfNeededAsync(
            this IFrame frame,
            string selector,
            LocatorScrollIntoViewIfNeededOptions options = default)
            => frame.Locator(selector).ScrollIntoViewIfNeededAsync(options);

        /// <summary>Worker console wait helper.</summary>
        public static Task<IConsoleMessage> WaitForConsoleMessageAsync(this IWorker worker, float? timeout = default)
            => WaitForEventHelper.WaitAsync<IConsoleMessage>(
                h => worker.Console += h,
                h => worker.Console -= h,
                _ => true,
                timeout,
                "worker.waitForEvent");

        /// <summary>Worker close wait helper.</summary>
        public static Task<IWorker> WaitForCloseAsync(this IWorker worker, float? timeout = default)
        {
            switch (worker)
            {
                case ChromiumWorker chromium:
                    return chromium.WaitForCloseAsync(timeout);
                case WebKitWorker webkit:
                    return webkit.WaitForCloseAsync(timeout);
                default:
                    return WaitForEventHelper.WaitAsync<IWorker>(
                        h => worker.Close += h,
                        h => worker.Close -= h,
                        _ => true,
                        timeout,
                        "worker.waitForEvent");
            }
        }

        /// <summary>Page errors buffer with filter.</summary>
        public static Task<IReadOnlyList<string>> PageErrorsAsync(this IPage page, PageErrorsFilter filter)
            => PageCompatDispatch.PageErrorsAsync(page, filter);

        /// <summary>Legacy binding that returns a value without a binding source.</summary>
        public static Task ExposeBindingAsync(this IPage page, string name, Func<object> callback)
            => page.ExposeBindingAsync(name, _ => callback());

        /// <summary>Legacy binding that returns a typed value without a binding source.</summary>
        /// <typeparam name="T">Return type.</typeparam>
        public static Task ExposeBindingAsync<T>(this IPage page, string name, Func<T> callback)
            => page.ExposeBindingAsync(name, _ => callback());

        private static Dictionary<string, object> ObjectToDictionary(object args)
        {
            if (args == null)
            {
                return null;
            }

            if (args is Dictionary<string, object> dictionary)
            {
                return dictionary;
            }

            Dictionary<string, object> result = new Dictionary<string, object>();
            foreach (System.Reflection.PropertyInfo property in args.GetType().GetProperties())
            {
                if (property.GetIndexParameters().Length == 0)
                {
                    result[property.Name] = property.GetValue(args);
                }
            }

            return result;
        }
    }
}
