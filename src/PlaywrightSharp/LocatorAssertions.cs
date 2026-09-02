// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightSharp.Helpers;

namespace PlaywrightSharp
{
    /// <summary>
    /// Default <see cref="ILocatorAssertions"/> that polls locator queries.
    /// </summary>
    public sealed partial class LocatorAssertions : ILocatorAssertions
    {
        private const string ElementPreviewFunction = ElementStateScript.PreviewNodeFunction;

        private const string CollectVisibleTextFunction = @"el => {
  const out = [];
  const walk = (n) => {
    if (n.nodeType === 3) {
      const t = String(n.textContent || '').trim();
      if (t) out.push(t);
    } else if (n.nodeType === 1) {
      for (let i = 0; i < n.childNodes.length; i++) walk(n.childNodes[i]);
    }
  };
  walk(el);
  return out;
}";

        private readonly ILocator _locator;
        private readonly bool _negate;

        /// <summary>
        /// Initializes a new instance of the <see cref="LocatorAssertions"/> class.
        /// </summary>
        /// <param name="locator">The locator to assert against.</param>
        public LocatorAssertions(ILocator locator)
            : this(locator, negate: false)
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="LocatorAssertions"/> class.
        /// </summary>
        /// <param name="locator">The locator to assert against.</param>
        /// <param name="negate">When <see langword="true"/>, invert each assertion.</param>
        public LocatorAssertions(ILocator locator, bool negate)
        {
            _locator = locator ?? throw new ArgumentNullException(nameof(locator));
            _negate = negate;
        }

        private enum ExpectSnapshotKind
        {
            None,
            Property,
            Containment,
            Page,
        }

        /// <inheritdoc/>
        public ILocatorAssertions Not => new LocatorAssertions(_locator, !_negate);

