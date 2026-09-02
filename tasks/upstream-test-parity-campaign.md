# Upstream test parity campaign

Unattended Cloud Agents continue this campaign from `origin/main` without a human typing "next".

If you were started by a Cursor Automation, or the user said "keep going" / "next", **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** leftover API hunt (exhausted at Wave 694). Active chain: `tasks/campaign-chain.md`.

## Goal

Port official Playwright Node tests from `/tmp/playwright/tests/page/` and `/tmp/playwright/tests/library/` into PlaywrightNative.

Golden rule: **tests must match upstream**. Use the exact spec file name and the exact `test(...)` / `it(...)` title in `[PlaywrightTest]`. Never weaken an assertion to match local code. If a ported test fails, **fix the library**.

Do **not** invent leftover filler APIs. A library change is allowed only when a faithfully ported upstream test proves a bug or a missing official behavior.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, assume another agent is in flight and **stop**. Exception: this campaign may launch **parallel** waves on **non-overlapping specs** (see Parallel).
4. Create `cursor/<descriptive-name>-554a` from latest `main`. Never commit implementation on `main`.
5. Port the next unchecked wave (one spec file, or a tight cluster named in the wave).

## Parallel waves

A parent agent may launch several isolated subagents at once when:

- Each wave owns a **different upstream spec file**.
- Each wave writes a **different C# test class file**.
- Library edits do not overlap (agree ownership in the wave prompt).
- Subagents **push their feature branch** but **do not** merge `main`.
- The parent fast-forward merges waves onto `main` **one at a time** in wave-number order, then writes the docs commit for that wave.

Do not race on `main`. Do not let two agents edit `tasks/todo.md` or the same library file.

## Per-wave loop (mandatory)

1. Hunt **one** unported spec (see Hunt). Put it in `tasks/todo.md` as Current Phase Wave NNN, unchecked.
2. Create `cursor/wave-NNN-<short-spec>-554a` from latest `main`.
3. Port every `test`/`it` from that spec into a new test class. Skip only Node-only internals (`toImpl`, inspector, Electron, Android, BiDi-only). List skipped titles in the class XML summary.
4. `git add` / `git commit` the feature (`feat: port <spec> (Wave NNN)`).
5. `git push -u origin <branch>`.
6. Run Chromium **and** WebKit for the new class. Both must be green. If a test fails, fix the library (or leave the failure — never skip / never `TestExpectations`).
7. Docs commit: check the box, move the wave to Previous, set Current Phase to the next unchecked spec (`docs: mark Wave NNN <spec> complete`).
8. Fast-forward merge to `main` and `git push origin main`.
9. Repeat.

No pull requests. No `ManagePullRequest`. Owner merges by ff-merge to `main`.

Co-authored-by: `Darío Kondratiuk <dariokondratiuk@gmail.com>`.

## Hunt

1. Clone or update upstream: `/tmp/playwright` (`tests/page`, `tests/library`, `tests/assets`).
2. For a candidate spec, collect every `test('title'` / `it('title'`.
3. Grep local `[PlaywrightTest("that.spec.ts", "...")]`.
4. Pick a spec with **missing titles**. Prefer APIs that already exist (page/locator/handle actions).
5. Official .NET ports under `/tmp/playwright-dotnet/src/Playwright.Tests/` are **idiom reference only**. Upstream TypeScript is the source of truth.

### Skip (do not port yet)

- `inspector/`, debugger, `browsertype-connect*`, Node driver protocol
- Firefox-only folders when the API cannot run here
- Huge screenshot pixel-diff suites until a dedicated screenshot-parity wave
- Tests that need `toImpl` / utility-world tampering with no public equivalent

## Test file conventions

- New file: `src/PlaywrightNative.Tests/<SpecName>Tests.cs` (no `Direct` filename/class prefix)
- Apache-2.0 header like other public-API tests
- `[TestFixture]`
- `[PlaywrightTest("exact-upstream-file.spec.ts", "exact upstream title")]`
- `[Test] [Timeout(30_000)]` (or `TestConstants.DefaultTestTimeout`)
- Launch: `BrowserLauncher.LaunchAsync()`, `NewContextAsync()`, `NewPageAsync()`
- Server: `TestConstants.ServerUrl` (`http://localhost:8081`), `EmptyPage`, `CrossProcessHttpPrefix`. `SimpleServer` via `TestServerSetup.Server` for `SetRoute` / `SetRedirect`
- Map TS → C#: `setContent` → `SetContentAsync`, `$eval` → `EvalOnSelectorAsync`, `$` → `QuerySelectorAsync`, `evaluateHandle` → `EvaluateHandleAsync`, `jsonValue` → `JsonValueAsync`, `asElement` → `AsElement`
- `EvaluateAsync` is raw `Runtime.evaluate` — wrap page functions in an IIFE / function string
- NUnit: `Assert.That(...)`. `Assert.ThrowsAsync<TimeoutException>` is exact-type. `Assert.CatchAsync` for `PlaywrightNativeException` (sync — no `.ConfigureAwait`)
- `ConfigureAwait(false)` on every `await`
- No `var` for built-in types. Private fields `_camelCase`. CRLF on `.cs` and `tasks/todo.md`
- `List<T>` for IEnumerable overloads. `foreach` on `ChildFrames`
- Browser-specific upstream `it.skip`: `Assert.Ignore` with the upstream reason. Firefox may `Assert.Ignore` here
- Do not wrap `Assert.Ignore` in `Assert.CatchAsync`

