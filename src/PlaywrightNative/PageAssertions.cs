// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// Default <see cref="IPageAssertions"/> that polls page title and URL.
    /// </summary>
    public sealed partial class PageAssertions : IPageAssertions
    {
        private readonly IPage _page;
        private readonly bool _negate;

        /// <summary>
        /// Initializes a new instance of the <see cref="PageAssertions"/> class.
        /// </summary>
        /// <param name="page">The page to assert against.</param>
        public PageAssertions(IPage page)
            : this(page, negate: false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="PageAssertions"/> class.
        /// </summary>
        /// <param name="page">The page to assert against.</param>
        /// <param name="negate">When <see langword="true"/>, invert each assertion.</param>
        public PageAssertions(IPage page, bool negate)
        {
            _page = page ?? throw new ArgumentNullException(nameof(page));
            _negate = negate;
        }

        /// <inheritdoc/>
        public IPageAssertions Not => new PageAssertions(_page, !_negate);

        /// <inheritdoc/>
        public Task ToHaveTitleAsync(string title, float? timeout = default)
        {
            if (title == null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            string last = string.Empty;
            return ExpectLabeledAsync(
                async () =>
                {
                    last = ExpectTextMatch.NormalizeWhiteSpace(
                        await _page.TitleAsync().ConfigureAwait(false) ?? string.Empty);
                    return last == ExpectTextMatch.NormalizeWhiteSpace(title);
                },
                timeout,
                "toHaveTitle",
                () => Task.FromResult(
                    (_negate ? "Expected: not \"" : "Expected: \"") +
                    title +
                    "\"\nReceived: \"" +
                    last +
                    "\""));
        }

        /// <inheritdoc/>
        public Task ToHaveTitleAsync(Regex title, float? timeout = default)
        {
            if (title == null)
            {
                throw new ArgumentNullException(nameof(title));
            }

            return ExpectBoolAsync(
                async () =>
                {
                    string actual = await _page.TitleAsync().ConfigureAwait(false) ?? string.Empty;
                    return title.IsMatch(actual);
                },
                timeout,
                "toHaveTitle");
        }

        /// <inheritdoc/>
        public Task ToHaveURLAsync(string url, float? timeout = default, bool? ignoreCase = default, AbortSignal signal = default)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            string last = string.Empty;
            return ExpectLabeledAsync(
                async () =>
                {
                    last = await CurrentUrlAsync().ConfigureAwait(false);
                    return ExpectTextMatch.Matches(last, url, exact: false, ignoreCase);
                },
                timeout,
                "toHaveURL",
                () => Task.FromResult(
                    (_negate ? "Expected: not \"" : "Expected: \"") +
                    url +
                    "\"\nReceived: \"" +
                    last +
                    "\""),
                signal,
                alreadyAbortedDetails: "Expected: " + System.Text.Json.JsonSerializer.Serialize(url) + "\n");
        }

        /// <inheritdoc/>
        public Task ToHaveURLAsync(Func<string, bool> predicate, float? timeout = default)
        {
            if (predicate == null)
            {
                throw new ArgumentNullException(nameof(predicate));
            }

            return ExpectLabeledAsync(
                async () =>
                {
                    string actual = await CurrentUrlAsync().ConfigureAwait(false);
                    return predicate(actual);
                },
                timeout,
                "toHaveURL",
                async () =>
                {
                    string actual = await CurrentUrlAsync().ConfigureAwait(false);
                    return (_negate ? "Expected: predicate to fail" : "Expected: predicate to succeed") +
                           "\nReceived: \"" + actual + "\"";
                });
        }

        /// <inheritdoc/>
        /// <inheritdoc/>
        public Task ToHaveURLAsync(object expected, float? timeout = default)
        {
            _ = timeout;
            throw ExpectUrlExpected.Invalid(expected);
        }

        /// <inheritdoc/>
        public Task ToHaveURLAsync(Regex url, float? timeout = default, bool? ignoreCase = default)
        {
            if (url == null)
            {
                throw new ArgumentNullException(nameof(url));
            }

            return ExpectBoolAsync(
                async () =>
                {
                    string actual = await CurrentUrlAsync().ConfigureAwait(false);
                    return ExpectTextMatch.Matches(actual, url, ignoreCase);
                },
                timeout,
                "toHaveURL");
        }

        /// <inheritdoc/>
        public Task ToMatchAriaSnapshotAsync(string expected, bool? exact = default, float? timeout = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return AriaSnapshotExpect.MatchAsync(
                _page,
                locator: null,
                root: null,
                expected,
                exact,
                timeout,
                _negate);
        }

        /// <inheritdoc/>
        public Task ToHaveScreenshotAsync(
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
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            ScreenshotComparer.ValidateTolerance(maxDiffPixels, maxDiffPixelRatio, threshold);
            return ExpectBoolAsync(
                () => ScreenshotComparer.MatchesAsync(
                    _page,
                    expected,
                    maxDiffPixels,
                    maxDiffPixelRatio,
                    threshold,
                    animations,
                    caret,
                    omitBackground,
                    mask,
                    maskColor),
                timeout,
                "toHaveScreenshot");
        }

        /// <inheritdoc/>
        public Task ToHaveScreenshotAsync(
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
        {
            if (path == null)
            {
                throw new ArgumentNullException(nameof(path));
            }

            byte[] expected = File.ReadAllBytes(path);
            return ToHaveScreenshotAsync(
                expected,
                maxDiffPixels,
                maxDiffPixelRatio,
                threshold,
                timeout,
                animations,
                caret,
                omitBackground,
                mask,
                maskColor);
        }

        private async Task<string> CurrentUrlAsync()
        {
            string actual = _page.Url ?? string.Empty;
            try
            {
                string live = await _page.EvaluateAsync<string>("location.href").ConfigureAwait(false);
                if (!string.IsNullOrEmpty(live))
                {
                    actual = live;
                }
            }
            catch (PlaywrightNativeException)
            {
                // Execution context is gone mid-navigation; keep polling.
            }

            return actual;
        }

        private string ApiName(string method)
            => _negate ? "expect.not." + method : "expect." + method;

        private Task ExpectBoolAsync(Func<Task<bool>> predicateAsync, float? timeout, string method)
            => ExpectLabeledAsync(predicateAsync, timeout, method, extraMessageAsync: null);

        private async Task ExpectLabeledAsync(
            Func<Task<bool>> predicateAsync,
            float? timeout,
            string method,
            Func<Task<string>> extraMessageAsync,
            AbortSignal signal = default,
            string alreadyAbortedDetails = null)
        {
            string header = _negate
                ? "expect(page).not." + method + "(expected) failed"
                : "expect(page)." + method + "(expected) failed";
            ExpectAbort.ThrowIfAlreadyAborted(
                signal,
                header,
                alreadyAbortedDetails ?? string.Empty);

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            System.Diagnostics.Stopwatch sw = System.Diagnostics.Stopwatch.StartNew();
            string expectLog = _negate ? "not " + method : method;

            while (true)
            {
                if (ExpectAbort.TryMidAbort(signal, out string abortReason))
                {
                    throw ExpectException.Fail(
                        header + "\n\n  - operation was aborted: " + abortReason + "\n",
                        actual: null,
                        expected: null,
                        method,
                        pass: false,
                        timeoutMs,
                        ariaSnapshot: null);
                }

                await LocatorHandlers.RunAsync(_page, timeout).ConfigureAwait(false);
                bool ok = await predicateAsync().ConfigureAwait(false);
                if (_negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    System.Text.StringBuilder log = new System.Text.StringBuilder();
                    log.Append(header);
                    log.Append('\n');
                    if (extraMessageAsync != null)
                    {
                        log.Append('\n');
                        log.Append(await extraMessageAsync().ConfigureAwait(false));
                    }

                    log.Append("\nTimeout:  ");
                    log.Append(timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    log.Append("ms\n\nCall log:\n  - Expect \"");
                    log.Append(expectLog);
                    log.Append("\" with timeout ");
                    log.Append(timeoutMs.ToString(System.Globalization.CultureInfo.InvariantCulture));
                    log.Append("ms\n");
                    string ariaSnapshot = null;
                    if (string.Equals(method, "toHaveTitle", StringComparison.Ordinal)
                        || string.Equals(method, "toHaveURL", StringComparison.Ordinal))
                    {
                        try
                        {
                            ariaSnapshot = await _page.AriaSnapshotAsync(timeout: 1000).ConfigureAwait(false);
                        }
                        catch (Exception ex) when (ex is PlaywrightNativeException || ex is TimeoutException)
                        {
                        }
                    }

                    throw ExpectException.Fail(
                        log.ToString(),
                        actual: null,
                        expected: null,
                        method,
                        pass: _negate,
                        timeoutMs,
                        ariaSnapshot);
                }

                await ExpectAbort.DelayOrAbortAsync(signal).ConfigureAwait(false);
            }
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IPageAssertions.ToHaveTitleAsync(string titleOrRegExp, PageAssertionsToHaveTitleOptions options) => Task.CompletedTask;

        Task IPageAssertions.ToHaveTitleAsync(Regex titleOrRegExp, PageAssertionsToHaveTitleOptions options) => Task.CompletedTask;

        Task IPageAssertions.ToHaveURLAsync(string urlOrRegExp, PageAssertionsToHaveURLOptions options) => Task.CompletedTask;

        Task IPageAssertions.ToHaveURLAsync(Regex urlOrRegExp, PageAssertionsToHaveURLOptions options) => Task.CompletedTask;

        Task IPageAssertions.ToMatchAriaSnapshotAsync(string expected, PageAssertionsToMatchAriaSnapshotOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