        /// <inheritdoc/>
        public async Task ToBeVisibleAsync(float? timeout = default, bool? visible = default, AbortSignal signal = default)
        {
            ExpectAbort.ThrowIfAlreadyAborted(
                signal,
                _negate ? "expect(locator).not.toBeVisible() failed" : "expect(locator).toBeVisible() failed",
                "Locator: " + _locator + "\nExpected: " + (_negate ? "not visible" : "visible") + "\n");

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            int resolved = 0;
            string preview = null;
            bool wantVisible = visible != false;

            while (true)
            {
                if (ExpectAbort.TryMidAbort(signal, out string abortReason))
                {
                    throw CreateExpectException(
                        FormatVisibleAbortFailure(timeoutMs, resolved, abortReason),
                        resolved > 0 ? (wantVisible ? "hidden" : "visible") : null,
                        "visible",
                        "toBeVisible",
                        pass: false,
                        timeoutMs,
                        ariaSnapshot: null);
                }

                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all;
                try
                {
                    all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                }
                catch (PlaywrightSharpException ex) when (IsSelectorSyntaxError(ex))
                {
                    throw FormatVisibleSelectorError(ex);
                }

                if (all.Count > 1)
                {
                    throw new PlaywrightSharpException(
                        await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                }

                bool isVisible = false;
                if (all.Count == 1)
                {
                    resolved++;
                    try
                    {
                        preview = await all[0].EvaluateAsync<string>(ElementPreviewFunction).ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        preview = "element";
                    }

                    try
                    {
                        isVisible = await all[0].IsVisibleAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        isVisible = false;
                    }
                }

                bool ok = wantVisible ? isVisible : !isVisible;
                if (_negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    ExpectSnapshotKind snapshotKind = resolved == 0 || !isVisible
                        ? ExpectSnapshotKind.Page
                        : ExpectSnapshotKind.Property;
                    throw CreateExpectException(
                        FormatVisibleExpectFailure(timeoutMs, resolved, isVisible, preview),
                        resolved > 0 ? (isVisible ? "visible" : "hidden") : null,
                        "visible",
                        "toBeVisible",
                        pass: _negate && isVisible,
                        timeoutMs,
                        await CaptureExpectAriaSnapshotAsync(snapshotKind).ConfigureAwait(false));
                }

                await ExpectAbort.DelayOrAbortAsync(signal).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task ToBeHiddenAsync(float? timeout = default)
        {
            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            string preview = null;
            int resolved = 0;

            while (true)
            {
                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                if (all.Count > 1)
                {
                    throw new PlaywrightSharpException(
                        await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                }

                bool isHidden = true;
                if (all.Count == 1)
                {
                    resolved++;
                    try
                    {
                        preview = await all[0].EvaluateAsync<string>(ElementStateScript.PreviewNodeFunction)
                            .ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        preview = "element";
                    }

                    try
                    {
                        isHidden = await all[0].IsHiddenAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        isHidden = true;
                    }
                }

                bool ok = isHidden;
                if (_negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(_negate
                        ? "expect(locator).not.toBeHidden() failed"
                        : "expect(locator).toBeHidden() failed");
                    log.Append("\n\nLocator: ");
                    log.Append(_locator);
                    log.Append("\nExpected: ");
                    log.Append(_negate ? "not hidden" : "hidden");
                    log.Append("\nTimeout: ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms");
                    if (resolved == 0)
                    {
                        log.Append("\nError: element(s) not found");
                    }

                    log.Append("\n\nCall log:\n  - Expect \"");
                    log.Append(_negate ? "not toBeHidden" : "toBeHidden");
                    log.Append("\" with timeout ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms\n");
                    if (resolved > 0)
                    {
                        log.Append("locator resolved to ");
                        log.Append(preview);
                        log.Append('\n');
                    }

                    throw new TimeoutException(log.ToString());
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public async Task ToBeAttachedAsync(float? timeout = default, bool? attached = default)
        {
            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            bool wantAttached = attached != false;
            string preview = null;
            int resolved = 0;

            while (true)
            {
                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                if (all.Count > 1)
                {
                    throw new PlaywrightSharpException(
                        await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                }

                bool isAttached = all.Count > 0;
                if (isAttached)
                {
                    resolved++;
                    try
                    {
                        preview = await all[0].EvaluateAsync<string>(ElementStateScript.PreviewNodeFunction)
                            .ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        preview = "element";
                    }
                }

                bool ok = wantAttached ? isAttached : !isAttached;
                if (_negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(_negate
                        ? "expect(locator).not.toBeAttached() failed"
                        : "expect(locator).toBeAttached() failed");
                    log.Append("\n\nLocator: ");
                    log.Append(_locator);
                    log.Append("\nTimeout: ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms");
                    if (resolved == 0)
                    {
                        log.Append("\nError: element(s) not found");
                    }

                    log.Append("\n\nCall log:\n  - Expect \"");
                    log.Append(_negate ? "not toBeAttached" : "toBeAttached");
                    log.Append("\" with timeout ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms\n  - waiting for ");
                    log.Append(_locator);
                    log.Append('\n');
                    if (resolved > 0)
                    {
                        log.Append("locator resolved to ");
                        log.Append(preview);
                        log.Append('\n');
                    }

                    log.Append("expect.toBeAttached: Timeout ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms exceeded.\n");
                    ExpectSnapshotKind snapshotKind = resolved == 0
                        ? ExpectSnapshotKind.Page
                        : ExpectSnapshotKind.Property;
                    throw CreateExpectException(
                        log.ToString(),
                        actual: null,
                        expected: null,
                        "toBeAttached",
                        pass: _negate && isAttached,
                        timeoutMs,
                        await CaptureExpectAriaSnapshotAsync(snapshotKind).ConfigureAwait(false));
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task ToBeFocusedAsync(float? timeout = default)
            => ExpectBoolAsync(
                () => UniqueStateAsync(handle => handle.EvaluateAsync<bool>(ElementStateScript.IsFocusedFunction)),
                timeout,
                "toBeFocused");

        /// <inheritdoc/>
        public async Task ToBeEnabledAsync(float? timeout = default, bool? enabled = default)
        {
            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            bool wantEnabled = enabled != false;
            string preview = null;
            int resolved = 0;

            while (true)
            {
                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                if (all.Count > 1)
                {
                    throw new PlaywrightSharpException(
                        await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                }

                bool isEnabled = false;
                if (all.Count == 1)
                {
                    resolved++;
                    try
                    {
                        preview = await all[0].EvaluateAsync<string>(ElementStateScript.PreviewNodeFunction)
                            .ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        preview = "element";
                    }

                    try
                    {
                        isEnabled = await all[0].IsEnabledAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        isEnabled = false;
                    }
                }

                bool ok = wantEnabled ? isEnabled : !isEnabled;
                if (_negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(ApiName("toBeEnabled"));
                    log.Append(": Timeout ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms exceeded.\n");
                    if (resolved > 0)
                    {
                        log.Append("locator resolved to ");
                        log.Append(preview);
                        log.Append('\n');
                    }

                    throw new TimeoutException(log.ToString());
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task ToBeDisabledAsync(float? timeout = default)
            => ExpectBoolAsync(() => UniqueStateAsync(h => h.IsDisabledAsync()), timeout, "toBeDisabled");

        /// <inheritdoc/>
        public Task ToBeEditableAsync(float? timeout = default, bool? editable = default)
            => ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    bool isEditable = await handle.IsEditableAsync().ConfigureAwait(false);
                    return editable == false ? !isEditable : isEditable;
                }),
                timeout,
                "toBeEditable");

        /// <inheritdoc/>
        public Task ToBeCheckedAsync(float? timeout = default, bool? @checked = default, bool? indeterminate = default)
        {
            if (indeterminate == true && @checked.HasValue)
            {
                throw new ArgumentException("Can't assert indeterminate and checked at the same time");
            }

            return WaitAsync();

            async Task WaitAsync()
            {
                Dictionary<string, object> spec = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["checked"] = @checked,
                    ["indeterminate"] = indeterminate,
                };
                string expected = indeterminate == true
                    ? "indeterminate"
                    : @checked == false ? "unchecked" : "checked";
                if (_negate)
                {
                    expected = "not " + expected;
                }

                string call = "toBeChecked()";
                if (indeterminate == true)
                {
                    call = "toBeChecked({ indeterminate: true })";
                }
                else if (@checked == false)
                {
                    call = "toBeChecked({ checked: false })";
                }

                string header = _negate
                    ? "expect(locator).not." + call + " failed"
                    : "expect(locator)." + call + " failed";
                string expectLog = _negate ? "not toBeChecked" : "toBeChecked";
                int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
                Stopwatch sw = Stopwatch.StartNew();
                string preview = null;
                string received = null;
                bool sawElement = false;

                while (true)
                {
                    await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                    IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                    if (all.Count > 1)
                    {
                        throw new PlaywrightSharpException(
                            await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                    }

                    bool matched = false;
                    if (all.Count == 1)
                    {
                        sawElement = true;
                        try
                        {
                            preview = await all[0].EvaluateAsync<string>(ElementStateScript.PreviewNodeFunction)
                                .ConfigureAwait(false);
                        }
                        catch (PlaywrightSharpException)
                        {
                            preview = "element";
                        }

                        try
                        {
                            received = await all[0].EvaluateAsync<string>(ElementStateScript.CheckedReceivedFunction)
                                .ConfigureAwait(false);
                            matched = await all[0]
                                .EvaluateAsync<bool>(ElementStateScript.MatchesCheckedStateFunction, spec)
                                .ConfigureAwait(false);
                        }
                        catch (PlaywrightSharpException)
                        {
                            matched = false;
                        }
                    }

                    // Missing elements fail both toBeChecked and not.toBeChecked.
                    bool ok = all.Count == 0 ? false : (_negate ? !matched : matched);
                    if (ok)
                    {
                        return;
                    }

                    if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                    {
                        StringBuilder log = new StringBuilder();
                        log.Append(header);
                        log.Append("\n\nLocator:");
                        log.Append(sawElement ? "  " : " ");
                        log.Append(_locator);
                        log.Append("\nExpected: ");
                        log.Append(expected);
                        if (sawElement && !string.IsNullOrEmpty(received))
                        {
                            log.Append("\nReceived: ");
                            log.Append(received);
                        }

                        log.Append("\nTimeout:");
                        log.Append(sawElement ? "  " : " ");
                        log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                        log.Append("ms");
                        if (!sawElement)
                        {
                            log.Append("\nError: element(s) not found");
                        }

                        log.Append("\n\nCall log:\n  - Expect \"");
                        log.Append(expectLog);
                        log.Append("\" with timeout ");
                        log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                        log.Append("ms\n");
                        if (!sawElement)
                        {
                            log.Append("  - waiting for ");
                            log.Append(_locator);
                            log.Append('\n');
                        }

                        if (sawElement)
                        {
                            log.Append("  locator resolved to ");
                            log.Append(preview);
                            log.Append("\n  unexpected value \"");
                            log.Append(received);
                            log.Append("\"\n");
                        }

                        string matcherExpected = "checked";
                        if (indeterminate == true)
                        {
                            matcherExpected = "indeterminate";
                        }
                        else if (@checked == false)
                        {
                            matcherExpected = "unchecked";
                        }

                        throw CreateExpectException(
                            log.ToString(),
                            sawElement ? received : null,
                            matcherExpected,
                            "toBeChecked",
                            pass: _negate && sawElement && matched,
                            timeoutMs,
                            await CaptureExpectAriaSnapshotAsync(ExpectSnapshotKind.Property).ConfigureAwait(false));
                    }

                    await Task.Delay(50).ConfigureAwait(false);
                }
            }
        }

        /// <inheritdoc/>
        public Task ToBeEmptyAsync(float? timeout = default)
            => ExpectBoolAsync(
                () => UniqueStateAsync(handle => handle.EvaluateAsync<bool>(ElementStateScript.IsEmptyFunction)),
                timeout,
                "toBeEmpty");

        /// <inheritdoc/>
        public async Task ToHaveCountAsync(int count, float? timeout = default, AbortSignal signal = default)
        {
            ArgumentOutOfRangeException.ThrowIfNegative(count);
            string header = _negate
                ? "expect(locator).not.toHaveCount(expected) failed"
                : "expect(locator).toHaveCount(expected) failed";
            ExpectAbort.ThrowIfAlreadyAborted(
                signal,
                header,
                "Locator:  " + _locator + "\nExpected: " + count.ToString(CultureInfo.InvariantCulture) + "\n");

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            int last = -1;
            string expectLog = _negate ? "not toHaveCount" : "toHaveCount";

            while (true)
            {
                if (ExpectAbort.TryMidAbort(signal, out string abortReason))
                {
                    throw CreateExpectException(
                        header + "\n\n  - operation was aborted: " + abortReason + "\n",
                        last,
                        count,
                        "toHaveCount",
                        pass: false,
                        timeoutMs,
                        ariaSnapshot: null);
                }

                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                last = await _locator.CountAsync().ConfigureAwait(false);
                bool match = last == count;
                if (_negate ? !match : match)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(header);
                    log.Append("\n\nLocator:  ");
                    log.Append(_locator);
                    log.Append("\nExpected: ");
                    if (_negate)
                    {
                        log.Append("not ");
                    }

                    log.Append(count.ToString(CultureInfo.InvariantCulture));
                    log.Append("\nReceived: ");
                    log.Append(last.ToString(CultureInfo.InvariantCulture));
                    log.Append("\nTimeout:  ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms\n\nCall log:\n  - Expect \"");
                    log.Append(expectLog);
                    log.Append("\" with timeout ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms\n  - waiting for ");
                    log.Append(_locator);
                    log.Append("\n  locator resolved to ");
                    log.Append(last.ToString(CultureInfo.InvariantCulture));
                    log.Append(last == 1 ? " element" : " elements");
                    log.Append("\n  unexpected value \"");
                    log.Append(last.ToString(CultureInfo.InvariantCulture));
                    log.Append("\"\n");
                    throw CreateExpectException(
                        log.ToString(),
                        last,
                        count,
                        "toHaveCount",
                        pass: _negate,
                        timeoutMs,
                        ariaSnapshot: null);
                }

                await ExpectAbort.DelayOrAbortAsync(signal).ConfigureAwait(false);
            }
        }

        /// <inheritdoc/>
        public Task ToHaveTextAsync(string expected, bool? exact = default, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default, AbortSignal signal = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return ExpectTextCoreAsync(
                new[] { new ExpectTextNeedle(expected) },
                exact: exact != false,
                requireLength: true,
                single: true,
                timeout,
                ignoreCase,
                useInnerText,
                "toHaveText",
                signal);
        }

        /// <inheritdoc/>
        public Task ToHaveTextAsync(Regex expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return ExpectTextCoreAsync(
                new[] { new ExpectTextNeedle(expected) },
                exact: true,
                requireLength: true,
                single: true,
                timeout,
                ignoreCase,
                useInnerText,
                "toHaveText");
        }

        /// <inheritdoc/>
        public Task ToHaveTextAsync(IEnumerable<string> expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            return ExpectTextCoreAsync(
                ExpectTextMatch.NeedlesFromStrings(expected),
                exact: true,
                requireLength: true,
                single: false,
                timeout,
                ignoreCase,
                useInnerText,
                "toHaveText");
        }

        /// <inheritdoc/>
        public Task ToHaveTextAsync(IEnumerable<Regex> expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            return ExpectTextCoreAsync(
                ExpectTextMatch.NeedlesFromRegex(expected),
                exact: true,
                requireLength: true,
                single: false,
                timeout,
                ignoreCase,
                useInnerText,
                "toHaveText");
        }

        /// <inheritdoc/>
        public Task ToHaveTextAsync(IEnumerable<object> expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            return ExpectTextCoreAsync(
                ExpectTextMatch.NeedlesFromObjects(expected),
                exact: true,
                requireLength: true,
                single: false,
                timeout,
                ignoreCase,
                useInnerText,
                "toHaveText");
        }

        /// <inheritdoc/>
        public Task ToContainTextAsync(string expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return ExpectTextCoreAsync(
                new[] { new ExpectTextNeedle(expected) },
                exact: false,
                requireLength: false,
                single: true,
                timeout,
                ignoreCase,
                useInnerText,
                "toContainText");
        }

        /// <inheritdoc/>
        public Task ToContainTextAsync(Regex expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return ExpectTextCoreAsync(
                new[] { new ExpectTextNeedle(expected) },
                exact: false,
                requireLength: false,
                single: true,
                timeout,
                ignoreCase,
                useInnerText,
                "toContainText");
        }

        /// <inheritdoc/>
        public Task ToContainTextAsync(IEnumerable<string> expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            return ExpectTextCoreAsync(
                ExpectTextMatch.NeedlesFromStrings(expected),
                exact: false,
                requireLength: false,
                single: false,
                timeout,
                ignoreCase,
                useInnerText,
                "toContainText");
        }

        /// <inheritdoc/>
        public Task ToContainTextAsync(IEnumerable<Regex> expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            return ExpectTextCoreAsync(
                ExpectTextMatch.NeedlesFromRegex(expected),
                exact: false,
                requireLength: false,
                single: false,
                timeout,
                ignoreCase,
                useInnerText,
                "toContainText");
        }

        /// <inheritdoc/>
        public Task ToContainTextAsync(IEnumerable<object> expected, float? timeout = default, bool? ignoreCase = default, bool? useInnerText = default)
        {
            return ExpectTextCoreAsync(
                ExpectTextMatch.NeedlesFromObjects(expected),
                exact: false,
                requireLength: false,
                single: false,
                timeout,
                ignoreCase,
                useInnerText,
                "toContainText");
        }

        /// <inheritdoc/>
        public Task ToHaveAttributeAsync(string name, float? timeout = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                    await handle.GetAttributeAsync(name).ConfigureAwait(false) != null),
                timeout,
                "toHaveAttribute");
        }

        /// <inheritdoc/>
        public Task ToHaveAttributeAsync(string name, string value, float? timeout = default, bool? ignoreCase = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            string lastActual = string.Empty;
            bool lastMissing = true;
            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.GetAttributeAsync(name).ConfigureAwait(false);
                    lastMissing = actual == null;
                    lastActual = actual ?? string.Empty;
                    return actual != null && ExpectTextMatch.Matches(actual, value, exact: true, ignoreCase);
                }),
                timeout,
                "toHaveAttribute",
                _ => FormatAttributeStringExtra(value, lastActual, lastMissing));
        }

        /// <inheritdoc/>
        public Task ToHaveAttributeAsync(string name, Regex value, float? timeout = default, bool? ignoreCase = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            string lastActual = string.Empty;
            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.GetAttributeAsync(name).ConfigureAwait(false);
                    lastActual = actual ?? string.Empty;
                    return actual != null && ExpectTextMatch.Matches(actual, value, ignoreCase);
                }),
                timeout,
                "toHaveAttribute",
                timeoutMs => FormatAttributeRegexExtra(value, lastActual, timeoutMs));
        }

        /// <inheritdoc/>
        public Task ToHaveValueAsync(string value, float? timeout = default)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return ExpectValueCoreAsync(new ExpectTextNeedle(value), timeout);
        }

        /// <inheritdoc/>
        public Task ToHaveValueAsync(Regex value, float? timeout = default)
        {
            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return ExpectValueCoreAsync(new ExpectTextNeedle(value), timeout);
        }

        /// <inheritdoc/>
        public Task ToHaveValuesAsync(IEnumerable<string> values, float? timeout = default)
            => ExpectValuesCoreAsync(ExpectTextMatch.NeedlesFromStrings(values), timeout);

        /// <inheritdoc/>
        public Task ToHaveValuesAsync(IEnumerable<Regex> values, float? timeout = default)
            => ExpectValuesCoreAsync(ExpectTextMatch.NeedlesFromRegex(values), timeout);

        /// <inheritdoc/>
        public Task ToHaveIdAsync(string id, float? timeout = default)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.GetAttributeAsync("id").ConfigureAwait(false);
                    return actual == id;
                }),
                timeout,
                "toHaveId");
        }

        /// <inheritdoc/>
        public Task ToHaveIdAsync(Regex id, float? timeout = default)
        {
            if (id == null)
            {
                throw new ArgumentNullException(nameof(id));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.GetAttributeAsync("id").ConfigureAwait(false) ?? string.Empty;
                    return id.IsMatch(actual);
                }),
                timeout,
                "toHaveId");
        }

        /// <inheritdoc/>
        public Task ToHaveClassAsync(string className, float? timeout = default)
        {
            if (className == null)
            {
                throw new ArgumentNullException(nameof(className));
            }

            string lastClass = string.Empty;
            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    lastClass = await handle.GetAttributeAsync("class").ConfigureAwait(false) ?? string.Empty;
                    return string.Equals(lastClass, className, StringComparison.Ordinal);
                }),
                timeout,
                "toHaveClass",
                _ => FormatQuotedExpectedReceived(className, lastClass));
        }

        /// <inheritdoc/>
        public Task ToHaveClassAsync(Regex className, float? timeout = default)
        {
            if (className == null)
            {
                throw new ArgumentNullException(nameof(className));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.GetAttributeAsync("class").ConfigureAwait(false) ?? string.Empty;
                    return className.IsMatch(actual);
                }),
                timeout,
                "toHaveClass");
        }

        /// <inheritdoc/>
        public Task ToHaveClassAsync(IEnumerable<string> classNames, float? timeout = default)
        {
            if (classNames == null)
            {
                throw new ArgumentNullException(nameof(classNames));
            }

            string[] expected = classNames as string[] ?? new List<string>(classNames).ToArray();
            return ExpectBoolAsync(
                async () =>
                {
                    string[] actual = await CollectClassAttributesAsync().ConfigureAwait(false);
                    if (actual.Length != expected.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < expected.Length; i++)
                    {
                        if (actual[i] != expected[i])
                        {
                            return false;
                        }
                    }

                    return true;
                },
                timeout,
                "toHaveClass");
        }

        /// <inheritdoc/>
        public Task ToHaveClassAsync(IEnumerable<Regex> classNames, float? timeout = default)
        {
            if (classNames == null)
            {
                throw new ArgumentNullException(nameof(classNames));
            }

            Regex[] expected = classNames as Regex[] ?? new List<Regex>(classNames).ToArray();
            for (int i = 0; i < expected.Length; i++)
            {
                if (expected[i] == null)
                {
                    throw new ArgumentNullException(nameof(classNames));
                }
            }

            return ExpectBoolAsync(
                async () =>
                {
                    string[] actual = await CollectClassAttributesAsync().ConfigureAwait(false);
                    if (actual.Length != expected.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < expected.Length; i++)
                    {
                        if (!expected[i].IsMatch(actual[i]))
                        {
                            return false;
                        }
                    }

                    return true;
                },
                timeout,
                "toHaveClass");
        }

        /// <inheritdoc/>
        public Task ToHaveClassAsync(IEnumerable<object> classNames, float? timeout = default)
        {
            ExpectTextNeedle[] needles = ExpectTextMatch.NeedlesFromObjects(classNames);
            return ExpectBoolAsync(
                async () =>
                {
                    string[] actual = await CollectClassAttributesAsync().ConfigureAwait(false);
                    return ExpectTextMatch.MatchesSequence(
                        actual,
                        needles,
                        requireLength: true,
                        exact: true,
                        ignoreCase: null);
                },
                timeout,
                "toHaveClass");
        }

        /// <inheritdoc/>
        public Task ToContainClassAsync(Regex classNames, float? timeout = default)
        {
            throw new ArgumentException("\"expected\" argument in toContainClass cannot be a RegExp value");
        }

        /// <inheritdoc/>
        public Task ToContainClassAsync(IEnumerable<object> classNames, float? timeout = default)
        {
            if (classNames == null)
            {
                throw new ArgumentNullException(nameof(classNames));
            }

            List<object> items = new List<object>(classNames);
            List<string> tokens = new List<string>(items.Count);
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i] is Regex)
                {
                    throw new ArgumentException("\"expected\" argument in toContainClass cannot contain RegExp values");
                }

                tokens.Add(items[i] as string ?? string.Empty);
            }

            return ToContainClassAsync(tokens, timeout);
        }

        /// <inheritdoc/>
        public Task ToContainClassAsync(string classNames, float? timeout = default)
        {
            if (classNames == null)
            {
                throw new ArgumentNullException(nameof(classNames));
            }

            string lastClass = string.Empty;
            return ExpectBoolAsync(
                async () =>
                {
                    bool ok = await UniqueStateAsync(handle => handle.EvaluateAsync<bool>(ElementStateScript.ContainsClassFunction, classNames))
                        .ConfigureAwait(false);
                    IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                    if (all.Count == 1)
                    {
                        lastClass = await all[0].GetAttributeAsync("class").ConfigureAwait(false) ?? string.Empty;
                    }

                    return ok;
                },
                timeout,
                "toContainClass",
                _ => FormatQuotedExpectedReceived(classNames, lastClass));
        }

        /// <inheritdoc/>
        public Task ToContainClassAsync(IEnumerable<string> classNames, float? timeout = default)
        {
            if (classNames == null)
            {
                throw new ArgumentNullException(nameof(classNames));
            }

            string[] expected = classNames as string[] ?? new List<string>(classNames).ToArray();
            return ExpectBoolAsync(
                async () =>
                {
                    IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                    if (all.Count != expected.Length)
                    {
                        return false;
                    }

                    for (int i = 0; i < expected.Length; i++)
                    {
                        bool ok = await all[i].EvaluateAsync<bool>(ElementStateScript.ContainsClassFunction, expected[i])
                            .ConfigureAwait(false);
                        if (!ok)
                        {
                            return false;
                        }
                    }

                    return true;
                },
                timeout,
                "toContainClass");
        }

        /// <inheritdoc/>
        public Task ToHaveCSSAsync(string name, string value, float? timeout = default, string pseudo = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await ReadCssAsync(handle, name, pseudo).ConfigureAwait(false);
                    return actual == value;
                }),
                timeout,
                "toHaveCSS");
        }

        /// <inheritdoc/>
        public Task ToHaveCSSAsync(string name, Regex value, float? timeout = default, string pseudo = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            if (value == null)
            {
                throw new ArgumentNullException(nameof(value));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await ReadCssAsync(handle, name, pseudo).ConfigureAwait(false) ?? string.Empty;
                    return value.IsMatch(actual);
                }),
                timeout,
                "toHaveCSS");
        }

        /// <inheritdoc/>
        public Task ToHaveJSPropertyAsync(string name, object value, float? timeout = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            Dictionary<string, object> spec = new Dictionary<string, object>(StringComparer.Ordinal)
            {
                ["name"] = name,
                ["expected"] = value,
            };
            string lastPrinted = "undefined";
            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    lastPrinted = await handle.EvaluateAsync<string>(ElementStateScript.ReadJSPropertyPrintedFunction, name)
                        .ConfigureAwait(false) ?? "undefined";
                    return await handle.EvaluateAsync<bool>(ElementStateScript.HasJSPropertyFunction, spec)
                        .ConfigureAwait(false);
                }),
                timeout,
                "toHaveJSProperty",
                _ => FormatJsPropertyExtra(value, lastPrinted));
        }

        /// <inheritdoc/>
        public Task ToBeInViewportAsync(float? ratio = default, float? timeout = default)
        {
            if (ratio.HasValue && (ratio.Value < 0f || ratio.Value > 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(ratio), "Viewport ratio must be between 0 and 1.");
            }

            float need = ratio ?? 0f;
            return ExpectBoolAsync(
                () => UniqueStateAsync(handle => handle.EvaluateAsync<bool>(ElementStateScript.IsInViewportFunction, need)),
                timeout,
                "toBeInViewport");
        }

        /// <inheritdoc/>
        public Task ToHaveRoleAsync(string role, float? timeout = default)
        {
            if (role == null)
            {
                throw new ArgumentNullException(nameof(role));
            }

            return ExpectBoolAsync(
                () => UniqueAriaAsync(snapshot =>
                    string.Equals(snapshot.Role, role, StringComparison.Ordinal)),
                timeout,
                "toHaveRole");
        }

        /// <inheritdoc/>
        public Task ToHaveRoleAsync(AriaRole role, float? timeout = default)
            => ToHaveRoleAsync(role.ToRoleString(), timeout);

        /// <inheritdoc/>
        public Task ToHaveAccessibleNameAsync(string name, bool? exact = default, float? timeout = default, bool? ignoreCase = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.EvaluateAsync<string>(ElementStateScript.AccessibleNameFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    return ExpectTextMatch.Matches(
                        ExpectTextMatch.NormalizeWhiteSpace(actual),
                        ExpectTextMatch.NormalizeWhiteSpace(name),
                        exact == true,
                        ignoreCase);
                }),
                timeout,
                "toHaveAccessibleName");
        }

        /// <inheritdoc/>
        public Task ToHaveAccessibleNameAsync(Regex name, float? timeout = default, bool? ignoreCase = default)
        {
            if (name == null)
            {
                throw new ArgumentNullException(nameof(name));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.EvaluateAsync<string>(ElementStateScript.AccessibleNameFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    return ExpectTextMatch.Matches(actual, name, ignoreCase);
                }),
                timeout,
                "toHaveAccessibleName");
        }

        /// <inheritdoc/>
        public Task ToHaveAccessibleDescriptionAsync(string description, bool? exact = default, float? timeout = default, bool? ignoreCase = default)
        {
            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.EvaluateAsync<string>(ElementStateScript.AccessibleDescriptionFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    return ExpectTextMatch.Matches(
                        ExpectTextMatch.NormalizeWhiteSpace(actual),
                        ExpectTextMatch.NormalizeWhiteSpace(description),
                        exact == true,
                        ignoreCase);
                }),
                timeout,
                "toHaveAccessibleDescription");
        }

        /// <inheritdoc/>
        public Task ToHaveAccessibleDescriptionAsync(Regex description, float? timeout = default, bool? ignoreCase = default)
        {
            if (description == null)
            {
                throw new ArgumentNullException(nameof(description));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.EvaluateAsync<string>(ElementStateScript.AccessibleDescriptionFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    return ExpectTextMatch.Matches(actual, description, ignoreCase);
                }),
                timeout,
                "toHaveAccessibleDescription");
        }

        /// <inheritdoc/>
        public Task ToHaveAccessibleErrorMessageAsync(string message, bool? exact = default, float? timeout = default, bool? ignoreCase = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.EvaluateAsync<string>(ElementStateScript.AccessibleErrorMessageFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    return ExpectTextMatch.Matches(actual, message, exact == true, ignoreCase);
                }),
                timeout,
                "toHaveAccessibleErrorMessage");
        }

        /// <inheritdoc/>
        public Task ToHaveAccessibleErrorMessageAsync(Regex message, float? timeout = default, bool? ignoreCase = default)
        {
            if (message == null)
            {
                throw new ArgumentNullException(nameof(message));
            }

            return ExpectBoolAsync(
                () => UniqueStateAsync(async handle =>
                {
                    string actual = await handle.EvaluateAsync<string>(ElementStateScript.AccessibleErrorMessageFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    return ExpectTextMatch.Matches(actual, message, ignoreCase);
                }),
                timeout,
                "toHaveAccessibleErrorMessage");
        }

        /// <inheritdoc/>
        public Task ToMatchAriaSnapshotAsync(string expected, bool? exact = default, float? timeout = default, AbortSignal signal = default)
        {
            if (expected == null)
            {
                throw new ArgumentNullException(nameof(expected));
            }

            return AriaSnapshotExpect.MatchAsync(
                _locator.Page,
                _locator,
                root: null,
                expected,
                exact,
                timeout,
                _negate,
                signal);
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
                () => UniqueStateAsync(handle => ScreenshotComparer.MatchesAsync(
                    handle,
                    expected,
                    maxDiffPixels,
                    maxDiffPixelRatio,
                    threshold,
                    animations,
                    caret,
                    omitBackground,
                    mask,
                    maskColor)),
                timeout,
                "toHaveScreenshot",
                extraMessage: null,
                ExpectSnapshotKind.None);
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

        /// <inheritdoc/>
        public Task ToPassAsync(Func<Task> assertion, float? timeout = default)
        {
            if (assertion == null)
            {
                throw new ArgumentNullException(nameof(assertion));
            }

            return ExpectBoolAsync(
                async () =>
                {
                    try
                    {
                        await assertion().ConfigureAwait(false);
                        return true;
                    }
                    catch (Exception)
                    {
                        return false;
                    }
                },
                timeout,
                "toPass");
        }

        private static bool IsSelectorSyntaxError(Exception ex)
        {
            string message = ex?.Message ?? string.Empty;
            return message.Contains("Unexpected token", StringComparison.Ordinal)
                || message.Contains("Unknown engine", StringComparison.Ordinal)
                || message.Contains("Malformed selector", StringComparison.Ordinal)
                || message.Contains("Unclosed quote", StringComparison.Ordinal);
        }

        private static string FormatExpectedJsValue(object expected)
        {
            if (expected == null)
            {
                return "undefined";
            }

            if (expected is bool flag)
            {
                return flag ? "true" : "false";
            }

            if (expected is string text)
            {
                return "\"" + text + "\"";
            }

            if (expected is byte || expected is sbyte || expected is short || expected is ushort
                || expected is int || expected is uint || expected is long || expected is ulong
                || expected is float || expected is double || expected is decimal)
            {
                return Convert.ToString(expected, CultureInfo.InvariantCulture);
            }

            return expected.ToString();
        }

        private static string FormatJsKeyDiff(object expected)
        {
            if (expected == null
                || expected is string
                || expected is bool
                || expected is byte || expected is sbyte || expected is short || expected is ushort
                || expected is int || expected is uint || expected is long || expected is ulong
                || expected is float || expected is double || expected is decimal)
            {
                return string.Empty;
            }

            StringBuilder log = new StringBuilder();
            System.Reflection.PropertyInfo[] props = expected.GetType().GetProperties();
            for (int i = 0; i < props.Length; i++)
            {
                log.Append("\n-   \"");
                log.Append(props[i].Name);
                log.Append('"');
            }

            return log.ToString();
        }

        private string ApiName(string method)
            => _negate ? "expect.not." + method : "expect." + method;

        private ExpectException CreateExpectException(
            string message,
            object actual,
            object expected,
            string name,
            bool pass,
            int timeoutMs,
            string ariaSnapshot = null)
            => ExpectException.Fail(message, actual, expected, name, pass, timeoutMs, ariaSnapshot);

        private async Task<string> CaptureExpectAriaSnapshotAsync(ExpectSnapshotKind kind)
        {
            if (kind == ExpectSnapshotKind.None)
            {
                return null;
            }

            try
            {
                if (kind == ExpectSnapshotKind.Page)
                {
                    return await _locator.Page.AriaSnapshotAsync(timeout: 1000).ConfigureAwait(false);
                }

                IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                bool visible = false;
                if (all.Count == 1)
                {
                    try
                    {
                        visible = await all[0].IsVisibleAsync().ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                    }
                }

                if (!visible)
                {
                    return await _locator.Page.AriaSnapshotAsync(timeout: 1000).ConfigureAwait(false);
                }

                int? depth = kind == ExpectSnapshotKind.Containment ? (int?)null : 1;
                AccessibilitySnapshotResult snapshot = await _locator.Page
                    .SnapshotAccessibilityAsync(interestingOnly: false, root: all[0])
                    .ConfigureAwait(false);
                string yaml = AriaSnapshotYaml.Format(
                    snapshot,
                    depth: depth,
                    omitDescendantNames: kind == ExpectSnapshotKind.Property);
                if (kind == ExpectSnapshotKind.Containment && all.Count == 1)
                {
                    string[] texts = await all[0]
                        .EvaluateAsync<string[]>(CollectVisibleTextFunction)
                        .ConfigureAwait(false);
                    if (texts != null)
                    {
                        for (int i = 0; i < texts.Length; i++)
                        {
                            string text = texts[i];
                            if (!string.IsNullOrEmpty(text)
                                && yaml.IndexOf(text, StringComparison.Ordinal) < 0)
                            {
                                yaml = yaml + "\n- text: " + text;
                            }
                        }
                    }
                }

                return yaml;
            }
            catch (Exception ex) when (ex is PlaywrightSharpException || ex is TimeoutException)
            {
                try
                {
                    return await _locator.Page.AriaSnapshotAsync(timeout: 1000).ConfigureAwait(false);
                }
                catch (Exception fallback) when (fallback is PlaywrightSharpException || fallback is TimeoutException)
                {
                    return null;
                }
            }
        }

        private string FormatVisibleExpectFailure(int timeoutMs, int resolved, bool isVisible, string preview)
        {
            bool missing = resolved == 0;
            bool padPresentNegate = _negate && !missing;
            StringBuilder log = new StringBuilder();
            log.Append(_negate
                ? "expect(locator).not.toBeVisible() failed"
                : "expect(locator).toBeVisible() failed");
            log.Append("\n\nLocator:");
            log.Append(padPresentNegate ? "  " : " ");
            log.Append(_locator);
            log.Append("\nExpected: ");
            log.Append(_negate ? "not visible" : "visible");
            if (!missing)
            {
                log.Append("\nReceived: ");
                log.Append(isVisible ? "visible" : "hidden");
            }

            log.Append("\nTimeout:");
            log.Append(padPresentNegate ? "  " : " ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms");
            if (missing)
            {
                log.Append("\nError: element(s) not found");
            }

            log.Append("\n\nCall log:\n  - Expect \"");
            log.Append(_negate ? "not toBeVisible" : "toBeVisible");
            log.Append("\" with timeout ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n  - waiting for ");
            log.Append(_locator);
            log.Append('\n');
            if (resolved > 0)
            {
                log.Append("    ");
                log.Append(resolved.ToString(CultureInfo.InvariantCulture));
                log.Append(" × locator resolved to ");
                log.Append(preview);
                log.Append('\n');
            }

            log.Append("expect.toBeVisible: Timeout ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms exceeded.\n");
            return log.ToString();
        }

        private string FormatVisibleAbortFailure(int timeoutMs, int resolved, string reason)
        {
            StringBuilder log = new StringBuilder();
            log.Append(_negate
                ? "expect(locator).not.toBeVisible() failed"
                : "expect(locator).toBeVisible() failed");
            log.Append("\n\nLocator: ");
            log.Append(_locator);
            log.Append("\nExpected: ");
            log.Append(_negate ? "not visible" : "visible");
            if (resolved == 0)
            {
                log.Append("\nError: element(s) not found");
            }

            log.Append("\n\nCall log:\n  - Expect \"");
            log.Append(_negate ? "not toBeVisible" : "toBeVisible");
            log.Append("\" ");
            log.Append(_locator);
            log.Append(" with timeout ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n  - waiting for ");
            log.Append(_locator);
            log.Append("\n  - operation was aborted: ");
            log.Append(reason);
            log.Append('\n');
            return log.ToString();
        }

        private Task ExpectBoolAsync(Func<Task<bool>> predicateAsync, float? timeout, string method)
            => ExpectBoolAsync(predicateAsync, timeout, method, extraMessage: null, ExpectSnapshotKind.Property);

        private async Task ExpectBoolAsync(
            Func<Task<bool>> predicateAsync,
            float? timeout,
            string method,
            Func<int, string> extraMessage,
            ExpectSnapshotKind snapshotKind = ExpectSnapshotKind.Property)
        {
            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            string header = _negate
                ? "expect(locator).not." + method + "(expected) failed"
                : "expect(locator)." + method + "(expected) failed";
            string expectLog = _negate ? "not " + method : method;

            while (true)
            {
                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                bool ok = await predicateAsync().ConfigureAwait(false);
                if (_negate ? !ok : ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(header);
                    string extra = extraMessage == null ? string.Empty : extraMessage(timeoutMs);
                    if (string.IsNullOrEmpty(extra) || !extra.Contains("Locator:", StringComparison.Ordinal))
                    {
                        log.Append("\n\nLocator:  ");
                        log.Append(_locator);
                    }

                    if (!string.IsNullOrEmpty(extra))
                    {
                        log.Append('\n');
                        if (extra.Contains("Locator:", StringComparison.Ordinal))
                        {
                            log.Append('\n');
                        }

                        log.Append(extra);
                    }

                    log.Append("\nTimeout:  ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms");
                    int resolvedCount = await _locator.CountAsync().ConfigureAwait(false);
                    if (resolvedCount == 0)
                    {
                        log.Append("\nError: element(s) not found");
                    }

                    log.Append("\n\nCall log:\n  - Expect \"");
                    log.Append(expectLog);
                    log.Append("\" with timeout ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms\n  - waiting for ");
                    log.Append(_locator);
                    log.Append("\nTimeout: ");
                    log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                    log.Append("ms\n");
                    throw CreateExpectException(
                        log.ToString(),
                        actual: null,
                        expected: null,
                        method,
                        pass: _negate,
                        timeoutMs,
                        await CaptureExpectAriaSnapshotAsync(snapshotKind).ConfigureAwait(false));
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private async Task ExpectValueCoreAsync(ExpectTextNeedle needle, float? timeout)
        {
            if (needle == null)
            {
                throw new ArgumentNullException(nameof(needle));
            }

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            string last = null;
            string header = _negate
                ? "expect(locator).not.toHaveValue(expected) failed"
                : "expect(locator).toHaveValue(expected) failed";

            while (true)
            {
                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                if (all.Count > 1)
                {
                    throw new PlaywrightSharpException(
                        await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                }

                bool matched = false;
                if (all.Count == 1)
                {
                    last = await all[0]
                        .EvaluateAsync<string>(ElementStateScript.InputValueFunction)
                        .ConfigureAwait(false) ?? string.Empty;
                    matched = ExpectTextMatch.MatchesNeedle(last, needle, exact: true, ignoreCase: null);
                }

                bool ok = all.Count == 0 ? false : (_negate ? !matched : matched);
                if (ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(header);
                    log.Append("\nExpected: ");
                    log.Append(ExpectTextMatch.FormatNeedle(needle, _negate));
                    if (last != null)
                    {
                        log.Append("\nReceived: \"");
                        log.Append(last);
                        log.Append('"');
                    }

                    throw CreateExpectException(
                        log.ToString(),
                        last,
                        needle.Regex != null ? needle.Regex : (object)needle.String,
                        "toHaveValue",
                        pass: _negate,
                        timeoutMs,
                        await CaptureExpectAriaSnapshotAsync(ExpectSnapshotKind.Property).ConfigureAwait(false));
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private async Task ExpectValuesCoreAsync(ExpectTextNeedle[] needles, float? timeout)
        {
            if (needles == null)
            {
                throw new ArgumentNullException(nameof(needles));
            }

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            string[] lastReceived = Array.Empty<string>();
            string header = _negate
                ? "expect(locator).not.toHaveValues(expected) failed"
                : "expect(locator).toHaveValues(expected) failed";

            while (true)
            {
                await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                if (all.Count > 1)
                {
                    throw new PlaywrightSharpException(
                        await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
                }

                if (all.Count == 1)
                {
                    string[] received = await all[0]
                        .EvaluateAsync<string[]>(ElementStateScript.SelectedValuesFunction)
                        .ConfigureAwait(false) ?? Array.Empty<string>();
                    lastReceived = received;
                    bool matched = received.Length == needles.Length;
                    if (matched)
                    {
                        for (int i = 0; i < needles.Length; i++)
                        {
                            if (!ExpectTextMatch.MatchesNeedle(received[i], needles[i], exact: true, ignoreCase: null))
                            {
                                matched = false;
                                break;
                            }
                        }
                    }

                    bool ok = _negate ? !matched : matched;
                    if (ok)
                    {
                        return;
                    }
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    StringBuilder log = new StringBuilder();
                    log.Append(header);
                    log.Append(FormatValueArrayDiff(lastReceived, needles));
                    throw new TimeoutException(log.ToString());
                }

                await Task.Delay(50).ConfigureAwait(false);
            }
        }

        private string FormatValueArrayDiff(string[] received, ExpectTextNeedle[] needles)
        {
            StringBuilder log = new StringBuilder();
            int max = received.Length > needles.Length ? received.Length : needles.Length;
            for (int i = 0; i < max; i++)
            {
                string got = i < received.Length ? received[i] : null;
                ExpectTextNeedle needle = i < needles.Length ? needles[i] : null;
                bool same = got != null && needle != null
                    && ExpectTextMatch.MatchesNeedle(got, needle, exact: true, ignoreCase: null);
                if (same)
                {
                    continue;
                }

                if (needle != null)
                {
                    log.Append("\n-   ");
                    log.Append(ExpectTextMatch.FormatNeedle(needle, negate: false));
                }

                if (got != null)
                {
                    log.Append("\n+   \"");
                    log.Append(got);
                    log.Append('"');
                }
            }

            return log.ToString();
        }

        private async Task ExpectTextCoreAsync(
            ExpectTextNeedle[] needles,
            bool exact,
            bool requireLength,
            bool single,
            float? timeout,
            bool? ignoreCase,
            bool? useInnerText,
            string method,
            AbortSignal signal = default)
        {
            if (needles == null)
            {
                throw new ArgumentNullException(nameof(needles));
            }

            string header = _negate
                ? "expect(locator).not." + method + "(expected) failed"
                : "expect(locator)." + method + "(expected) failed";
            string alreadyAbortedDetails = "Locator: " + _locator +
                "\nExpected: " +
                (needles.Length > 0 ? ExpectTextMatch.FormatNeedle(needles[0], _negate) : "\"\"") +
                "\n";
            ExpectAbort.ThrowIfAlreadyAborted(signal, header, alreadyAbortedDetails);

            int timeoutMs = TimeoutSettings.ExpectTimeoutMs(timeout);
            Stopwatch sw = Stopwatch.StartNew();
            string[] lastReceived = Array.Empty<string>();
            string lastPreview = null;
            bool sawElement = false;
            string expectLog = _negate ? "not " + method : method;

            while (true)
            {
                if (ExpectAbort.TryMidAbort(signal, out string abortReason))
                {
                    throw CreateTextExpectException(
                        header + "\n\n  - operation was aborted: " + abortReason + "\n",
                        needles,
                        lastReceived,
                        method,
                        pass: false,
                        timeoutMs,
                        ariaSnapshot: null);
                }

                IReadOnlyList<IElementHandle> all;
                try
                {
                    await LocatorHandlers.RunAsync(_locator.Page, timeout).ConfigureAwait(false);
                    all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
                }
                catch (TimeoutException ex) when (
                    ex.Message != null
                    && ex.Message.Contains("locator handler has finished", StringComparison.Ordinal))
                {
                    string[] handlerReceived = lastReceived.Length > 0 ? lastReceived : new[] { string.Empty };
                    string overlay = ex.Message + "\n";
                    try
                    {
                        IReadOnlyList<IElementHandle> visible = await _locator.Page.Locator("*")
                            .ElementHandlesAsync()
                            .ConfigureAwait(false);
                        for (int i = 0; i < visible.Count; i++)
                        {
                            if (!await visible[i].IsVisibleAsync().ConfigureAwait(false))
                            {
                                continue;
                            }

                            string preview = await visible[i]
                                .EvaluateAsync<string>(ElementPreviewFunction)
                                .ConfigureAwait(false);
                            if (string.IsNullOrEmpty(preview)
                                || preview.StartsWith("<html", StringComparison.Ordinal)
                                || preview.StartsWith("<body", StringComparison.Ordinal)
                                || preview.StartsWith("<head", StringComparison.Ordinal))
                            {
                                continue;
                            }

                            overlay = ex.Message + "\n  locator resolved to visible " + preview + "\n";
                            break;
                        }
                    }
                    catch (PlaywrightSharpException)
                    {
                    }

                    throw CreateTextExpectException(
                        FormatTextExpectFailure(
                            header,
                            expectLog,
                            method,
                            needles,
                            handlerReceived,
                            sawElement: true,
                            single,
                            exact,
                            ignoreCase,
                            timeoutMs,
                            lastPreview) +
                        overlay,
                        needles,
                        handlerReceived,
                        method,
                        pass: _negate,
                        timeoutMs,
                        await CaptureTextAriaSnapshotAsync(single, sawElement: true).ConfigureAwait(false));
                }
                catch (Exception ex) when (ClosedTarget.IsClosed(ex))
                {
                    if (sawElement)
                    {
                        throw CreateTextExpectException(
                            FormatTextExpectFailure(
                                header,
                                expectLog,
                                method,
                                needles,
                                lastReceived,
                                sawElement,
                                single,
                                exact,
                                ignoreCase,
                                timeoutMs,
                                lastPreview),
                            needles,
                            lastReceived,
                            method,
                            pass: _negate,
                            timeoutMs,
                            await CaptureTextAriaSnapshotAsync(single, sawElement).ConfigureAwait(false));
                    }

                    throw new PlaywrightSharpException(header + "\n" + ex.Message, ex);
                }

                if (single && all.Count > 1)
                {
                    string strict = await StrictModeViolation.FormatAsync(_locator.ToString(), all)
                        .ConfigureAwait(false);
                    ExpectTextNeedle needle = needles.Length > 0 ? needles[0] : new ExpectTextNeedle(string.Empty);
                    throw new PlaywrightSharpException(
                        header +
                        "\n\nLocator: " +
                        _locator +
                        "\nExpected: " +
                        ExpectTextMatch.FormatNeedle(needle, _negate) +
                        "\nError: " +
                        strict +
                        "\n\nCall log:\n");
                }

                string[] received = new string[all.Count];
                bool readFailed = false;
                for (int i = 0; i < all.Count; i++)
                {
                    try
                    {
                        received[i] = await ReadTextAsync(all[i], useInnerText).ConfigureAwait(false);
                    }
                    catch (PlaywrightSharpException)
                    {
                        readFailed = true;
                        break;
                    }
                }

                if (!readFailed && all.Count > 0)
                {
                    lastReceived = received;
                    sawElement = true;
                    if (all.Count == 1)
                    {
                        try
                        {
                            lastPreview = await all[0].EvaluateAsync<string>(ElementPreviewFunction)
                                .ConfigureAwait(false);
                        }
                        catch (PlaywrightSharpException)
                        {
                        }
                    }
                }

                bool matched = !readFailed && ExpectTextMatch.MatchesSequence(
                    received,
                    needles,
                    requireLength,
                    exact,
                    ignoreCase);

                // Missing elements fail both toHaveText/toContainText and
                // not.toHaveText/not.toContainText for a single locator.
                bool ok;
                if (single && all.Count == 0)
                {
                    ok = false;
                }
                else
                {
                    ok = _negate ? !matched : matched;
                }

                if (ok)
                {
                    return;
                }

                if (timeoutMs != Timeout.Infinite && sw.ElapsedMilliseconds >= timeoutMs)
                {
                    throw CreateTextExpectException(
                        FormatTextExpectFailure(
                            header,
                            expectLog,
                            method,
                            needles,
                            lastReceived,
                            sawElement,
                            single,
                            exact,
                            ignoreCase,
                            timeoutMs,
                            lastPreview),
                        needles,
                        lastReceived,
                        method,
                        pass: _negate && matched,
                        timeoutMs,
                        await CaptureTextAriaSnapshotAsync(single, sawElement).ConfigureAwait(false));
                }

                await ExpectAbort.DelayOrAbortAsync(signal).ConfigureAwait(false);
            }
        }

        private string FormatTextExpectFailure(
            string header,
            string expectLog,
            string method,
            ExpectTextNeedle[] needles,
            string[] received,
            bool sawElement,
            bool single,
            bool exact,
            bool? ignoreCase,
            int timeoutMs,
            string preview)
        {
            StringBuilder log = new StringBuilder();
            log.Append(header);
            log.Append("\n\nLocator: ");
            log.Append(_locator);
            if (single)
            {
                ExpectTextNeedle needle = needles.Length > 0 ? needles[0] : new ExpectTextNeedle(string.Empty);
                string printed = ExpectTextMatch.FormatNeedle(needle, _negate);
                if (needle.Regex != null)
                {
                    log.Append("\nExpected pattern: ");
                    log.Append(printed);
                    if (sawElement && received.Length > 0)
                    {
                        log.Append("\nReceived string:  \"");
                        log.Append(received[0]);
                        log.Append('"');
                    }
                }
                else if (string.Equals(method, "toContainText", StringComparison.Ordinal))
                {
                    log.Append("\nExpected substring: ");
                    log.Append(printed);
                    if (sawElement && received.Length > 0)
                    {
                        log.Append("\nReceived string:    \"");
                        log.Append(received[0]);
                        log.Append('"');
                    }
                }
                else
                {
                    log.Append("\nExpected: ");
                    log.Append(printed);
                    if (sawElement && received.Length > 0)
                    {
                        log.Append("\nReceived: \"");
                        log.Append(received[0]);
                        log.Append('"');
                    }
                }

                log.Append("\nTimeout: ");
                log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                log.Append("ms");
                if (!sawElement)
                {
                    log.Append("\nError: element(s) not found");
                }
            }
            else
            {
                bool emptyNegate = _negate && needles.Length == 0;
                log.Append("\nTimeout:");
                log.Append(emptyNegate ? "  " : " ");
                log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                log.Append("ms\n");
                log.Append(ExpectTextMatch.FormatArrayDiff(received, needles, exact, ignoreCase));
            }

            log.Append("\n\nCall log:\n  - Expect \"");
            log.Append(expectLog);
            log.Append("\" with timeout ");
            log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
            log.Append("ms\n  - waiting for ");
            log.Append(_locator);
            log.Append('\n');
            if (received.Length == 1 && single)
            {
                log.Append("  locator resolved to ");
                log.Append(preview ?? received[0]);
                log.Append("\n  unexpected value \"");
                log.Append(received[0]);
                log.Append("\"\n");
            }
            else if (received.Length > 0)
            {
                log.Append("  locator resolved to ");
                log.Append(received.Length.ToString(CultureInfo.InvariantCulture));
                log.Append(received.Length == 1 ? " element\n" : " elements\n");
            }

            if (single
                && sawElement
                && string.Equals(method, "toHaveText", StringComparison.Ordinal)
                && needles.Length > 0
                && needles[0].Regex == null)
            {
                log.Append(header);
                log.Append("\n\nLocator:  ");
                log.Append(_locator);
                log.Append("\nExpected: ");
                log.Append(ExpectTextMatch.FormatNeedle(needles[0], _negate));
                log.Append("\nReceived: \"");
                log.Append(received.Length > 0 ? received[0] : string.Empty);
                log.Append("\"\nTimeout:  ");
                log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                log.Append("ms\n\nCall log:\n");
            }

            if (single
                && sawElement
                && _negate
                && needles.Length > 0
                && needles[0].Regex != null)
            {
                log.Append("Error: ");
                log.Append(header);
                log.Append("\n\nLocator: ");
                log.Append(_locator);
                log.Append("\nExpected pattern: ");
                log.Append(ExpectTextMatch.FormatNeedle(needles[0], _negate));
                log.Append("\nReceived string: \"");
                log.Append(received.Length > 0 ? received[0] : string.Empty);
                log.Append("\"\nTimeout: ");
                log.Append(timeoutMs.ToString(CultureInfo.InvariantCulture));
                log.Append("ms\n\nCall log");
            }

            return log.ToString();
        }

        private ExpectException CreateTextExpectException(
            string message,
            ExpectTextNeedle[] needles,
            string[] received,
            string method,
            bool pass,
            int timeoutMs,
            string ariaSnapshot)
        {
            object expected = null;
            if (needles != null && needles.Length > 0 && needles[0] != null)
            {
                expected = needles[0].Regex != null ? needles[0].Regex : (object)needles[0].String;
            }

            object actual = received != null && received.Length > 0 ? received[0] : string.Empty;
            return CreateExpectException(message, actual, expected, method, pass, timeoutMs, ariaSnapshot);
        }

        private Task<string> CaptureTextAriaSnapshotAsync(bool single, bool sawElement)
        {
            if (!single)
            {
                return Task.FromResult<string>(null);
            }

            return CaptureExpectAriaSnapshotAsync(
                sawElement ? ExpectSnapshotKind.Containment : ExpectSnapshotKind.Page);
        }

        private async Task<string> ReadTextAsync(IElementHandle handle, bool? useInnerText)
        {
            if (useInnerText == true)
            {
                return await handle.InnerTextAsync().ConfigureAwait(false) ?? string.Empty;
            }

            return await handle.EvaluateAsync<string>(ElementStateScript.ElementTextFullFunction)
                .ConfigureAwait(false) ?? string.Empty;
        }

        private async Task<string[]> CollectTextContentsAsync(bool? useInnerText = default)
        {
            IReadOnlyList<IElementHandle> all = await _locator.ElementHandlesAsync().ConfigureAwait(false);
            string[] texts = new string[all.Count];
            for (int i = 0; i < all.Count; i++)
            {
                texts[i] = await ReadTextAsync(all[i], useInnerText).ConfigureAwait(false);
            }

            return texts;
        }

        private async Task<string[]> CollectClassAttributesAsync()
        {
            IReadOnlyList<IElementHandle> all = await _locator.ElementHandlesAsync().ConfigureAwait(false);
            string[] classes = new string[all.Count];
            for (int i = 0; i < all.Count; i++)
            {
                classes[i] = await all[i].GetAttributeAsync("class").ConfigureAwait(false) ?? string.Empty;
            }

            return classes;
        }

        private async Task<IReadOnlyList<IElementHandle>> ElementHandlesOrEmptyAsync()
        {
            try
            {
                return await _locator.ElementHandlesAsync().ConfigureAwait(false);
            }
            catch (PlaywrightSharpException ex) when (
                PlaywrightSharpException.IsDestroyedContext(ex)
                || DomVisibility.IsTransientVisibilityError(ex))
            {
                return Array.Empty<IElementHandle>();
            }
        }

        private async Task<bool> UniqueStateAsync(Func<IElementHandle, Task<bool>> checkAsync)
        {
            IReadOnlyList<IElementHandle> all = await ElementHandlesOrEmptyAsync().ConfigureAwait(false);
            if (all.Count > 1)
            {
                throw new PlaywrightSharpException(
                    await StrictModeViolation.FormatAsync(_locator.ToString(), all).ConfigureAwait(false));
            }

            if (all.Count == 0)
            {
                return false;
            }

            return await checkAsync(all[0]).ConfigureAwait(false);
        }

        private Task<string> ReadCssAsync(IElementHandle handle, string name, string pseudo)
        {
            return handle.EvaluateAsync<string>(
                @"(el, spec) => {
                    let pseudo = spec.pseudo;
                    if (pseudo) {
                        pseudo = String(pseudo);
                        if (pseudo.indexOf(':') !== 0) {
                            pseudo = '::' + pseudo;
                        }
                    } else {
                        pseudo = null;
                    }
                    return (getComputedStyle(el, pseudo).getPropertyValue(spec.name) || '').trim();
                }",
                new { name, pseudo });
        }

        private Task<bool> UniqueAriaAsync(Func<AccessibilitySnapshotResult, bool> check)
            => UniqueStateAsync(async handle =>
            {
                IFrame owner = await handle.OwnerFrameAsync().ConfigureAwait(false);
                IPage page = owner?.Page ?? _locator.Page;
                if (page is not IHasPageExtras)
                {
                    return false;
                }

                AccessibilitySnapshotResult snapshot = await page
                    .SnapshotAccessibilityAsync(root: handle)
                    .ConfigureAwait(false);
                if (snapshot == null)
                {
                    return false;
                }

                return check(snapshot);
            });

        private PlaywrightSharpException FormatVisibleSelectorError(Exception ex)
        {
            string locator = _locator.ToString();
            string raw = locator;
            if (raw.StartsWith("locator('", StringComparison.Ordinal) && raw.EndsWith("')", StringComparison.Ordinal))
            {
                raw = raw.Substring("locator('".Length, raw.Length - "locator('".Length - 2);
            }

            string header = _negate
                ? "expect(locator).not.toBeVisible() failed"
                : "expect(locator).toBeVisible() failed";
            return new PlaywrightSharpException(
                header +
                "\n\nLocator: " +
                raw +
                "\nExpected: " +
                (_negate ? "hidden" : "visible") +
                "\nError: " +
                (ex.Message ?? string.Empty) +
                "\n\nCall log:\nexpect.toBeVisible\n");
        }

        private string FormatQuotedExpectedReceived(string expected, string received)
        {
            return "Expected: " +
                   (_negate ? "not " : string.Empty) +
                   "\"" +
                   expected +
                   "\"\nReceived: \"" +
                   received +
                   "\"";
        }

        private string FormatAttributeStringExtra(string expected, string received, bool missing)
        {
            if (missing && string.IsNullOrEmpty(expected) && !_negate)
            {
                return "Expected: \"\"\nReceived: serializes to the same string";
            }

            return FormatQuotedExpectedReceived(expected, received);
        }

        private string FormatAttributeRegexExtra(Regex expected, string received, int timeoutMs)
        {
            string printed = ExpectTextMatch.FormatNeedle(new ExpectTextNeedle(expected), _negate);
            string ms = timeoutMs.ToString(CultureInfo.InvariantCulture);
            string header = _negate
                ? "expect(locator).not.toHaveAttribute(expected) failed"
                : "expect(locator).toHaveAttribute(expected) failed";
            return "Locator: " +
                   _locator +
                   "\nExpected pattern: " +
                   printed +
                   "\nReceived string:  \"" +
                   received +
                   "\"\nTimeout: " +
                   ms +
                   "ms\n\nCall log:\n" +
                   header +
                   "\n\nLocator: " +
                   _locator +
                   "\nExpected pattern: " +
                   printed +
                   "\nReceived string: \"" +
                   received +
                   "\"\nTimeout: " +
                   ms +
                   "ms";
        }

        private string FormatJsPropertyExtra(object expected, string received)
        {
            StringBuilder log = new StringBuilder();
            log.Append("Expected: ");
            if (_negate)
            {
                log.Append("not ");
            }

            log.Append(FormatExpectedJsValue(expected));
            log.Append("\nReceived: ");
            log.Append(received);
            log.Append(FormatJsKeyDiff(expected));
            return log.ToString();
        }

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task ILocatorAssertions.ToBeAttachedAsync(LocatorAssertionsToBeAttachedOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeCheckedAsync(LocatorAssertionsToBeCheckedOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeDisabledAsync(LocatorAssertionsToBeDisabledOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeEditableAsync(LocatorAssertionsToBeEditableOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeEmptyAsync(LocatorAssertionsToBeEmptyOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeEnabledAsync(LocatorAssertionsToBeEnabledOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeFocusedAsync(LocatorAssertionsToBeFocusedOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeHiddenAsync(LocatorAssertionsToBeHiddenOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeInViewportAsync(LocatorAssertionsToBeInViewportOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToBeVisibleAsync(LocatorAssertionsToBeVisibleOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToContainClassAsync(string expected, LocatorAssertionsToContainClassOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToContainClassAsync(IEnumerable<string> expected, LocatorAssertionsToContainClassOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToContainTextAsync(string expected, LocatorAssertionsToContainTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToContainTextAsync(Regex expected, LocatorAssertionsToContainTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToContainTextAsync(IEnumerable<string> expected, LocatorAssertionsToContainTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToContainTextAsync(IEnumerable<Regex> expected, LocatorAssertionsToContainTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAccessibleDescriptionAsync(string description, LocatorAssertionsToHaveAccessibleDescriptionOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAccessibleDescriptionAsync(Regex description, LocatorAssertionsToHaveAccessibleDescriptionOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAccessibleErrorMessageAsync(string errorMessage, LocatorAssertionsToHaveAccessibleErrorMessageOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAccessibleErrorMessageAsync(Regex errorMessage, LocatorAssertionsToHaveAccessibleErrorMessageOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAccessibleNameAsync(string name, LocatorAssertionsToHaveAccessibleNameOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAccessibleNameAsync(Regex name, LocatorAssertionsToHaveAccessibleNameOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAttributeAsync(string name, string value, LocatorAssertionsToHaveAttributeOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveAttributeAsync(string name, Regex value, LocatorAssertionsToHaveAttributeOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveClassAsync(string expected, LocatorAssertionsToHaveClassOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveClassAsync(Regex expected, LocatorAssertionsToHaveClassOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveClassAsync(IEnumerable<string> expected, LocatorAssertionsToHaveClassOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveClassAsync(IEnumerable<Regex> expected, LocatorAssertionsToHaveClassOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveCountAsync(int count, LocatorAssertionsToHaveCountOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveCSSAsync(string name, string value, LocatorAssertionsToHaveCSSOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveCSSAsync(string name, Regex value, LocatorAssertionsToHaveCSSOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveIdAsync(string id, LocatorAssertionsToHaveIdOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveIdAsync(Regex id, LocatorAssertionsToHaveIdOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveJSPropertyAsync(string name, object value, LocatorAssertionsToHaveJSPropertyOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveRoleAsync(AriaRole role, LocatorAssertionsToHaveRoleOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveTextAsync(string expected, LocatorAssertionsToHaveTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveTextAsync(Regex expected, LocatorAssertionsToHaveTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveTextAsync(IEnumerable<string> expected, LocatorAssertionsToHaveTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveTextAsync(IEnumerable<Regex> expected, LocatorAssertionsToHaveTextOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveValueAsync(string value, LocatorAssertionsToHaveValueOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveValueAsync(Regex value, LocatorAssertionsToHaveValueOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveValuesAsync(IEnumerable<string> values, LocatorAssertionsToHaveValuesOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToHaveValuesAsync(IEnumerable<Regex> values, LocatorAssertionsToHaveValuesOptions options) => Task.CompletedTask;

        Task ILocatorAssertions.ToMatchAriaSnapshotAsync(string expected, LocatorAssertionsToMatchAriaSnapshotOptions options) => Task.CompletedTask;
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