## Test locally (before merge)

```bash
killall -9 chrome chrome_crashpad_handler || true

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build ./src/PlaywrightNative/PlaywrightNative.csproj -f net10.0

PRODUCT=CHROMIUM dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj \
  -f net10.0 --filter "FullyQualifiedName~<YourNewTestClass>" --no-restore

PRODUCT=WEBKIT dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj \
  -f net10.0 --filter "FullyQualifiedName~<YourNewTestClass>" --no-restore
```

Chrome: `/opt/google/chrome/chrome` or `/usr/local/bin/chrome`
WebKit: `~/.cache/ms-playwright/webkit-2276/pw_run.sh`
`netstandard2.1` still has pre-existing `OperatingSystem.IsMacOS` errors — build `-f net10.0`.

After editing tests, **rebuild**. `--no-build` after a source-only fix reuses a stale DLL.

## Queue (first waves)

| Wave | Spec | Notes |
|------|------|-------|
| 695 | `page-check.spec.ts` | New `DirectPageCheckParityTests.cs`. Do not edit `DirectPageCheckScrollTests.cs` / `DirectCheckStrictTests.cs`. |
| 696 | `locator-evaluate.spec.ts` | New `DirectLocatorEvaluateParityTests.cs`. Official `evaluateAll` receives **all** elements as one array. Existing leftover `EvaluateAllAsync` maps per-element — **fix the library** and update `DirectLocatorEvaluateTests` to official `els => els.map(...)`. |
| 697 | `jshandle-json-value.spec.ts` | New `DirectJSHandleJsonValueTests.cs`. |
| 698 | `jshandle-as-element.spec.ts` | New `DirectJSHandleAsElementTests.cs`. `AsElement` is a property. Done. |
| 699 | `jshandle-evaluate.spec.ts` | New `DirectJSHandleEvaluateTests.cs`. |
| 700 | `jshandle-to-string.spec.ts` | New `DirectJSHandleToStringTests.cs`. |
| 701 | `page-dialog.spec.ts` | New `DirectPageDialogParityTests.cs`. |
| 702 | `page-basic.spec.ts` | New `DirectPageBasicTests.cs`. Done. |
| 703 | `page-focus.spec.ts` | New `DirectPageFocusTests.cs`. |
| 704 | `page-history.spec.ts` | New `DirectPageHistoryParityTests.cs`. |
| 705 | `page-fill.spec.ts` | New `DirectPageFillParityTests.cs`. |
| 706 | `page-set-content.spec.ts` | New `DirectPageSetContentTests.cs`. Skip `toImpl` / utility-world internals. Done. |
| 707 | `page-emulate-media.spec.ts` | New `DirectPageEmulateMediaTests.cs`. |
| 708 | `page-network-idle.spec.ts` | New `DirectPageNetworkIdleTests.cs`. |
| 709 | `page-event-console.spec.ts` | New `DirectPageEventConsoleTests.cs`. |
| 710 | `locator-click.spec.ts` | New `DirectLocatorClickParityTests.cs`. Done. |
| 711 | `page-keyboard.spec.ts` | New `DirectPageKeyboardTests.cs`. Done. |
| 712 | `page-mouse.spec.ts` | New `DirectPageMouseTests.cs`. Done. |
| 713 | `page-dispatchevent.spec.ts` | New `DirectPageDispatchEventParityTests.cs`. Done. |
| 714 | `locator-convenience.spec.ts` | New `DirectLocatorConvenienceTests.cs`. Done. |
| 715 | `jshandle-properties.spec.ts` | New `DirectJSHandlePropertiesTests.cs`. Done. |
| 716 | `locator-query.spec.ts` | New `DirectLocatorQueryTests.cs`. Done. |
| 717 | `elementhandle-convenience.spec.ts` | New `DirectElementHandleConvenienceTests.cs`. Done. |
| 718 | `page-add-script-tag.spec.ts` | New `DirectPageAddScriptTagTests.cs`. Done. |
| 719 | `page-add-style-tag.spec.ts` | New `DirectPageAddStyleTagTests.cs`. Done. |
| 720 | `locator-is-visible.spec.ts` | New `DirectLocatorIsVisibleTests.cs`. Done. |
| 721 | `eval-on-selector.spec.ts` | New `DirectEvalOnSelectorParityTests.cs`. Done. |
| 722 | `elementhandle-misc.spec.ts` | New `DirectElementHandleMiscTests.cs`. Done. |
| 723 | `page-add-init-script.spec.ts` | New `DirectPageAddInitScriptTests.cs`. Done. |
| 724 | `locator-misc-1.spec.ts` | New `DirectLocatorMisc1Tests.cs`. Done. |
| 725 | `eval-on-selector-all.spec.ts` | New `DirectEvalOnSelectorAllParityTests.cs`. Done. |
| 726 | `frame-evaluate.spec.ts` | New `DirectFrameEvaluateTests.cs`. Done. |
| 727 | `page-select-option.spec.ts` | New `DirectPageSelectOptionParityTests.cs`. Done. |
| 728 | `wheel.spec.ts` | New `DirectPageWheelTests.cs`. Done. |
| 729 | `elementhandle-type.spec.ts` | New `DirectElementHandleTypeTests.cs`. Done. |
| 730 | `page-drop.spec.ts` | New `DirectPageDropTests.cs`. Done. |
| 731 | `locator-misc-2.spec.ts` | New `DirectLocatorMisc2Tests.cs`. Done. |
| 732 | `page-event-pageerror.spec.ts` | New `DirectPageEventPageErrorTests.cs`. Done. |
| 733 | `page-filechooser.spec.ts` | New `DirectPageFileChooserParityTests.cs`. Done. |
| 734 | `elementhandle-press.spec.ts` | New `DirectElementHandlePressTests.cs`. Done. |
| 735 | `page-expose-function.spec.ts` | New `DirectPageExposeFunctionParityTests.cs`. Done. |
| 736 | `page-event-popup.spec.ts` | New `DirectPageEventPopupTests.cs`. Done. |
| 737 | `locator-frame.spec.ts` | New `DirectLocatorFrameParityTests.cs`. Done. |
| 738 | `page-event-request.spec.ts` | New `DirectPageEventRequestTests.cs`. Done. |
| 739 | `page-wait-for-function.spec.ts` | New `DirectPageWaitForFunctionParityTests.cs`. Done. |
| 740 | `page-drag.spec.ts` | New `DirectPageDragParityTests.cs`. Done. |
| 741 | `elementhandle-click.spec.ts` | New `DirectElementHandleClickParityTests.cs`. Done. |
| 742 | `page-wait-for-load-state.spec.ts` | New `DirectPageWaitForLoadStateParityTests.cs`. Done. |
| 743 | `page-event-load.spec.ts` | New `DirectPageEventLoadParityTests.cs`. Done. |
| 744 | `page-listeners.spec.ts` | New `DirectPageListenersParityTests.cs`. Done. |
| 745 | `page-set-extra-http-headers.spec.ts` | New `DirectPageSetExtraHttpHeadersParityTests.cs`. Done. |
| 746 | `elementhandle-bounding-box.spec.ts` | New `DirectElementHandleBoundingBoxParityTests.cs`. Done. |
| 747 | `workers.spec.ts` | New `DirectWorkersParityTests.cs`. Done. |
| 748 | `page-evaluate-handle.spec.ts` | New `DirectPageEvaluateHandleParityTests.cs`. Done. |
| 749 | `page-strict.spec.ts` | New `DirectPageStrictParityTests.cs`. Done. |
| 750 | `frame-hierarchy.spec.ts` | New `DirectFrameHierarchyParityTests.cs`. Done. |
| 751 | `frame-goto.spec.ts` | New `DirectFrameGotoParityTests.cs`. Done. |
| 752 | `page-request-gc.spec.ts` | New `DirectPageRequestGCParityTests.cs`. Done. |
| 753 | `frame-frame-element.spec.ts` | New `DirectFrameFrameElementParityTests.cs`. Done. |
| 754 | `locator-element-handle.spec.ts` | New `DirectLocatorElementHandleParityTests.cs`. Done. |
| 755 | `queryselector.spec.ts` | New `DirectQuerySelectorParityTests.cs`. Done. |
| 756 | `page-event-network.spec.ts` | New `DirectPageEventNetworkParityTests.cs`. Done. |
| 757 | `page-cache-storage.spec.ts` | New `DirectPageCacheStorageParityTests.cs`. Done. |
| 758 | `locator-get.spec.ts` | New `DirectLocatorGetParityTests.cs`. Done. |
| 759 | `page-network-response.spec.ts` | New `DirectPageNetworkResponseParityTests.cs`. Done. |
| 760 | `page-network-request.spec.ts` | New `DirectPageNetworkRequestParityTests.cs`. Done. |
| 761 | `page-network-sizes.spec.ts` | New `DirectPageNetworkSizesParityTests.cs`. Done. |
| 762 | `elementhandle-content-frame.spec.ts` | New `DirectElementHandleContentFrameParityTests.cs`. Done. |
| 763 | `elementhandle-owner-frame.spec.ts` | New `DirectElementHandleOwnerFrameParityTests.cs`. Done. |
| 764 | `elementhandle-query-selector.spec.ts` | New `DirectElementHandleQuerySelectorParityTests.cs`. Done. |
| 765 | `elementhandle-select-text.spec.ts` | New `DirectElementHandleSelectTextParityTests.cs`. Done. |
| 766 | `elementhandle-eval-on-selector.spec.ts` | New `DirectElementHandleEvalOnSelectorParityTests.cs`. Done. |
| 767 | `elementhandle-scroll-into-view.spec.ts` | New `DirectElementHandleScrollIntoViewParityTests.cs`. Done. |
| 768 | `elementhandle-wait-for-element-state.spec.ts` | New `DirectElementHandleWaitForElementStateParityTests.cs`. Done. |
| 769 | `page-wait-for-navigation.spec.ts` | New `DirectPageWaitForNavigationParityTests.cs`. Done. |
| 770 | `page-goto.spec.ts` | New `DirectPageGotoParityTests.cs`. Done. |
| 771 | `page-click.spec.ts` | New `DirectPageClickParityTests.cs`. Done. |
| 772 | `page-route.spec.ts` | New `DirectPageRouteParityTests.cs`. Done. |
| 773 | `page-request-continue.spec.ts` | New `DirectPageRequestContinueParityTests.cs`. Done. |
| 774 | `page-request-fulfill.spec.ts` | New `DirectPageRequestFulfillParityTests.cs`. Done. |
| 775 | `page-request-fallback.spec.ts` | New `DirectPageRequestFallbackParityTests.cs`. Done. |
| 776 | `page-request-intercept.spec.ts` | New `DirectPageRequestInterceptParityTests.cs`. Done. |
| 777 | `locator-list.spec.ts` | New `DirectLocatorListParityTests.cs`. Done. |
| 778 | `network-post-data.spec.ts` | New `DirectNetworkPostDataParityTests.cs`. Done. |
| 779 | `locator-wait-for-function.spec.ts` | New `DirectLocatorWaitForFunctionParityTests.cs`. Done. |
| 780 | `page-set-input-files.spec.ts` | New `DirectPageSetInputFilesParityTests.cs`. Done. |
| 781 | `retarget.spec.ts` | New `DirectRetargetParityTests.cs`. Done. |
| 782 | `interception.spec.ts` | New `DirectInterceptionParityTests.cs`. |
| 793 | `page-wait-for-selector-2.spec.ts` | `DirectPageWaitForSelector2ParityTests.cs`. Done. |
| 794 | `page-wait-for-selector-1.spec.ts` | Remaining titles → `DirectPageWaitForSelector1ParityTests.cs`. Done. |
| 795 | `selectors-register.spec.ts` | `DirectSelectorsRegisterParityTests.cs`. Done. |
| 796 | `selectors-misc.spec.ts` | `DirectSelectorsMiscParityTests.cs`. Done. |
| 797 | `selectors-css.spec.ts` | `DirectSelectorsCssParityTests.cs`. Done. |
| 798 | `selectors-get-by.spec.ts` | `DirectSelectorsGetByParityTests.cs`. Done. |
| 799 | `selectors-text.spec.ts` | `DirectSelectorsTextParityTests.cs`. Done. |
| 800 | `selectors-role.spec.ts` | `DirectSelectorsRoleParityTests.cs`. Done. |
| 801 | `selectors-frame.spec.ts` | `DirectSelectorsFrameParityTests.cs`. Done. |
| 802 | `locator-highlight.spec.ts` | `DirectLocatorHighlightParityTests.cs`. Done. |
| 803 | `library/locator-highlight.spec.ts` | `DirectLibraryLocatorHighlightParityTests.cs`. Done. |
| 804 | `expect-to-have-accessible.spec.ts` | `DirectExpectToHaveAccessibleParityTests.cs`. Done. |
| 805 | `expect-boolean.spec.ts` | `DirectExpectBooleanParityTests.cs`. Done. |
| 806 | `expect-to-have-text.spec.ts` | `DirectExpectToHaveTextParityTests.cs`. Done. |
| 807 | `expect-to-have-value.spec.ts` | `DirectExpectToHaveValueParityTests.cs`. Done. |
| 808 | `expect-misc.spec.ts` | `DirectExpectMiscParityTests.cs`. Done. |
| 809 | `expect-timeout.spec.ts` | `DirectExpectTimeoutParityTests.cs`. Done. |
| 810 | `expect-matcher-result.spec.ts` | `DirectExpectMatcherResultParityTests.cs`. Done. |
| 811 | `expect-with-snapshot.spec.ts` | `DirectExpectWithSnapshotParityTests.cs`. Done. |
| 812 | `matchers.misc.spec.ts` | `DirectMatchersMiscParityTests.cs`. Done. |
| 813 | `to-match-aria-snapshot.spec.ts` | `DirectToMatchAriaSnapshotParityTests.cs`. Done. |
| 814 | `page-aria-snapshot.spec.ts` | `DirectPageAriaSnapshotParityTests.cs`. Done. |
| 815 | `page-aria-snapshot-ai.spec.ts` | `DirectPageAriaSnapshotAiParityTests.cs`. Done. |
| 816 | `page-aria-snapshot-json.spec.ts` | `DirectPageAriaSnapshotJsonParityTests.cs`. Done. |
| 817 | `page-evaluate-callback.spec.ts` | `DirectPageEvaluateCallbackParityTests.cs`. Done. |
| 818 | `page-add-init-script-callback.spec.ts` | `DirectPageAddInitScriptCallbackParityTests.cs`. Done. |
| 819 | `locator-any-frame.spec.ts` | `DirectLocatorAnyFrameParityTests.cs`. Done. |
| 820 | `selectors-frame.spec.ts` remaining any-frame titles | `DirectSelectorsFrameAnyFrameParityTests.cs`. Done. |
| 821 | `page-evaluate.spec.ts` | `DirectPageEvaluateParityTests.cs`. Done. |
| 822 | `page-wait-for-url.spec.ts` remaining titles | `DirectPageWaitForUrlParityTests.cs`. Done. |
| 823 | `page-wait-for-response.spec.ts` remaining titles | `DirectPageWaitForResponseParityTests.cs`. Done. |
| 824 | `page-wait-for-request.spec.ts` remaining titles | `DirectPageWaitForRequestParityTests.cs`. Done. |
| 825 | `page-localstorage.spec.ts` remaining origin title | `DirectPageLocalStorageOriginParityTests.cs`. Done. (`page-click-scroll` remaining titles already in `DirectPageClickScrollParityTests`.) |
| 826 | `library/page-close.spec.ts` | `DirectLibraryPageCloseParityTests.cs`. Official page.close. Done. |
| 827 | `library/beforeunload.spec.ts` | `DirectLibraryBeforeUnloadParityTests.cs`. Official beforeunload. Done. |
| 828 | `library/geolocation.spec.ts` | `DirectLibraryGeolocationParityTests.cs`. Official geolocation. Done. |
| 829 | `library/browsercontext-add-cookies.spec.ts` | `DirectLibraryBrowserContextAddCookiesParityTests.cs`. Official addCookies. Done. |
| 830 | `library/browsercontext-cookies.spec.ts` | `DirectLibraryBrowserContextCookiesParityTests.cs`. Official context.cookies. Done. |
| 831 | `library/browsercontext-clearcookies.spec.ts` | `DirectLibraryBrowserContextClearCookiesParityTests.cs`. Official clearCookies. Done. |
| 832 | `library/browsercontext-add-init-script.spec.ts` | `DirectLibraryBrowserContextAddInitScriptParityTests.cs`. Official context.addInitScript. Done. |
| 833 | `library/browsercontext-base-url.spec.ts` | `DirectLibraryBrowserContextBaseUrlParityTests.cs`. Official context baseURL. Done. |
| 834 | `library/browsercontext-basic.spec.ts` | `DirectLibraryBrowserContextBasicParityTests.cs`. Official context create/close, isolation, viewport, offline, javascript. Done. |
| 835 | `library/browsercontext-credentials.spec.ts` | `DirectLibraryBrowserContextCredentialsParityTests.cs`. Official HTTP credentials / setHTTPCredentials. Done. |
| 836 | `library/browsercontext-csp.spec.ts` | `DirectLibraryBrowserContextCspParityTests.cs`. Official bypassCSP. Done. |
| 837 | `library/browsercontext-device.spec.ts` | `DirectLibraryBrowserContextDeviceParityTests.cs`. Official device descriptors. Done. |
| 838 | `library/browsercontext-dsf.spec.ts` | `DirectLibraryBrowserContextDsfParityTests.cs`. Official deviceScaleFactor. Done. |
| 839 | `library/browsercontext-events.spec.ts` | `DirectLibraryBrowserContextEventsParityTests.cs`. Official context waitForEvent. Done. |
| 840 | `library/browsercontext-expose-function.spec.ts` | `DirectLibraryBrowserContextExposeFunctionParityTests.cs`. Official context.exposeFunction / exposeBinding. Done. |
| 841 | `library/browsercontext-fetch.spec.ts` | `DirectLibraryBrowserContextFetchParityTests.cs`. Official context.request / APIRequest. Done. |
| 842 | `library/browsercontext-har.spec.ts` | `DirectLibraryBrowserContextHarParityTests.cs`. Official context HAR record/routeFromHAR. Done. |
| 843 | `library/browsercontext-locale.spec.ts` | `DirectLibraryBrowserContextLocaleParityTests.cs`. Official context locale. Done. |
| 844 | `library/browsercontext-network-event.spec.ts` | `DirectLibraryBrowserContextNetworkEventParityTests.cs`. Official context request/response events. Done. |
| 845 | `library/browsercontext-page-event.spec.ts` | `DirectLibraryBrowserContextPageEventParityTests.cs`. Official context page events. Done. |
| 846 | `library/browsercontext-pages.spec.ts` | `DirectLibraryBrowserContextPagesParityTests.cs`. Official context.pages, page.context, multi-page focus/click. Done. |
| 847 | `library/browsercontext-proxy.spec.ts` | `DirectLibraryBrowserContextProxyParityTests.cs`. Official context proxy. |
| 879 | `library/har.spec.ts` | `DirectLibraryHarParityTests.cs`. Official recordHar. Done. |
| 880 | `library/har-websocket.spec.ts` | `DirectLibraryHarWebsocketParityTests.cs`. Official HAR websocket entries. Done. |
| 881 | `library/headful.spec.ts` | `DirectLibraryHeadfulParityTests.cs`. Official headed launch / persistent context. Done. |
| — | `library/heap.spec.ts` | Skip: Node-only `node:inspector` heap instrumentation. |
| 882 | `library/hit-target.spec.ts` | `DirectLibraryHitTargetParityTests.cs`. Official hit-target click blocking. Done. |
| 883 | `library/modernizr.spec.ts` | `DirectLibraryModernizrParityTests.cs`. Official Modernizr feature matrix. Done. |
| 884 | `library/page-clock.spec.ts` | `DirectLibraryPageClockParityTests.cs`. Official page.clock. Done. |
| 885 | `library/page-clock.frozen.spec.ts` | `DirectLibraryPageClockFrozenParityTests.cs`. Official PW_CLOCK frozen/realtime fixture. Done. |
| 886 | `library/page-event-crash.spec.ts` | `DirectLibraryPageEventCrashParityTests.cs`. Official page crash event. Done. |
| 887 | `library/pdf.spec.ts` | `DirectLibraryPdfParityTests.cs`. Official page.pdf. Done. |
| 888 | `library/proxy.spec.ts` | `DirectLibraryProxyParityTests.cs`. Official launch-level proxy. Done. |
| 889 | `library/proxy-pattern.spec.ts` | `DirectLibraryProxyPatternParityTests.cs`. Official SOCKS parsePattern. Done. |
| 890 | `library/resource-timing.spec.ts` | `DirectLibraryResourceTimingParityTests.cs`. Official request.timing. Done. |
| 891 | `library/route-web-socket.spec.ts` | `DirectLibraryRouteWebSocketParityTests.cs`. Official page.routeWebSocket. Done. |
| 892 | `library/shared-worker.spec.ts` | `DirectLibrarySharedWorkerParityTests.cs`. Official SharedWorker restart. Done. |
| — | `library/signals.spec.ts` | Skip: Node-only `launchServer` / `process.kill`. |
| — | `library/slowmo.spec.ts` | Skip: Node-only `toImpl` / `_doSlowMo`. |
| 893 | `library/tap.spec.ts` | `DirectLibraryTapParityTests.cs`. Official page.tap. Chromium 9/9 + leftover 9/9; WebKit 9/9 + leftover 6/6. Done. |
| — | `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` | Skip: Node-only trace viewer UI. |
| 894 | `library/tracing.spec.ts` | `DirectLibraryTracingParityTests.cs`. Official context.tracing. Chromium 32/32 + leftover 14/14; WebKit 32/32 + leftover 13/13 + 1 leftover Chromium-only skip. Done. |
| 895 | `library/web-socket.spec.ts` | `DirectLibraryWebSocketParityTests.cs`. Official page.WebSocket. Chromium 11/11 + leftover 5/5 + 3 official skips; WebKit 13/13 + leftover 5/5 + 1 official offline skip. Done. |
| 896 | `library/locator-dispatchevent-touch.spec.ts` | `DirectLibraryLocatorDispatchEventTouchParityTests.cs`. Official locator.dispatchEvent touch points. Chromium 1/1; WebKit 1/1. Done. |
| — | `library/role-utils.spec.ts` | Skip: Node-only `__injectedScript` internals. |
| 922 | `library/screencast-actions.spec.ts` | Official `screencast.showActions`. See Wave 922. |
| 923 | `library/screencast-overlay.spec.ts` | Official `screencast.showOverlay`. See Wave 923. |
| 897 | `library/selectors-register.spec.ts` | `DirectLibrarySelectorsRegisterParityTests.cs`. Official playwright.selectors.register. Chromium 7/7 + leftover 6/6 + page 6/6 + 1 official skip; WebKit 7/7 + leftover 6/6 + page 6/6 + 1 official skip. Done. |
| 898 | `library/unroute-behavior.spec.ts` | `DirectLibraryUnrouteBehaviorParityTests.cs`. Official page/context unroute wait vs ignoreErrors. Chromium 16/16 + leftover 8/8; WebKit 16/16 + leftover 8/8. Done. |
| 899 | `library/chromium/css-coverage.spec.ts` | `DirectLibraryCssCoverageParityTests.cs`. Official page.coverage CSS. Chromium 10/10 + leftover 5/5; WebKit 0/0 + 10 official Chromium-only skips + leftover 5 leftover Chromium-only skips. Done. |
| 900 | `library/chromium/js-coverage.spec.ts` | `DirectLibraryJsCoverageParityTests.cs`. Official page.coverage JS. Chromium 7/7 + leftover 5/5; WebKit 0/0 + 7 official Chromium-only skips + leftover 5 leftover Chromium-only skips. Done. |
| — | `library/chromium/connect-to-worker.spec.ts` | Skip: Node-only `_connectToWorker` / Node `--inspect-brk`. |
| 901 | `library/chromium/disable-web-security.spec.ts` | `DirectLibraryDisableWebSecurityParityTests.cs`. Official `--disable-web-security` popup utility world and init script. Chromium 2/2; WebKit 0/0 + 2 official Chromium-only skips. Done. |
| 902 | `library/chromium/bfcache.spec.ts` | `DirectLibraryBfcacheParityTests.cs`. Official exposeFunction after back-forward cache restore. Chromium 1/1; WebKit 0/0 + 1 official Chromium-only skip. Done. |
| 903 | `library/chromium/session.spec.ts` | `DirectLibrarySessionParityTests.cs`. Official page/context newCDPSession. Chromium 14/14 + 1 official Node skip; leftover 5/5 + 1 leftover WebKit skip; WebKit 0/0 + 15 official Chromium-only skips + leftover 1/1 + leftover 5 leftover Chromium-only skips. Done. |
| 904 | `library/chromium/chromium.spec.ts` | `DirectLibraryChromiumParityTests.cs`. Official Chromium service-worker workers, routing, HAR, console, and persistent-context CDP. Chromium 27/27 + 4 official skips; leftover SW 9/9; WebKit 0/0 + 31 official Chromium-only skips. Done. |
| 905 | `library/chromium/oopif.spec.ts` | `DirectLibraryOopifParityTests.cs`. Official out-of-process iframe CDP sessions and routing. Chromium 24/24 + 3 official skips; leftover connect-over-cdp 3/3. WebKit 0/0 + 27 official Chromium-only skips. Done. |
| 906 | `library/chromium/extensions.spec.ts` | `DirectLibraryExtensionsParityTests.cs`. Official Chromium MV3 extension service workers, content-script console, and SW fetch UA. Chromium 5/5 + 1 official skip (`browserMajorVersion < 143`); leftover ignore-default-args/UA/video 6/6. WebKit 0/0 + 6 official Chromium-only skips. Done. |
| 907 | `library/chromium/connect-over-cdp.spec.ts` | `DirectLibraryConnectOverCdpParityTests.cs`. Official Chromium `connectOverCDP` endpoints, existing pages, traces, downloads, proxy, and `noDefaults`. Chromium 28/28 + 4 official Node skips (`toImpl` artifacts/`isLocal`/utility world, in-process `ConnectionTransport`); leftover connect-over-cdp 3/3; leftover oopif reconnect 1/1. WebKit 0/0 + 32 official Chromium-only skips + leftover 3 leftover Chromium-only skips. Done. |
| 908 | `library/chromium/launcher.spec.ts` | `DirectLibraryChromiumLauncherParityTests.cs`. Official Chromium launchServer remote-debugging args and `newBrowserCDPSession` target discovery. Chromium 3/3; leftover launcher 3/3; leftover connect-over-cdp 3/3; leftover oopif reconnect 1/1. WebKit 0/0 + 3 official Chromium-only skips + leftover launcher 3/3. Done. |
| 909 | `library/chromium/tracing.spec.ts` | `DirectLibraryChromiumTracingParityTests.cs`. Official Chromium `browser.startTracing` / `stopTracing` (CDP Tracing, not context.tracing). Chromium 7/7; leftover context tracing 2/2. WebKit 0/0 + 7 official Chromium-only skips + leftover 1/1 + leftover 1 leftover Chromium-only skip. Done. |
| 910 | `library/client-certificates.spec.ts` | `DirectLibraryClientCertificatesParityTests.cs`. Official context/APIRequest client certificates (SOCKS5 MITM). Chromium 45/45; leftover context/API/persistent/client-cert green. WebKit 44/44 + 1 official skip (`support http2 if the browser only supports http1.1`). Done. |
| 911 | `page/expect-builtins.spec.ts` | `DirectExpectBuiltinsParityTests.cs`. Official Jest-style expect builtins (toBe/toEqual/toThrow/asymmetric matchers). Chromium 278/278; WebKit 278/278. Done. Portable spec-file set exhausted; continue title-level `[PlaywrightTest("spec.ts", "title")]` holes. |
| 912 | PlaywrightTest title holes | `toBeOK fail with invalid argument` (`DirectExpectBooleanParityTests`); `display:contents should be visible when contents are visible` (`DirectLibraryRoleUtilsParityTests`); `should work with ip6 and port as the host` (`DirectLibraryBrowserContextFetchHappyEyeballsParityTests`). Chromium 3/3 new + expect-boolean 85/85; WebKit 3/3 new. Done. |
| 913 | `page-screenshot.spec.ts` | `DirectPageScreenshotParityTests.cs`. Official path/quality/clip validation and non-golden titles. Chromium 9/9; WebKit 9/9. Removed blanket TestExpectations skip for `page-screenshot.spec.ts`. Done. |
| 914 | `page-screenshot.spec.ts` / `elementhandle-screenshot.spec.ts` pixel-diff | `DirectPageScreenshotParityTests` / `DirectElementHandleScreenshotParityTests` / `OfficialSnapshot`. Official `toMatchSnapshot` goldens plus document-rect `captureBeyondViewport`. Chromium 38/38; WebKit 38/38. Removed blanket TestExpectations skip for `elementhandle-screenshot.spec.ts`. Done. |
| 915 | `page-screenshot.spec.ts` mask/caret | Official mask-option goldens and caret hide-by-default. Chromium 52/52; WebKit 52/52. Done. |
| 916 | `page-request-fulfill.spec.ts` snapshots | Official binary/svg/path fulfill goldens. `FulfillAsync(path)` MIME from file. Chromium 22/22 + 1 official skip; WebKit 3/3 new. Done. |
| 917 | `screencast.spec.ts` | `DirectLibraryScreencastParityTests`. Official page.screencast start/stop/onFrame. Chromium 12/12; WebKit 12/12. Done. |
| 918 | `page-screenshot.spec.ts` animations | Official animation disable/resume/events, Array-deleted, jpeg path. Chromium 63/63; WebKit 63/63. Done. |
| 919 | `page-screenshot.spec.ts` / `elementhandle-screenshot.spec.ts` remaining titles | Official webp/quality/fonts/navigation/canvas/webgl/box-shadow and element wait-for-visible. Chromium 86/86; WebKit 85/85 + 1 official skip. Done. |
| 920 | `library/screenshot.spec.ts` | Official mobile/DSF/scale/null-viewport/vh/large/element-mobile. Chromium 22/22 + 1 official skip; WebKit 22/22 + 1 official skip. Done. |
| 921 | `library/video.spec.ts` | `DirectLibraryVideoParityTests`. Official recordVideo size/path/SaveAs/empty-ffmpeg/video+trace. Chromium 31/31 + 3 official skips; WebKit 28/28. Done. |
| 922 | `library/screencast-actions.spec.ts` | `DirectLibraryScreencastActionsParityTests`. Official showActions highlight/point/title/cursor. Chromium 15/15 + leftover 2/2; WebKit 15/15 + leftover 2/2. Done. |
| 923 | `library/screencast-overlay.spec.ts` | `DirectLibraryScreencastOverlayParityTests`. Official showOverlay host/sanitize/navigation. Chromium 9/9 + leftover 3/3; WebKit 9/9 + leftover 3/3. Done. |
| 924 | HAR / locator-query title holes | `DirectLibraryHarParityTests` / `DirectLocatorQueryTests`. Official startHar pages/resourcesDir, APIRequestContext HAR, locator capture prefix. Chromium new 7/7; WebKit new 7/7. Done. |
| 925 | remaining portable title holes | Screenshot animation-before-capture, public click-during-navigation, AbortSignal, scroll none. |
| — | `page-click-during-navigation.spec.ts` | Skip: Node-only `__testHookAfterStable`. |
| — | `page-evaluate-no-stall.spec.ts` | Skip: Node-only `toImpl` / `nonStallingRawEvaluateInExistingMainContext`. |
| — | `page-leaks.spec.ts` | Skip: Node-only `toImpl` / leaked JSHandles. |
| — | `library/events/*.spec.ts` | Skip: Node `EventEmitter` internals. |
| — | `library/unit/*.spec.ts` | Skip: Node `playwright-core` internals (`toImpl` / injected clock / trace-viewer codegen / Android). |
| — | `library/firefox/launcher.spec.ts` | Skip: Firefox-only (campaign gates Chromium+WebKit). |
| — | `library/browser-server.spec.ts` / `browsertype-connect*` / `browsertype-launch-server` / `browsertype-launch-selenium` / `browsers-path.spec.ts` / `channels.spec.ts` | Skip: Node driver / `launchServer`. |
| — | `library/debug-controller.spec.ts` / `debugger.spec.ts` / `inspector/*` / `heap.spec.ts` / `signals.spec.ts` / `slowmo.spec.ts` / `trace-viewer*.spec.ts` | Skip: Node inspector / `toImpl` / Node trace viewer. |
| — | `library/locator-generator.spec.ts` / `selector-generator.spec.ts` / `css-parser.spec.ts` / `component-parser.spec.ts` / `snapshot-renderer.spec.ts` | Skip: Node `__injectedScript` / `iso.asLocator`. Remaining `role-utils.spec.ts` Node `__injectedScript` / WPT (Wave 912 ported the public getByRole title). |
| — | `library/playwright-client.spec.ts` / `multiclient.spec.ts` / `browsercontext-reuse.spec.ts` / `chromium/connect-to-worker.spec.ts` | Skip: Node client bundle / `_newContextForReuse` / `_connectToWorker`. Remaining `browsercontext-fetch-happy-eyeballs.spec.ts` Node `__testHookLookup` (Wave 912 ported IPv6 GET). |




After 762, continue with unported `tests/page/*.spec.ts` then `tests/library/`.
