# Task Tracker

The Node.js driver is gone. Public launch matches upstream playwright-dotnet:
`Playwright.CreateAsync()` → `playwright.Chromium.LaunchAsync()` (also Firefox /
WebKit). Static `Playwright.Chromium` and `LaunchChromiumAsync` helpers remain.
`Transport/` is browser pipe/WebSocket I/O, not Channel RPC.

This file is a historical wave log plus the completed driver-removal checklist.
It is not a new roadmap. See GitHub issues for current work.


## Current Phase: Upstream test parity — exhausted (no remaining portable titles)

## Previous: Drop `Direct` test filename/class prefix

- [x] Rename `src/PlaywrightNative.Tests/Direct/Direct*.cs` to drop the `Direct` filename and class prefix (`DirectPageFocusTests` → `PageFocusTests`). Keep the `Direct/` folder, `PlaywrightNative.Tests.Direct` namespace, and `[Category("DirectConnection")]`. Historical wave notes below still use the old names.

## Previous: Upstream test parity — exhausted (no remaining portable titles)

Upstream test parity campaign: follow `tasks/upstream-test-parity-campaign.md`. Title-level audit after Wave 930: every leftover official `tests/page` and `tests/library` title that can run on direct Chromium/WebKit already has a local `[PlaywrightTest]` twin on `origin/main`. Remaining official titles are Node-only internals (`toImpl`, `__testHook*`, `__injectedScript`, `_channel`/`_instrumentation`/`_newContextForReuse`, `killForTests`, `window.builtins`, `process.on`, EventEmitter listener leaks, Node `Playwright/… node/X.X` UA, JS `URLPattern`, typed-C# `proxy.server: 123`, `@playwright/test` matcherResult attachments) or whole Node-only specs (connect/launchServer/inspector/events/unit/trace-viewer/signals/heap/slowmo/debugger/MCP/Electron/Android/BiDi/Firefox launcher).

## Previous: Wave 931 — leftover-title audit (no portable titles)

### Wave 931
- [x] Re-audit official `tests/page` + `tests/library` against local `[PlaywrightTest("spec.ts", "exact title")]` pairs (including escaped-quote titles and `@smoke` suffixes). Zero remaining portable Chromium/WebKit titles. Skip Node-only `__testHook*` / `toImpl` / `killForTests` / `process.on` / JS `URLPattern` / Firefox launcher prefs. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 930 — emulation-focus screenshot and exact @smoke titles

### Wave 930
- [x] Port official `[PlaywrightTest]` `emulation-focus.spec.ts` `should not affect screenshots` and exact `@smoke` titles on already-covered page/library APIs. Chromium leftover leftover emulation-focus 11/11; WebKit leftover leftover 11/11. Skip Node-only `__testHook*` / `toImpl` / `killForTests` / `process.on` / JS `URLPattern` / Firefox launcher prefs. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 929 — screencast artifactsDir and toHaveURL invalid argument

### Wave 929
- [x] Port official `[PlaywrightTest]` `screencast.spec.ts` `start/stop twice without path creates two files in artifactsDir` and `expect-misc.spec.ts` `fail with invalid argument`. Library: launch `artifactsDir` stores screencast webm files; `ToHaveURLAsync(object)` throws official `expected value must be a string or regular expression`. Chromium new titles 2/2 + leftover leftover expect-misc/screencast 73/73. WebKit new titles + leftover leftover screencast 14/14 and expect-misc 60/60. Skip Node-only `__testHook*` / `toImpl` / `killForTests` / `process.on` / JS `URLPattern` / pixel-diff / Firefox launcher prefs. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 928 — HAR fulfill and highlight object style

### Wave 928
- [x] Port official `[PlaywrightTest]` `page-request-fulfill.spec.ts` `should fulfill with har response` and `locator-highlight.spec.ts` `highlight should accept an object style (JS only)`. Library: `ILocator.HighlightAsync(IReadOnlyDictionary<string,string>)` camelCase-to-CSS. Chromium new titles 2/2 + leftover leftover highlight 4/4 and fulfill 23/23 (1 official skip). WebKit new titles + leftover leftover highlight 6/6; leftover leftover fulfill WebKit route flakes not weakened. Skip Node-only `__testHook*` / `toImpl` / `killForTests` / `process.on` / HAR-fixture-lookup / JS `URLPattern` / pixel-diff / Firefox launcher prefs. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 927 — goto and waitForSelector string options

### Wave 927
- [x] Port official `[PlaywrightTest]` `page-goto.spec.ts` `should throw if networkidle2 is passed as an option`, `page-wait-for-selector-1.spec.ts` `should throw on waitFor` / `should tolerate waitFor=visible`, and `page-wait-for-selector-2.spec.ts` unknown/visibility/true/false state titles. Library: public `GoToAsync(string waitUntil)` and `WaitForSelectorAsync` `waitFor`/`visibility`/string/bool `state` with official errors. Chromium new titles 7/7 + leftover leftover 104/104 (6 official skips). WebKit new titles + waitForSelector leftover leftover 49/49; leftover leftover goto 30s hangs and connection-refused wording are pre-existing WebKit flakes (do not weaken). Skip Node-only listener-leak / `__testHook*` / `toImpl` / HAR-fixture / JS-only object / pixel-diff title holes. Skip Firefox-only `library/firefox/launcher.spec.ts` prefs/policies (not Chromium/WebKit-runnable). Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 926 — click and waitForFunction AbortError signal

### Wave 926
- [x] Port official `[PlaywrightTest]` `page-click.spec.ts` abort-signal titles and `locator-wait-for-function.spec.ts` abort-signal titles. Library: public `AbortError` (`cause`) and `signal` on `ILocator.ClickAsync` / `WaitForFunctionAsync`. Chromium new titles 6/6; leftover click/waitForFunction green except pre-existing `should click into shadow root with slotted div` hang (reproduced on origin/main). WebKit new titles + waitForFunction 14/14. Skip Node-only `__testHook*` click titles. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 925 — expect abort signal and load-state string

### Wave 925
- [x] Port official `[PlaywrightTest]` `expect-timeout.spec.ts` abort-signal titles, `expect-boolean.spec.ts` `toBeOK fail with promise`, and `page-wait-for-load-state.spec.ts` `should throw for bad state`. Library: public `AbortController`/`AbortSignal` on `ToBeVisible`/`ToHaveText`/`ToHaveCount`/`ToMatchAriaSnapshot`/`ToHaveURL`, `toBeOK` rejects non-`APIResponse`, `WaitForLoadStateAsync(string)` official invalid-state error. Chromium expect/load-state 124/124. WebKit new titles 8/8; leftover leftover toBeOK HTTP and load-state navigation flakes retried. Skip Node-only `__testHook*` / `toImpl` / `killForTests` / `__injectedScript` / JS `URLPattern` title holes. Skip remaining click/waitForFunction `signal` (`AbortError` name + cause). Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 924 — HAR and locator-query title holes

### Wave 924
- [x] Port official `[PlaywrightTest("har.spec.ts" / "locator-query.spec.ts", title)]` `should include pages`, zipped/cookie/content-type APIRequestContext HAR, `resourcesDir` attach/reject-zip, and locator capture-before-frame. Library: `ITracing.StartHarAsync(resourcesDir)` and `Locator.Locator` frame-prefix strip. Chromium new HAR 6/6 + leftover HAR green; locator new 1/1. WebKit new titles 7/7. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 923 — library/screencast-overlay.spec.ts

### Wave 923
- [x] Port official `[PlaywrightTest("screencast-overlay.spec.ts", title)]` add/remove/multiple/hide/show/navigation/sanitize/timeout/style titles. Library: official `x-pw-user-overlays` / `.x-pw-user-overlay`, script and `on*` sanitization, init-script re-inject. Chromium 9/9 + leftover 3/3; WebKit 9/9 + leftover 3/3. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 922 — library/screencast-actions.spec.ts

### Wave 922
- [x] Port official `[PlaywrightTest("screencast-actions.spec.ts", title)]` showActions highlight/point/title/cursor/position/duration/fill/dispose/hide/navigation titles. Library: official `x-pw-*` custom elements and 6px title offsets; leftover `data-pw-screencast-action-*` kept. Chromium 15/15 + leftover 2/2; WebKit 15/15 + leftover 2/2. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/screencast-overlay.spec.ts`. Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 921 — library/video.spec.ts

### Wave 921
- [x] Port official `[PlaywrightTest("video.spec.ts", title)]` recordVideo size/path/SaveAs/empty-ffmpeg/video+trace titles. Library: official 800x800 default size, immediate `video.path()`, SaveAs throws `browser has been closed`, coalesced stop so context/browser close waits for files, tracing last-frame prefers video JPEG. Chromium 31/31 + 3 official skips (`chromium && !isHeadlessShell` full viewport/hidpi/scale); WebKit 28/28. Skip Node `_channel.killForTests` (`should throw if browser dies`). Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/screencast-actions.spec.ts` / `library/screencast-overlay.spec.ts`. Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 920 — library/screenshot.spec.ts

### Wave 920
- [x] Port official `[PlaywrightTest("screenshot.spec.ts", title)]` mobile/DSF/scale/null-viewport/vh/large/element-mobile titles. Library: Chromium document-rect fullPage clip (`_fullPageSize`) via CDP `returnByValue` (deleted page `Array` safe) and WebKit navigation-error remap. Chromium 22/22 + 1 official skip (`should work if the main resource hangs`); WebKit 22/22 + 1 official skip (`page screenshot should capture css transform with device pixels`). Skip Node `__testHook*` titles. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast-actions.spec.ts` / `library/screencast-overlay.spec.ts`. Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 919 — remaining page/element screenshot titles

### Wave 919
- [x] Port official `[PlaywrightTest("page-screenshot.spec.ts" / "elementhandle-screenshot.spec.ts", title)]` webp/quality/fonts/navigation/canvas/webgl/box-shadow and element wait-for-visible/stable. Chromium 86/86; WebKit 85/85 + 1 official skip (`page screenshot should capture css transform`). Skip Node `__testHookBeforeScreenshot` and `window.builtins.Date` transitionend layout title. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 918 — page-screenshot animations

### Wave 918
- [x] Port official `[PlaywrightTest("page-screenshot.spec.ts", title)]` animation disable/resume, shadow-root, event finish/cancel, Array-deleted fullPage, jpeg path. Chromium 63/63; WebKit 63/63. Skip `__testHookBeforeScreenshot`, fonts, navigation-during-screenshot, webp/mobile remaining. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 917 — screencast.spec.ts

### Wave 917
- [x] Port official `[PlaywrightTest("screencast.spec.ts", title)]` start/stop/onFrame/backpressure/recordVideo/empty video titles. Library: WebKit `Screencast.startScreencast` and Chromium ack/path. Chromium 12/12; WebKit 12/12. Skip Node-only `start/stop twice without path creates two files in artifactsDir`. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 916 — page-request-fulfill snapshots

### Wave 916
- [x] Port official `[PlaywrightTest("page-request-fulfill.spec.ts", title)]` `should allow mocking binary responses`, `should allow mocking svg with charset`, `should work with file path`. Library: `FulfillAsync(path)` takes MIME from the file. Chromium 22/22 + 1 official skip; WebKit 3/3 new snapshots. Skip Node HAR fixture `should fulfill with har response`. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 915 — page-screenshot mask and caret

### Wave 915
- [x] Port official `[PlaywrightTest("page-screenshot.spec.ts", title)]` mask-option goldens, caret hide-by-default, and stalled-frame titles. Library: official inline caret hide and per-page screenshot queue. Chromium 52/52; WebKit 52/52 (page+element screenshot parity). Skip remaining webp/mobile/navigation/font titles and Node `__testHookBeforeScreenshot`. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 914 — page-screenshot / elementhandle-screenshot snapshots

### Wave 914
- [x] Port official `[PlaywrightTest]` `toMatchSnapshot` titles: page-screenshot `should work @smoke`, clip/fullPage/canvas/path/hide/remove/animation goldens; elementhandle-screenshot work/padding/larger-than-viewport/scroll/rotate/fractional/path. Library: document-rect element screenshots with `captureBeyondViewport`. Chromium 38/38; WebKit 38/38. Skip remaining mask/caret/webp/mobile/navigation titles and wait-for-visible element screenshots. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 913 — page-screenshot.spec.ts PlaywrightTest titles

### Wave 913
- [x] Port official `[PlaywrightTest("page-screenshot.spec.ts", title)]` validation titles: clip outside/negative, restore viewport, path mime, png quality, jpeg quality, type-over-extension, no resize event. Chromium 9/9; WebKit 9/9; leftover jpeg/webp/path green. Skip remaining pixel-diff snapshot titles and screenshot-during-navigation (`should work while navigating`, `should take fullPage screenshots during navigation`). Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / remaining `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 912 — PlaywrightTest title holes

### Wave 912
- [x] Port official `[PlaywrightTest]` titles missing locally: `expect-boolean.spec.ts` `toBeOK fail with invalid argument`; `role-utils.spec.ts` `display:contents should be visible when contents are visible`; `browsercontext-fetch-happy-eyeballs.spec.ts` `should work with ip6 and port as the host`. Chromium 3/3 new + expect-boolean 85/85; WebKit 3/3 new. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip remaining `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and remaining `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 911 — page/expect-builtins.spec.ts

### Wave 911
- [x] Port `tests/page/expect-builtins.spec.ts` → `DirectExpectBuiltinsParityTests.cs`. Official Jest-style expect builtins (toBe/toEqual/toThrow/asymmetric matchers). Chromium 278/278; WebKit 278/278. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 910 — library/client-certificates.spec.ts

### Wave 910
- [x] Port `tests/library/client-certificates.spec.ts` → `DirectLibraryClientCertificatesParityTests.cs`. Official context/APIRequest client certificates (SOCKS5 MITM). Chromium 45/45; leftover context/API/persistent/client-cert green. WebKit 44/44 + 1 official skip (`support http2 if the browser only supports http1.1`). Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 909 — library/chromium/tracing.spec.ts

### Wave 909
- [x] Port `tests/library/chromium/tracing.spec.ts` → `DirectLibraryChromiumTracingParityTests.cs`. Official Chromium `browser.startTracing` / `stopTracing` (CDP Tracing, not context.tracing). Chromium 7/7; leftover context tracing 2/2. WebKit 0/0 + 7 official Chromium-only skips + leftover 1/1 + leftover 1 leftover Chromium-only skip. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 908 — library/chromium/launcher.spec.ts

### Wave 908
- [x] Port `tests/library/chromium/launcher.spec.ts` → `DirectLibraryChromiumLauncherParityTests.cs`. Official Chromium launchServer remote-debugging args and `newBrowserCDPSession` target discovery. Chromium 3/3; leftover launcher 3/3; leftover connect-over-cdp 3/3; leftover oopif reconnect 1/1. WebKit 0/0 + 3 official Chromium-only skips + leftover launcher 3/3. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 907 — library/chromium/connect-over-cdp.spec.ts

### Wave 907
- [x] Port `tests/library/chromium/connect-over-cdp.spec.ts` → `DirectLibraryConnectOverCdpParityTests.cs`. Official Chromium `connectOverCDP` endpoints, existing pages, traces, downloads, proxy, and `noDefaults`. Chromium 28/28 + 4 official Node skips; leftover connect-over-cdp 3/3; leftover oopif reconnect 1/1. WebKit 0/0 + 32 official Chromium-only skips + leftover 3 leftover Chromium-only skips. Do not edit leftover `DirectConnectOverCdpTests`. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 906 — library/chromium/extensions.spec.ts

### Wave 906
- [x] Port `tests/library/chromium/extensions.spec.ts` → `DirectLibraryExtensionsParityTests.cs`. Official Chromium MV3 extension service workers, content-script console, and SW fetch UA. Chromium 5/5 + 1 official skip (`browserMajorVersion < 143` / `workerScriptLoaded`); leftover ignore-default-args/UA/video 6/6; Wave 904 SW 33/33 + 4 official skips. WebKit 0/0 + 6 official Chromium-only skips. Do not edit leftover `DirectConnectOverCdpTests` or leftover CDP session tests. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 905 — library/chromium/oopif.spec.ts

### Wave 905
- [x] Port `tests/library/chromium/oopif.spec.ts` → `DirectLibraryOopifParityTests.cs`. Official out-of-process iframe CDP sessions and routing. Chromium 24/24 + 3 official skips; leftover connect-over-cdp 3/3. WebKit 0/0 + 27 official Chromium-only skips. Do not edit leftover `DirectConnectOverCdpTests` or leftover CDP session tests. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 904 — library/chromium/chromium.spec.ts

### Wave 904
- [x] Port `tests/library/chromium/chromium.spec.ts` → `DirectLibraryChromiumParityTests.cs`. Official Chromium service-worker workers, routing, HAR, console, and persistent-context CDP. Chromium 27/27 + 4 official skips; leftover SW 9/9. WebKit 0/0 + 31 official Chromium-only skips. Do not edit leftover `DirectConnectOverCdpTests` or leftover CDP session tests. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 903 — library/chromium/session.spec.ts

### Wave 903
- [x] Port `tests/library/chromium/session.spec.ts` → `DirectLibrarySessionParityTests.cs`. Official page/context newCDPSession. Chromium 14/14 + 1 official Node skip; leftover 5/5 + 1 leftover WebKit skip. WebKit 0/0 + 15 official Chromium-only skips + leftover 1/1 + leftover 5 leftover Chromium-only skips. Leftover FrameSessionShouldEvaluate aligned to official in-process iframe throw. Do not edit leftover `DirectConnectOverCdpTests`. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 902 — library/chromium/bfcache.spec.ts

### Wave 902
- [x] Port `tests/library/chromium/bfcache.spec.ts` → `DirectLibraryBfcacheParityTests.cs`. Official exposeFunction after back-forward cache restore. Chromium 1/1; WebKit 0/0 + 1 official Chromium-only skip. Do not edit leftover page-history bfcache titles. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 901 — library/chromium/disable-web-security.spec.ts

### Wave 901
- [x] Port `tests/library/chromium/disable-web-security.spec.ts` → `DirectLibraryDisableWebSecurityParityTests.cs`. Official `--disable-web-security` popup utility world and init script. Chromium 2/2; WebKit 0/0 + 2 official Chromium-only skips. Leftover context init-script 12/12. Skip Node-only `library/chromium/connect-to-worker.spec.ts` (`_connectToWorker` / Node `--inspect-brk`). Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 900 — library/chromium/js-coverage.spec.ts

### Wave 900
- [x] Port `tests/library/chromium/js-coverage.spec.ts` → `DirectLibraryJsCoverageParityTests.cs`. Official page.coverage JS function ranges, sourceURL, eval skip, resetOnNavigation, and debugger skip-all-pauses. Chromium 7/7 + leftover 5/5; WebKit 0/0 + 7 official Chromium-only skips + leftover 5 leftover Chromium-only skips. Do not edit leftover `DirectPageCoverageTests`. Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 899 — library/chromium/css-coverage.spec.ts

### Wave 899
- [x] Port `tests/library/chromium/css-coverage.spec.ts` → `DirectLibraryCssCoverageParityTests.cs`. Official page.coverage CSS ranges, sourceURL, injected-sheet skip, and resetOnNavigation. Chromium 10/10 + leftover 5/5; WebKit 0/0 + 10 official Chromium-only skips + leftover 5 leftover Chromium-only skips. Do not edit leftover `DirectPageCoverageTests`. Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 898 — library/unroute-behavior.spec.ts

### Wave 898
- [x] Port `tests/library/unroute-behavior.spec.ts` → `DirectLibraryUnrouteBehaviorParityTests.cs`. Official page/context unroute wait vs ignoreErrors, close-during-handler, and in-flight fetch. Chromium 16/16 + leftover 8/8; WebKit 16/16 + leftover 8/8. Do not edit leftover `DirectUnrouteBehaviorTests`. Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 897 — library/selectors-register.spec.ts

### Wave 897
- [x] Port `tests/library/selectors-register.spec.ts` → `DirectLibrarySelectorsRegisterParityTests.cs`. Official playwright.selectors.register, isolated contentScript world, and chain engines. Chromium 7/7 + leftover 6/6 + page 6/6 + 1 official skip; WebKit 7/7 + leftover 6/6 + page 6/6 + 1 official skip. Do not edit leftover `DirectSelectorsRegisterTests` or page `DirectSelectorsRegisterParityTests`. Skip Node-only `library/role-utils.spec.ts` (`__injectedScript`). Skip `library/video.spec.ts` / `library/screencast*.spec.ts` / `library/screenshot.spec.ts` (pixel-diff). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 896 — library/locator-dispatchevent-touch.spec.ts

### Wave 896
- [x] Port `tests/library/locator-dispatchevent-touch.spec.ts` → `DirectLibraryLocatorDispatchEventTouchParityTests.cs`. Official locator.dispatchEvent touch points. Chromium 1/1; WebKit 1/1. Skip `library/video.spec.ts` (pixel-diff screencast). Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 895 — library/web-socket.spec.ts

### Wave 895
- [x] Port `tests/library/web-socket.spec.ts` → `DirectLibraryWebSocketParityTests.cs`. Official page.WebSocket events, frames, wait abort, and extra handshake headers. Chromium 11/11 + leftover 5/5 + 3 official skips; WebKit 13/13 + leftover 5/5 + 1 official offline skip. Do not edit leftover `DirectPageWebSocketTests`. Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 894 — library/tracing.spec.ts

### Wave 894
- [x] Port `tests/library/tracing.spec.ts` → `DirectLibraryTracingParityTests.cs`. Official context.tracing zip/NDJSON action traces, chunks, snapshots, API request traces, and WebSocket jsonl. Chromium 32/32 + leftover 14/14; WebKit 32/32 + leftover 13/13 + 1 leftover Chromium-only skip. Do not edit leftover `DirectApiRequestTracingTests`. Skip Node-only `library/trace-viewer.spec.ts` / `library/trace-viewer-scrub.spec.ts` (Node trace viewer). Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 893 — library/tap.spec.ts

### Wave 893
- [x] Port `tests/library/tap.spec.ts` → `DirectLibraryTapParityTests.cs`. Official page.tap with hasTouch, trial interceptor, modifiers, and touch points. Chromium 9/9 + leftover 9/9; WebKit 9/9 + leftover 6/6. Do not edit leftover `DirectPageTapScrollTests`. Skip Node-only `library/signals.spec.ts` (`launchServer` / `process.kill`). Skip Node-only `library/slowmo.spec.ts` (`toImpl` / `_doSlowMo`). Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 892 — library/shared-worker.spec.ts

### Wave 892
- [x] Port `tests/library/shared-worker.spec.ts` → `DirectLibrarySharedWorkerParityTests.cs`. Official SharedWorker restart. Chromium 1/1; WebKit 1/1. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 891 — library/route-web-socket.spec.ts

### Wave 891
- [x] Port `tests/library/route-web-socket.spec.ts` → `DirectLibraryRouteWebSocketParityTests.cs`. Official page.routeWebSocket. Chromium 41/41 + leftover 18/18 + HAR 12/12; WebKit 35/35 + 6 official skips + leftover 18/18 + HAR 12/12. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 890 — library/resource-timing.spec.ts

### Wave 890
- [x] Port `tests/library/resource-timing.spec.ts` → `DirectLibraryResourceTimingParityTests.cs`. Official request.timing. Chromium 5/5; WebKit 4/4 + 1 official redirect skip. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 889 — library/proxy-pattern.spec.ts

### Wave 889
- [x] Port `tests/library/proxy-pattern.spec.ts` → `DirectLibraryProxyPatternParityTests.cs`. Official SOCKS parsePattern matcher. Chromium 1/1; WebKit 1/1. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 888 — library/proxy.spec.ts

### Wave 888
- [x] Port `tests/library/proxy.spec.ts` → `DirectLibraryProxyParityTests.cs`. Official launch-level HTTP/SOCKS proxy, bypass, CONNECT 407 reconnect, and websocket. Chromium 22/22 + 1 official fixme; WebKit 22/22 + 1 official fixme. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 887 — library/pdf.spec.ts

### Wave 887
- [x] Port `tests/library/pdf.spec.ts` → `DirectLibraryPdfParityTests.cs`. Official page.pdf save-file and tagged outline. Chromium 2/2; WebKit 0/0 + 2 official Chromium-only skips. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 886 — library/page-event-crash.spec.ts

### Wave 886
- [x] Port `tests/library/page-event-crash.spec.ts` → `DirectLibraryPageEventCrashParityTests.cs`. Official page crash event. Chromium 0/0 + 8 official Ubuntu 24.04 skips; WebKit 7/7 + 1 official fixme. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 885 — library/page-clock.frozen.spec.ts

### Wave 885
- [x] Port `tests/library/page-clock.frozen.spec.ts` → `DirectLibraryPageClockFrozenParityTests.cs`. Official PW_CLOCK frozen/realtime fixture. Chromium 0/0 + 2 official skips (default); `PW_CLOCK=frozen` 1/1 + 1 skip; `PW_CLOCK=realtime` 1/1 + 1 skip. WebKit same. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 884 — library/page-clock.spec.ts

### Wave 884
- [x] Port `tests/library/page-clock.spec.ts` → `DirectLibraryPageClockParityTests.cs`. Official page.clock install/pause/runFor/fastForward, AbortSignal.timeout, and Date.now integer. Chromium 48/48; WebKit 48/48. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 883 — library/modernizr.spec.ts

### Wave 883
- [x] Port `tests/library/modernizr.spec.ts` → `DirectLibraryModernizrParityTests.cs`. Official Modernizr feature matrix. Chromium 0/0 + 2 official WebKit-only skips; WebKit 2/2. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 882 — library/hit-target.spec.ts

### Wave 882
- [x] Port `tests/library/hit-target.spec.ts` → `DirectLibraryHitTargetParityTests.cs`. Official hit-target click blocking, hover-then-recheck, iframe padding, and parent-frame overlays. Chromium 20/20; WebKit 20/20. Skip Node-only `library/heap.spec.ts` (`node:inspector`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 881 — library/headful.spec.ts

### Wave 881
- [x] Port `tests/library/headful.spec.ts` → `DirectLibraryHeadfulParityTests.cs`. Official headed launch / persistent context. Chromium 0/0 + 16 official headless skips; WebKit 0/0 + 16 official headless skips. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 880 — library/har-websocket.spec.ts

### Wave 880
- [x] Port `tests/library/har-websocket.spec.ts` → `DirectLibraryHarWebsocketParityTests.cs`. Official HAR websocket entries, handshake, frames, attach/omit, routeWebSocket, and `PLAYWRIGHT_HAR_NO_WEBSOCKET_FRAMES`. Chromium 12/12; WebKit 12/12. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 879 — library/har.spec.ts

### Wave 879
- [x] Port `tests/library/har.spec.ts` → `DirectLibraryHarParityTests.cs`. Official `recordHar` log: creator/browser/pages, timings, headers, postData, redirects, content, zip, and persistent-context HAR. Chromium 57/57 + 1 official skip; WebKit 55/55 + 3 official skips. Do not edit leftover `DirectContextHarTests` or `DirectLaunchPersistentRecordHar*`. Skip Node-only `should populate entry startedDateTime from the browser`. Skip `tracing.startHar` `resourcesDir` titles until `ITracing.StartHarAsync` grows that option. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 878 — library/global-fetch-cookie.spec.ts

### Wave 878
- [x] Port `tests/library/global-fetch-cookie.spec.ts` → `DirectLibraryGlobalFetchCookieParityTests.cs`. Official standalone request cookies / storageState. Chromium 12/12; WebKit 12/12. Skip Node-only `__testHookLookup` titles. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 877 — library/global-fetch.spec.ts

### Wave 877
- [x] Port remaining `tests/library/global-fetch.spec.ts` titles → `DirectLibraryGlobalFetchParityTests.cs`. Official standalone `playwright.request` fetch, credentials, redirects, JSON bodies, retries, and failOnStatusCode. Chromium 67/67 + 3 HTTPS server skips; WebKit 67/67 + 3 HTTPS server skips. Leftover `DirectApiResponse*` already covers server address / security details. Skip Node-only `should set playwright as user-agent` and `should be able to construct with context options`. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 876 — library/fetch-proxy.spec.ts

### Wave 876
- [x] Port `tests/library/fetch-proxy.spec.ts` → `DirectLibraryFetchProxyParityTests.cs`. Official context.request / APIRequest proxy credentials, CONNECT, bypass, SOCKS5, and HTTPS-proxy ALPN. Chromium 6/6; WebKit 6/6. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 875 — library/favicon.spec.ts

### Wave 875
- [x] Port `tests/library/favicon.spec.ts` → `DirectLibraryFaviconParityTests.cs`. Official SVG favicon prefer-color-scheme. Chromium 0/0 + 1 official skip; WebKit 0/0 + 1 official skip (headless except Firefox). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 874 — library/emulation-focus.spec.ts

### Wave 874
- [x] Port `tests/library/emulation-focus.spec.ts` → `DirectLibraryEmulationFocusParityTests.cs`. Official document.hasFocus, popup focus, keyboard/mouse isolation, iframe focus, multi-context focus/blur, and concurrent hover. Chromium 10/10; WebKit 10/10. Skip screenshot pixel-diff `should not affect screenshots`. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 873 — library/logger.spec.ts

### Wave 873
- [x] Port `tests/library/logger.spec.ts` → `DirectLibraryLoggerParityTests.cs`. Official playwright logger on launch and newContext, wrapping browser.newContext, page.setContent, and page.click. Chromium 2/2; WebKit 2/2. Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 872 — library/launcher.spec.ts

### Wave 872
- [x] Port `tests/library/launcher.spec.ts` → `DirectLibraryLauncherParityTests.cs`. Official playwright.errors.TimeoutError, device defaultBrowserType, and headed Linux XServer launch error. Chromium 3/3; WebKit 3/3. Skip Node-only `should kill browser process on timeout after close` (`__testHookGracefullyClose`). Skip Node-only `browsertype-connect*`, `browsertype-launch-server`, `browsertype-launch-selenium`, `browsers-path.spec.ts`, `channels.spec.ts`, `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 871 — library/browsertype-launch.spec.ts

### Wave 871
- [x] Port `tests/library/browsertype-launch.spec.ts` → `DirectLibraryBrowserTypeLaunchParityTests.cs`. Official launch option guards, launch-error wrapping, context close on browser.close, and await-using dispose. Chromium 12/12; WebKit 12/12. Skip Node-only `should handle timeout` / `should handle exception and report launch log` (`__testHookBeforeCreateBrowser`). Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 870 — library/browsertype-basic.spec.ts

### Wave 870
- [x] Port `tests/library/browsertype-basic.spec.ts` → `DirectLibraryBrowserTypeBasicParityTests.cs`. Official executablePath, name, and connectOverCDP browser-name guard. Chromium 2/2 + 1 official skip; WebKit 2/2 + 1 official skip. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 869 — library/browser.spec.ts

### Wave 869
- [x] Port `tests/library/browser.spec.ts` → `DirectLibraryBrowserParityTests.cs`. Official browserType, newPage context lifetime, version, close rejects evaluate, and context event. Chromium 6/6; WebKit 6/6. Skip Node-only `newContext should not leave a context upon failure` (`toImpl` / `__testHookBeforeSetStorageState`). Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 868 — library/capabilities.spec.ts

### Wave 868
- [x] Port `tests/library/capabilities.spec.ts` → `DirectLibraryCapabilitiesParityTests.cs`. Official SharedArrayBuffer, WebAssembly, WebSocket, CSP, video/audio, WebGL, service workers, AVIF, clipboard, fullscreen, and storage.getDirectory capability smoke. Chromium 28/28 + 1 official skip; WebKit 28/28 + 1 official skip. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 867 — library/defaultbrowsercontext-2.spec.ts

### Wave 867
- [x] Port `tests/library/defaultbrowsercontext-2.spec.ts` → `DirectLibraryDefaultBrowserContext2ParityTests.cs`. Official persistent-context hasTouch, colorScheme, reducedMotion, timezone, locale, geolocation, ignoreHTTPSErrors, extraHTTPHeaders, userDataDir, close, coverage, selectors, HAR, dialog, CacheStorage, context.browser(), and storage.getDirectory. Chromium 26/26; WebKit 25/25 + 1 official skip. Leftover `DirectLaunchPersistentContextTests` now expects official `context.browser()`. Skip Node-only `toImpl` / `__testHook*` / `defaultUserAgentForTest` titles. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 866 — library/defaultbrowsercontext-1.spec.ts

### Wave 866
- [x] Port `tests/library/defaultbrowsercontext-1.spec.ts` → `DirectLibraryDefaultBrowserContext1ParityTests.cs`. Official persistent-context cookies, viewport, deviceScaleFactor, userAgent, bypassCSP, javascriptEnabled, httpCredentials, offline, and acceptDownloads. Chromium 11/11; WebKit 11/11. Do not edit leftover `DirectLaunchPersistent*` tests. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 865 — library/popup.spec.ts

### Wave 865
- [x] Port `tests/library/popup.spec.ts` → `DirectLibraryPopupParityTests.cs`. Official popup user-agent, extra HTTP headers, touch, viewport, window-feature size, init-script, and exposeFunction inheritance. Chromium 15/15; WebKit 15/15. Do not edit leftover `DirectPageEventPopupTests`. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 864 — library/permissions.spec.ts

### Wave 864
- [x] Port `tests/library/permissions.spec.ts` → `DirectLibraryPermissionsParityTests.cs`. Official grant/clear permissions accumulate per origin, reject unknown names, isolate clipboard, and cover storage-access, local-fonts, local-network-access, and screen-wake-lock. Chromium 19/19 + 4 official skips; WebKit 15/15 + 8 official skips. Do not edit leftover `DirectLaunchPersistentPermissionsTests`. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 863 — library/ignorehttpserrors.spec.ts

### Wave 863
- [x] Port `tests/library/ignorehttpserrors.spec.ts` → `DirectLibraryIgnoreHttpsErrorsParityTests.cs`. Official ignoreHTTPSErrors isolation, mixed content, WebSocket, and service worker document intercept. Chromium 6/6 + 1 official skip; WebKit 6/6 + 1 official skip. Do not edit leftover `DirectContextScriptHttpsTests`, `DirectLaunchPersistentIgnoreHttpsErrorsTests`, or leftover `DirectApiRequestTests`. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 862 — library/downloads-path.spec.ts

### Wave 862
- [x] Port `tests/library/downloads-path.spec.ts` → `DirectLibraryDownloadsPathParityTests.cs`. Official launch `downloadsPath`, relative path, persistent context, and file cleanup without deleting the user folder. Chromium 6/6; WebKit 6/6. Do not edit leftover `DirectPageDownloadTests`, `DirectContextDownload*`, or `DirectLaunch*Download*`. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 861 — library/download.spec.ts

### Wave 861
- [x] Port `tests/library/download.spec.ts` → `DirectLibraryDownloadParityTests.cs`. Official downloads: deny messages, `canceled`, opener new-window events, context/browser cleanup, WebKit filename-then-event and `Download is starting`. Chromium 36/36 + 1 official skip; WebKit 34/34 + 3 official skips. Do not edit leftover `DirectPageDownloadTests`, `DirectContextDownload*`, or `DirectLaunch*Download*`. Skip Node-only `should throw if browser dies` (`_channel.killForTests`), `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`), and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 860 — library/browsercontext-fetch-algorithms.spec.ts

### Wave 860
- [x] Port `tests/library/browsercontext-fetch-algorithms.spec.ts` → `DirectLibraryBrowserContextFetchAlgorithmsParityTests.cs`. Official fetch gzip/deflate/br, missing Content-Length, chunked, and empty Z_BUF_ERROR bodies. Chromium 15/15; WebKit 15/15. Do not edit leftover `DirectLibraryBrowserContextFetchParityTests` or leftover `DirectApiRequestTests`. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 859 — library/browsercontext-cookies-third-party.spec.ts

### Wave 859
- [x] Port `tests/library/browsercontext-cookies-third-party.spec.ts` → `DirectLibraryBrowserContextCookiesThirdPartyParityTests.cs`. Official third-party cookies, CHIPS `_crHasCrossSiteAncestor`, and nested OOPIF frame sessions. Chromium 11/11; WebKit 9/9 + 2 official skips. Do not edit leftover `DirectLibraryBrowserContextCookiesParityTests`, `DirectLibraryBrowserContextAddCookiesParityTests`, `DirectLibraryBrowserContextClearCookiesParityTests`, or `DirectContextCookieTests`. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`) and `library/browsercontext-fetch-happy-eyeballs.spec.ts` (`__testHookLookup`).

## Previous: Wave 858 — library/browsercontext-webauthn.spec.ts

### Wave 858
- [x] Port `tests/library/browsercontext-webauthn.spec.ts` → `DirectLibraryBrowserContextWebAuthnParityTests.cs`. Official `context.credentials` JS interceptor (seed, page-created capture, storageState restore). Chromium 4/4; WebKit 4/4. Do not edit leftover `DirectCredentials*Tests`, `DirectStorageStateCredentialsTests`, or Wave 852 storage-state WebAuthn titles.

## Previous: Wave 857 — library/browsercontext-viewport-mobile.spec.ts

### Wave 857
- [x] Port `tests/library/browsercontext-viewport-mobile.spec.ts` → `DirectLibraryBrowserContextViewportMobileParityTests.cs`. Official mobile viewport, touch, orientation, hover/pointer, and mobile scroll. Chromium 16/16; WebKit 16/16. Do not edit leftover `DirectContextEmulationTests` or leftover device tests.

## Previous: Wave 856 — library/browsercontext-viewport.spec.ts

### Wave 856
- [x] Port `tests/library/browsercontext-viewport.spec.ts` → `DirectLibraryBrowserContextViewportParityTests.cs`. Official default 1280x720, screen/orientation, null viewport, and tap-requires-hasTouch. Chromium 15/15 + 1 official skip; WebKit 14/14 + 2 official skips. Do not edit leftover `DirectContextEmulationTests` or `DirectLaunchPersistentViewportTests`.

## Previous: Wave 855 — library/browsercontext-user-agent.spec.ts

### Wave 855
- [x] Port `tests/library/browsercontext-user-agent.spec.ts` → `DirectLibraryBrowserContextUserAgentParityTests.cs`. Official `userAgent`, device UA, option copy, download UA, and Chromium Client Hints. Chromium 6/6; WebKit 5/5 + 1 official Chromium-only skip. Do not edit leftover `DirectLaunchPersistentUserAgentTests`.

## Previous: Wave 854 — library/browsercontext-timezone-id.spec.ts

### Wave 854
- [x] Port `tests/library/browsercontext-timezone-id.spec.ts` → `DirectLibraryBrowserContextTimezoneIdParityTests.cs`. Official `timezoneId`, invalid-ID text, popups, isolation, and workers. Chromium 6/6; WebKit 6/6. Do not edit leftover `DirectContextEnvironmentTests` or `DirectLaunchPersistentTimezoneTests`.

## Previous: Wave 853 — library/browsercontext-strict.spec.ts

### Wave 853
- [x] Port `tests/library/browsercontext-strict.spec.ts` → `DirectLibraryBrowserContextStrictParityTests.cs`. Official `strictSelectors` context option and `strict: false` opt-out. Chromium 4/4; WebKit 4/4. Do not edit leftover `DirectStrictSelectorsTests` or per-action leftover `Direct*StrictTests`.

## Previous: Wave 852 — library/browsercontext-storage-state remaining titles

### Wave 852
- [x] Finish `tests/library/browsercontext-storage-state.spec.ts` remaining titles: file round-trip with IndexedDB, official `valueEncoded` structured clone, empty IndexedDB, `deleteDatabase` not blocked, and WebAuthn credentials replace. Chromium 19/19; WebKit 19/19. Do not edit leftover `DirectContextStorageStateTests` or `DirectStorageStateCredentialsTests`.

## Previous: Wave 851 — library/browsercontext-storage-state.spec.ts

### Wave 851
- [x] Port `tests/library/browsercontext-storage-state.spec.ts` localStorage/cookies/file-error titles → `DirectLibraryBrowserContextStorageStateParityTests.cs`. Official origin tracking, internal storage-state page, and `Error setting/reading storage state` text. Chromium 13/13; WebKit 13/13. Remaining IDB/credentials titles are Wave 852. Do not edit leftover `DirectContextStorageStateTests` or `DirectStorageStateCredentialsTests`.

## Previous: Wave 850 — library/browsercontext-set-extra-http-headers.spec.ts

### Wave 850
- [x] Port `tests/library/browsercontext-set-extra-http-headers.spec.ts` → `DirectLibraryBrowserContextSetExtraHttpHeadersParityTests.cs`. Official extra HTTP headers. Chromium 2/2; WebKit 2/2. Do not edit leftover `DirectPageSetExtraHttpHeadersParityTests` or `DirectLaunchPersistentExtraHttpHeadersTests`.

## Previous: Wave 849 — library/browsercontext-service-worker-policy.spec.ts

### Wave 849
- [x] Port `tests/library/browsercontext-service-worker-policy.spec.ts` → `DirectLibraryBrowserContextServiceWorkerPolicyParityTests.cs`. Official serviceWorkers allow/block. Chromium 3/3; WebKit 3/3. Skip Node-only `library/browsercontext-reuse.spec.ts` (`_newContextForReuse`, `_disconnectFromReusedContext`, `launchServer`). Do not edit leftover `DirectContextServiceWorkerTests` or `DirectLaunchPersistentServiceWorkersTests` except official block-console alignment.

## Previous: Wave 848 — library/browsercontext-route.spec.ts

### Wave 848
- [x] Port `tests/library/browsercontext-route.spec.ts` → `DirectLibraryBrowserContextRouteParityTests.cs`. Official context.route. Chromium 20/20; WebKit 19/19 (+1 official WebKit Linux fixme). Do not edit leftover `DirectContextRouteTimesTests` or leftover `DirectRoute*` / `DirectPageRouteParityTests`.

## Previous: Wave 847 — library/browsercontext-proxy.spec.ts

### Wave 847
- [x] Port `tests/library/browsercontext-proxy.spec.ts` → `DirectLibraryBrowserContextProxyParityTests.cs`. Official context proxy. Chromium 25/25; WebKit 24/24 (+1 official WebKit Linux fixme). Skip Node-only `should throw for bad server value`. Do not edit leftover `DirectContextProxyTests`, `DirectContextProxyAuthTests`, or `LoopbackHttpProxy`.

## Previous: Wave 846 — library/browsercontext-pages.spec.ts

### Wave 846
- [x] Port `tests/library/browsercontext-pages.spec.ts` → `DirectLibraryBrowserContextPagesParityTests.cs`. Official context.pages, page.context, multi-page focus/click. Chromium 10/10 (+1 official Chromium fixme); WebKit 11/11. Do not edit leftover context page tests.

## Previous: Wave 845 — library/browsercontext-page-event.spec.ts

### Wave 845
- [x] Port `tests/library/browsercontext-page-event.spec.ts` → `DirectLibraryBrowserContextPageEventParityTests.cs`. Official context page events. Chromium 11/11; WebKit 11/11. Do not edit leftover `DirectContextPageEventTests`.

## Previous: Wave 844 — library/browsercontext-network-event.spec.ts

### Wave 844
- [x] Port `tests/library/browsercontext-network-event.spec.ts` → `DirectLibraryBrowserContextNetworkEventParityTests.cs`. Official context request/response events. Chromium 6/6 (+1 official headless favicon skip); WebKit 6/6 (+1 skip). Do not edit leftover `DirectContextNetworkEventTests`.

## Previous: Wave 843 — library/browsercontext-locale.spec.ts

### Wave 843
- [x] Port `tests/library/browsercontext-locale.spec.ts` → `DirectLibraryBrowserContextLocaleParityTests.cs`. Official context locale. Chromium 14/14; WebKit 14/14. Do not edit leftover `DirectContextEnvironmentTests` or `DirectLaunchPersistentLocaleTests`.

## Previous: Wave 842 — library/browsercontext-har.spec.ts

### Wave 842
- [x] Port `tests/library/browsercontext-har.spec.ts` → `DirectLibraryBrowserContextHarParityTests.cs`. Official context HAR record/routeFromHAR. Chromium 33/33; WebKit 33/33. Do not edit leftover `DirectRouteFromHarTests`, `DirectContextHarTests`, or `DirectRouteFromHarUpdate*`.

## Previous: Wave 841 — library/browsercontext-fetch.spec.ts

### Wave 841
- [x] Port `tests/library/browsercontext-fetch.spec.ts` → `DirectLibraryBrowserContextFetchParityTests.cs`. Official context.request / APIRequest. Chromium 111/111; WebKit 111/111 (+6 Node-only skips). Do not edit leftover `DirectApiRequestTests` except hang-up assertion.

## Previous: Wave 840 — library/browsercontext-expose-function.spec.ts

### Wave 840
- [x] Port `tests/library/browsercontext-expose-function.spec.ts` → `DirectLibraryBrowserContextExposeFunctionParityTests.cs`. Official context.exposeFunction / exposeBinding. Chromium 6/6; WebKit 6/6. Do not edit leftover `DirectContextExposeTests`.

## Previous: Wave 839 — library/browsercontext-events.spec.ts

### Wave 839
- [x] Port `tests/library/browsercontext-events.spec.ts` → `DirectLibraryBrowserContextEventsParityTests.cs`. Official context waitForEvent (console, dialog, weberror, pageload, frames, download). Chromium 19/19; WebKit 19/19. Do not edit leftover context event tests.

## Previous: Wave 838 — library/browsercontext-dsf.spec.ts

### Wave 838
- [x] Port `tests/library/browsercontext-dsf.spec.ts` → `DirectLibraryBrowserContextDsfParityTests.cs`. Official deviceScaleFactor image srcset. Chromium 2/2; WebKit 2/2. Do not edit leftover device-scale tests.

## Previous: Wave 837 — library/browsercontext-device.spec.ts

### Wave 837
- [x] Port `tests/library/browsercontext-device.spec.ts` → `DirectLibraryBrowserContextDeviceParityTests.cs`. Official device descriptors (iPhone viewport, UA, click, scroll). Chromium 8/8; WebKit 6/6 (+2 official WebKit skips). Do not edit leftover device tests.

## Previous: Wave 836 — library/browsercontext-csp.spec.ts

### Wave 836
- [x] Port `tests/library/browsercontext-csp.spec.ts` → `DirectLibraryBrowserContextCspParityTests.cs`. Official bypassCSP (meta, header, cross-process, iframe). Chromium 4/4; WebKit 4/4. Do not edit leftover CSP tests.

## Previous: Wave 835 — library/browsercontext-credentials.spec.ts

### Wave 835
- [x] Port `tests/library/browsercontext-credentials.spec.ts` → `DirectLibraryBrowserContextCredentialsParityTests.cs`. Official HTTP credentials / setHTTPCredentials. Chromium 16/16; WebKit 16/16. Do not edit leftover `DirectContextAuthTests`.

## Previous: Wave 834 — library/browsercontext-basic.spec.ts

### Wave 834
- [x] Port `tests/library/browsercontext-basic.spec.ts` → `DirectLibraryBrowserContextBasicParityTests.cs`. Official context create/close, isolation, viewport, offline, javascript. Chromium 28/28 (+1 official Chromium fixme); WebKit 29/29. Skip Node-only default user agent. Do not edit leftover context tests.

## Previous: Wave 833 — library/browsercontext-base-url.spec.ts

### Wave 833
- [x] Port `tests/library/browsercontext-base-url.spec.ts` → `DirectLibraryBrowserContextBaseUrlParityTests.cs`. Official context baseURL (WHATWG resolve, urlMatcher, persistent). Chromium 8/8; WebKit 8/8. Do not edit leftover baseURL tests.

## Previous: Wave 832 — library/browsercontext-add-init-script.spec.ts

### Wave 832
- [x] Port `tests/library/browsercontext-add-init-script.spec.ts` → `DirectLibraryBrowserContextAddInitScriptParityTests.cs`. Official context.addInitScript (disposable, exposeFunctions, popups, cross-process). Chromium 12/12; WebKit 12/12. Do not edit leftover init-script tests.

## Previous: Wave 831 — library/browsercontext-clearcookies.spec.ts

### Wave 831
- [x] Port `tests/library/browsercontext-clearcookies.spec.ts` → `DirectLibraryBrowserContextClearCookiesParityTests.cs`. Official context.clearCookies (name/domain/path filters, cookieStore.change, CHIPS). Chromium 9/9; WebKit 8/8 (+1 skip non-Chromium CHIPS). Do not edit leftover cookie tests.

## Previous: Wave 830 — library/browsercontext-cookies.spec.ts

### Wave 830
- [x] Port `tests/library/browsercontext-cookies.spec.ts` → `DirectLibraryBrowserContextCookiesParityTests.cs`. Official context.cookies (httpOnly, SameSite, subdomain, expires, requestStorageAccess, iframe inherit). Chromium 15/15 (+1 skip Chromium requestStorageAccess); WebKit 16/16. Do not edit leftover cookie tests.

## Previous: Wave 829 — library/browsercontext-add-cookies.spec.ts

### Wave 829
- [x] Port `tests/library/browsercontext-add-cookies.spec.ts` → `DirectLibraryBrowserContextAddCookiesParityTests.cs`. Official context.addCookies (rewriteCookies, isolation, SameSite, secure WebSocket). Chromium 23/23; WebKit 23/23. Do not edit leftover cookie tests.

## Previous: Wave 828 — library/geolocation.spec.ts

### Wave 828
- [x] Port `tests/library/geolocation.spec.ts` → `DirectLibraryGeolocationParityTests.cs`. Official geolocation permissions, watchPosition, and popup inheritance. Chromium 9/9; WebKit 9/9. Do not edit leftover geolocation tests.

## Previous: Wave 827 — library/beforeunload.spec.ts

### Wave 827
- [x] Port `tests/library/beforeunload.spec.ts` → `DirectLibraryBeforeUnloadParityTests.cs`. Official beforeunload close/navigate. Chromium 12/12; WebKit 12/12. Do not edit leftover dialog tests.

## Previous: Wave 826 — library/page-close.spec.ts

### Wave 826
- [x] Port `tests/library/page-close.spec.ts` → `DirectLibraryPageCloseParityTests.cs`. Official page.close (dialog, expect, locator handler, popup nav). Chromium 18/18; WebKit 18/18. Do not edit leftover close tests.

## Previous: Wave 825 — page-localstorage.spec.ts remaining title

### Wave 825
- [x] Port remaining `tests/page/page-localstorage.spec.ts` title → `DirectPageLocalStorageOriginParityTests.cs`. Official origin-scoped storage. Chromium 1/1; WebKit 1/1. Do not edit leftover local/session storage tests. (`page-click-scroll` remaining `scroll: "none"` titles were already in `DirectPageClickScrollParityTests`.)

## Previous: Wave 824 — page-wait-for-request.spec.ts

### Wave 824
- [x] Port remaining `tests/page/page-wait-for-request.spec.ts` titles → `DirectPageWaitForRequestParityTests.cs`. Official waitForRequest (timeout logs, default timeout, regex). Chromium 8/8; WebKit 8/8. Do not edit leftover `DirectWaitForRequestTests.cs`.

## Previous: Wave 823 — page-wait-for-response.spec.ts

### Wave 823
- [x] Port remaining `tests/page/page-wait-for-response.spec.ts` titles → `DirectPageWaitForResponseParityTests.cs`. Official waitForResponse (timeout logs, default timeout, async predicate, one-shot sync predicate). Chromium 8/8; WebKit 8/8. Do not edit leftover wait-for-response tests.

## Previous: Wave 822 — page-wait-for-url.spec.ts

### Wave 822
- [x] Port remaining `tests/page/page-wait-for-url.spec.ts` titles → `DirectPageWaitForUrlParityTests.cs`. Official waitForURL (timeout, commit, history, frame). Chromium 11/11; WebKit 11/11. Do not edit leftover `DirectWaitForUrlTests.cs`.

## Previous: Wave 821 — page-evaluate.spec.ts

### Wave 821
- [x] Port `tests/page/page-evaluate.spec.ts` → `DirectPageEvaluateParityTests.cs`. Official page.evaluate structured-clone. Chromium 96/96 (+1 skip Node extra-arg); WebKit 94/94 (+3 skip Node extra-arg, using, Chromium-only deep-chain 2). Do not edit leftover `DirectPageEvaluateTests.cs`.

## Previous: Wave 820 — selectors-frame any-frame titles

### Wave 820
- [x] Port remaining `tests/page/selectors-frame.spec.ts` any-frame titles → `DirectSelectorsFrameAnyFrameParityTests.cs`. Official `internal:control=any-frame`. Chromium 8/8; WebKit 8/8. Do not edit leftover `DirectSelectorsFrameParityTests.cs`.

## Previous: Wave 819 — locator-any-frame.spec.ts

### Wave 819
- [x] Port `tests/page/locator-any-frame.spec.ts` → `DirectLocatorAnyFrameParityTests.cs`. Official `page.frameLocator()` any-frame search. Chromium 50/50; WebKit 50/50.

### Skipped: page-click-during-navigation.spec.ts
- Node-only `__testHookAfterStable` internal click hook; no public equivalent (playbook skip with toImpl).

## Previous: Wave 818 — page-add-init-script-callback.spec.ts

### Wave 818
- [x] Port `tests/page/page-add-init-script-callback.spec.ts` → `DirectPageAddInitScriptCallbackParityTests.cs`. Official `addInitScript(..., { exposeFunctions })`. Chromium 12/12; WebKit 12/12.

## Previous: Wave 817 — page-evaluate-callback.spec.ts

### Wave 817
- [x] Port `tests/page/page-evaluate-callback.spec.ts` → `DirectPageEvaluateCallbackParityTests.cs`. Official evaluate callbacks (`exposeFunctions`). Chromium 23/23; WebKit 23/23. Skip `page-evaluate-no-stall.spec.ts` (toImpl) and `page-leaks.spec.ts` (toImpl).

## Previous: Wave 816 — page-aria-snapshot-json.spec.ts

### Wave 816
- [x] Port `tests/page/page-aria-snapshot-json.spec.ts` → `DirectPageAriaSnapshotJsonParityTests.cs`. Official `ariaSnapshotJSON()` from the same DOM walk as YAML. Chromium 11/11; WebKit 11/11. Do not edit leftover `DirectPageAriaSnapshotJsonTests.cs` / `DirectLocatorAriaSnapshotJsonTests.cs`.

## Previous: Wave 815 — page-aria-snapshot-ai.spec.ts

### Wave 815
- [x] Port `tests/page/page-aria-snapshot-ai.spec.ts` → `DirectPageAriaSnapshotAiParityTests.cs`. Official AI-mode aria snapshot YAML (refs, iframe stitch, distillation). Chromium 45/45; WebKit 45/45. Do not edit leftover `DirectPageAriaSnapshotTests.cs`.

## Previous: Wave 814 — page-aria-snapshot.spec.ts

### Wave 814
- [x] Port `tests/page/page-aria-snapshot.spec.ts` → `DirectPageAriaSnapshotParityTests.cs`. Official `ariaSnapshot()` YAML from a DOM walk. Chromium 41/41; WebKit 41/41. Do not edit leftover `DirectPageAriaSnapshotTests.cs`.

## Previous: Wave 813 — to-match-aria-snapshot.spec.ts

### Wave 813
- [x] Port `tests/page/to-match-aria-snapshot.spec.ts` → `DirectToMatchAriaSnapshotParityTests.cs`. Official page/locator toMatchAriaSnapshot. Chromium 36/36; WebKit 36/36.

## Previous: Wave 812 — matchers.misc.spec.ts

### Wave 812
- [x] Port `tests/page/matchers.misc.spec.ts` → `DirectMatchersMiscParityTests.cs`. Official expect outlives navigation and missing-locator error text. Chromium 2/2; WebKit 2/2.

## Previous: Wave 811 — expect-with-snapshot.spec.ts

### Wave 811
- [x] Port `tests/page/expect-with-snapshot.spec.ts` → `DirectExpectWithSnapshotParityTests.cs`. Official matcherResult.ariaSnapshot on expect failures. Chromium 14/14; WebKit 14/14.

## Previous: Wave 810 — expect-matcher-result.spec.ts

### Wave 810
- [x] Port `tests/page/expect-matcher-result.spec.ts` → `DirectExpectMatcherResultParityTests.cs`. Official expect matcher result / error details. Chromium 4/4; WebKit 4/4. Skipped playbook screenshot pixel-diff: toHaveScreenshot should populate matcherResult.

## Previous: Wave 809 — expect-timeout.spec.ts

### Wave 809
- [x] Port `tests/page/expect-timeout.spec.ts` → `DirectExpectTimeoutParityTests.cs`. Official not-found / mismatch timeout messages, TimeoutException name, navigation during one-shot and locator-handler checks. Chromium 9/9; WebKit 9/9. Skipped JS AbortController / expect signal titles.

## Previous: Wave 808 — expect-misc.spec.ts

### Wave 808
- [x] Port `tests/page/expect-misc.spec.ts` → `DirectExpectMiscParityTests.cs`. Official toHaveCount / JS property / class / title / URL / attribute / CSS / id / viewport. Chromium 59/59; WebKit 59/59. Skipped JS-only: fail with invalid argument, support URLPattern, should have good stack.

## Previous: Wave 807 — expect-to-have-value.spec.ts

### Wave 807
- [x] Port `tests/page/expect-to-have-value.spec.ts` → `DirectExpectToHaveValueParityTests.cs`. Official toHaveValue / toHaveValues, labels, regex, fail diffs. Chromium 11/11; WebKit 11/11.

## Previous: Wave 806 — expect-to-have-text.spec.ts

### Wave 806
- [x] Port `tests/page/expect-to-have-text.spec.ts` → `DirectExpectToHaveTextParityTests.cs`. Official toHaveText / toContainText, whitespace, shadow text, fail messages. Chromium 28/28; WebKit 28/28.

## Previous: Wave 805 — expect-boolean.spec.ts

### Wave 805
- [x] Port `tests/page/expect-boolean.spec.ts` → `DirectExpectBooleanParityTests.cs`. Official boolean expect, fail messages, toBeOK, shadow focus. Chromium 84/84; WebKit 84/84. JS type-confused toBeOK titles skipped.

## Previous: Wave 804 — expect-to-have-accessible.spec.ts

### Wave 804
- [x] Port `tests/page/expect-to-have-accessible.spec.ts` → `DirectExpectToHaveAccessibleParityTests.cs`. Official accessible name, description, error message, and role. Chromium 15/15; WebKit 15/15. JS regex `toHaveRole` throw skipped.

## Previous: Wave 803 — library locator-highlight.spec.ts

### Wave 803
- [x] Port `tests/library/locator-highlight.spec.ts` → `DirectLibraryLocatorHighlightParityTests.cs`. Styled highlight, hide, survive navigation, page.hideHighlight. Chromium 4/4 (+ page overlay 1/1); WebKit 4/4. JS-only object-style title skipped.

## Previous: Wave 802 — locator-highlight.spec.ts

### Wave 802
- [x] Port `tests/page/locator-highlight.spec.ts` → `DirectLocatorHighlightParityTests.cs`. Official `x-pw-highlight` / `x-pw-tooltip` overlay. Chromium 1/1; WebKit 1/1.

## Previous: Wave 801 — selectors-frame.spec.ts

### Wave 801
- [x] Port `tests/page/selectors-frame.spec.ts` → `DirectSelectorsFrameParityTests.cs`. Official `internal:control=enter-frame` / `pierce-frames` iframe selectors. Chromium 35/35; WebKit 35/35.

## Previous: Wave 800 — selectors-role.spec.ts

### Wave 800
- [x] Port `tests/page/selectors-role.spec.ts` → `DirectSelectorsRoleParityTests.cs`. Official role / ARIA state matching. Chromium 16/16; WebKit 16/16. Do not edit leftover `DirectGetByTests.cs`.

## Previous: Wave 799 — selectors-text.spec.ts

### Wave 799
- [x] Port `tests/page/selectors-text.spec.ts` → `DirectSelectorsTextParityTests.cs`. Official text engine / getByText matching. Chromium 22/22; WebKit 22/22. Do not edit leftover `DirectGetByTests.cs`.

## Previous: Wave 798 — selectors-get-by.spec.ts

### Wave 798
- [x] Port `tests/page/selectors-get-by.spec.ts` → `DirectSelectorsGetByParityTests.cs`. Official getByTestId / getByText / getByLabel / getByPlaceholder / getByAltText / getByTitle / getByRole (including description). Chromium 26/26; WebKit 26/26. Do not edit leftover `DirectGetByTests.cs`.

## Previous: Wave 797 — selectors-css.spec.ts

### Wave 797
- [x] Port `tests/page/selectors-css.spec.ts` → `DirectSelectorsCssParityTests.cs`. Official CSS combinators, `:nth-child` / `:not` / `:is` / `:has` / `:scope`, comma lists, and handle-relative CSS. Chromium 27/27; WebKit 27/27.

## Previous: Wave 796 — selectors-misc.spec.ts

### Wave 796
- [x] Port `tests/page/selectors-misc.spec.ts` → `DirectSelectorsMiscParityTests.cs`. Official shadow, `:visible`, `nth`, layout, xpath, and `internal:has` / `and` / `or` / `chain` selectors. Chromium 21/21; WebKit 21/21.

## Previous: Wave 795 — selectors-register.spec.ts

### Wave 795
- [x] Port `tests/page/selectors-register.spec.ts`. Official custom-engine atomic reads for textContent, innerText, innerHTML, getAttribute, and isVisible, plus java-style object literal engines. Chromium 6/6; WebKit 6/6. Do not edit leftover `DirectSelectorsRegisterTests.cs`.

## Previous: Wave 794 — page-wait-for-selector-1.spec.ts

### Wave 794
- [x] Port remaining `tests/page/page-wait-for-selector-1.spec.ts`. Official attach/shadow/innerHTML waits, timeout locator-resolved logs, and click first-match preview. Chromium 15/15; WebKit 15/15. Typed `waitFor` option tests skipped. Do not edit leftover `DirectWaitForSelectorTests.cs`.

## Previous: Wave 793 — page-wait-for-selector-2.spec.ts

### Wave 793
- [x] Port `tests/page/page-wait-for-selector-2.spec.ts`. Official waitForSelector visibility, hidden/detached, xpath, and element-handle waits. Chromium 27/27; WebKit 27/27. Typed invalid-state options and `__testHookBeforeAdoptNode` skipped.

## Previous: Wave 792 — page-navigation.spec.ts

### Wave 792
- [x] Port `tests/page/page-navigation.spec.ts`. Official `_blank` clicks, form popups, and cross-origin POST redirect after click. Chromium 4/4; WebKit 4/4.

## Previous: Wave 791 — page-add-locator-handler.spec.ts

### Wave 791
- [x] Port `tests/page/page-add-locator-handler.spec.ts`. Official overlay handlers on click/hover/expect, force skip, times, noWaitAfter, and removeLocatorHandler. Chromium 14/14; WebKit 14/14. Do not edit leftover `DirectLocatorHandlerTests.cs`.

## Previous: Wave 790 — page-click-react.spec.ts

### Wave 790
- [x] Port `tests/page/page-click-react.spec.ts`. Official click timeout when an alert opens, and React hover/recycle retargeting. Chromium 3/3; WebKit 3/3.

## Previous: Wave 789 — page-autowaiting-no-hang.spec.ts

### Wave 789
- [x] Port `tests/page/page-autowaiting-no-hang.spec.ts`. Official click wait-after must not hang on HTTPS cert errors, window.stop, about:blank, popups, aborted/committed stall navigations, Navigation API intercepts, or goBack. Chromium 11/11; WebKit 11/11.

## Previous: Wave 788 — page-click-scroll.spec.ts

### Wave 788
- [x] Port `tests/page/page-click-scroll.spec.ts`. Official click/hover scroll including display:contents, iframe, and scroll:none. Chromium 10 pass + 1 skip (`it.fixme` display:contents position); WebKit 11/11. Do not edit leftover `DirectPageClickScrollTests.cs`.

## Previous: Wave 787 — page-click-timeout-4.spec.ts

### Wave 787
- [x] Port `tests/page/page-click-timeout-4.spec.ts`. Official unstable-position timeout and fixed-overlay intercept after scroll. Chromium 2/2; WebKit 2/2. `__testHookBeforePointerAction` skipped (Node-only).

## Previous: Wave 786 — page-click-timeout-3.spec.ts

### Wave 786
- [x] Port `tests/page/page-click-timeout-3.spec.ts`. Official hit-target intercept logs, force-click through overlay, and subtree intercept text. Chromium 3/3; WebKit 3/3. `__testHookBeforeHitTarget` skipped (Node-only).

## Previous: Wave 785 — page-click-timeout-2.spec.ts

### Wave 785
- [x] Port `tests/page/page-click-timeout-2.spec.ts`. Official display:none / visibility:hidden click timeouts. Chromium 2/2; WebKit 2/2.

## Previous: Wave 784 — page-autowaiting-basic.spec.ts

### Wave 784
- [x] Port `tests/page/page-autowaiting-basic.spec.ts`. Official click wait-after for navigation commit, noWaitAfter, dblclick, and collapsed action/expect call logs. Chromium 11/11; WebKit 11/11. `__testHookAfterPointerAction` skipped (Node-only).

## Previous: Wave 783 — page-click-timeout-1.spec.ts

### Wave 783
- [x] Port `tests/page/page-click-timeout-1.spec.ts`. Official disabled-button click timeout and call log. Chromium 1/1; WebKit 1/1. `__testHookBeforePointerAction` skipped (Node-only).

## Previous: Wave 782 — interception.spec.ts

### Wave 782
- [x] Port `tests/page/interception.spec.ts`. Official glob matching, worker interception, service-worker ordering, and memory-cache disable. Chromium 13 pass + 1 skip (blob WebKit-only); WebKit 14/14.

## Previous: Wave 781 — retarget.spec.ts

### Wave 781
- [x] Port `tests/page/retarget.spec.ts`. Official label/button retargeting for enabled, visible, editable, fill, select, check, and setInputFiles. Chromium 15/15; WebKit 15/15.

## Previous: Wave 780 — page-set-input-files.spec.ts

### Wave 780
- [x] Port `tests/page/page-set-input-files.spec.ts`. Official path/folder/memory uploads including ENOENT text, webkitdirectory validation, lastModified, and 200MB native `DOM.setFileInputFiles`. Chromium 20/20; WebKit 20/20.

## Previous: Wave 779 — locator-wait-for-function.spec.ts

### Wave 779
- [x] Port `tests/page/locator-wait-for-function.spec.ts`. Official locator.waitForFunction including ElementHandle args, async predicates, rerender, and strict-mode. Chromium 8/8; WebKit 8/8. AbortController signal tests skipped (Node-only).

## Previous: Wave 778 — network-post-data.spec.ts

### Wave 778
- [x] Port `tests/page/network-post-data.spec.ts`. Official postDataJSON / postDataBuffer including UTF-8 bodies and invalid-JSON error text. Chromium 4 pass + 2 skip (`it.fail` file/blob + sendBeacon); WebKit 4 pass + 2 skip.

## Previous: Wave 777 — locator-list.spec.ts

### Wave 777
- [x] Port `tests/page/locator-list.spec.ts`. Official `locator.all()` returns locators (`Nth` snapshots). Chromium 1/1; WebKit 1/1.

## Previous: Wave 776 — page-request-intercept.spec.ts

### Wave 776
- [x] Port `tests/page/page-request-intercept.spec.ts`. Official route.fetch / fulfill-from-response including contentType and body overrides, route.fetch timeout text, JSON postData, and favicon abort. Chromium 14 pass + 1 skip (`it.fixme` multipart); WebKit 13 pass + 2 skip (multipart + FormData+Blob `it.fixme`).

## Previous: Wave 775 — page-request-fallback.spec.ts

### Wave 775
- [x] Port `tests/page/page-request-fallback.spec.ts`. Official route.fallback chaining including header/method/URL/postData overrides. Chromium 14/14; WebKit 14/14.

## Previous: Wave 774 — page-request-fulfill.spec.ts

### Wave 774
- [x] Port `tests/page/page-request-fulfill.spec.ts`. Official route.fulfill including status phrases, multi Set-Cookie, cancelled fulfill, gzip fetch readback, and mocked bodies. Chromium 18 pass + 1 skip + 1 isolated suite-timeout rerun; WebKit 19 pass + 1 skip (`it.skip` Set-Cookie headerValue).

## Previous: Wave 773 — page-request-continue.spec.ts

### Wave 773
- [x] Port `tests/page/page-request-continue.spec.ts`. Official route.continue including forbidden headers, URL/postData overrides, redirect header replay, and cookie jar. Chromium 38 pass + 1 isolated suite-timeout rerun; WebKit 38 pass + 1 skip (`it.fail` COOP).

## Previous: Wave 772 — page-route.spec.ts

### Wave 772
- [x] Port `tests/page/page-route.spec.ts`. Official page.route including glob, unroute, CORS fulfill, times, and cookie/referer. Chromium 54 pass + 2 skip; WebKit 53 pass + 3 skip.

## Previous: Wave 771 — page-click.spec.ts

### Wave 771
- [x] Port `tests/page/page-click.spec.ts`. Official page.click including actionability, force, and iframe scrolling. Chromium 80 pass + 1 skip (`it.fixme` fixed-position iframe); WebKit 81/81.

## Previous: Wave 770 — page-goto.spec.ts

### Wave 770
- [x] Port `tests/page/page-goto.spec.ts`. Official page.goto including COOP process-swap events, JS-redirect interrupts, and iframe navigation requests. Chromium and WebKit 59 pass + 2 skip.

## Previous: Wave 769 — page-wait-for-navigation.spec.ts

### Wave 769
- [x] Port `tests/page/page-wait-for-navigation.spec.ts`. Official waitForNavigation including commit, URL match, SSL, and frame detach. 15/15 Chromium; 14/15 WebKit (`window.stop` fixme).

## Previous: Wave 768 — elementhandle-wait-for-element-state.spec.ts

### Wave 768
- [x] Port `tests/page/elementhandle-wait-for-element-state.spec.ts`. Detached/hidden/aria-disabled waits. 12/12 Chromium and WebKit.

## Previous: Wave 767 — elementhandle-scroll-into-view.spec.ts

### Wave 767
- [x] Port `tests/page/elementhandle-scroll-into-view.spec.ts`. ScrollIntoView wait + DOM.scrollIntoViewIfNeeded. 9/9 Chromium and WebKit.

## Previous: Wave 766 — elementhandle-eval-on-selector.spec.ts

### Wave 766
- [x] Port `tests/page/elementhandle-eval-on-selector.spec.ts`. Handle $eval / $$eval. 6/6 Chromium and WebKit.

## Previous: Wave 765 — elementhandle-select-text.spec.ts

### Wave 765
- [x] Port `tests/page/elementhandle-select-text.spec.ts`. Official selectText including plain divs. 5/5 Chromium and WebKit.

## Previous: Wave 764 — elementhandle-query-selector.spec.ts

### Wave 764
- [x] Port `tests/page/elementhandle-query-selector.spec.ts`. Handle $ / $$ including xpath. 7/7 Chromium and WebKit.

## Previous: Wave 763 — elementhandle-owner-frame.spec.ts

### Wave 763
- [x] Port `tests/page/elementhandle-owner-frame.spec.ts`. OwnerFrame including adopted/OOPIF. 7/7 Chromium and WebKit.

## Previous: Wave 762 — elementhandle-content-frame.spec.ts

### Wave 762
- [x] Port `tests/page/elementhandle-content-frame.spec.ts`. ContentFrame only for iframe hosts; OOPIF lookup. 5/5 Chromium and WebKit.

## Previous: Wave 761 — page-network-sizes.spec.ts

### Wave 761
- [x] Port `tests/page/page-network-sizes.spec.ts`. GetSizes wait/throw + encoded sizes. Chromium 13 pass + 2 skip; WebKit 14 pass + 1 skip.

## Previous: Wave 760 — page-network-request.spec.ts

### Wave 760
- [x] Port `tests/page/page-network-request.spec.ts`. ExtraInfo hops, postData, popup main request. Chromium 29/29; WebKit 26 pass + 3 official skip.

## Previous: Wave 759 — page-network-response.spec.ts

### Wave 759
- [x] Port `tests/page/page-network-response.spec.ts`. ExtraInfo headers, navigated-away bodies, WebKit evaluate unwrap. Chromium 25 pass + 2 skip; WebKit 24 pass + 3 skip.

## Previous: Wave 758 — locator-get.spec.ts

### Wave 758
- [x] Port `tests/page/locator-get.spec.ts`. page/frame/locator.get(By.*) + IBy. 13/13 Chromium and WebKit.

## Previous: Wave 757 — page-cache-storage.spec.ts

### Wave 757
- [x] Port `tests/page/page-cache-storage.spec.ts`. Chromium 1/1; WebKit official CacheStorage-across-reload Ignore.

## Previous: Wave 756 — page-event-network.spec.ts

### Wave 756
- [x] Port `tests/page/page-event-network.spec.ts`. Response.FinishedAsync waits for request finished. Chromium 6 pass + 1 official skip; WebKit 7/7.

## Previous: Wave 755 — queryselector.spec.ts

### Wave 755
- [x] Port `tests/page/queryselector.spec.ts`. Auto-detect xpath/text; harden page.$ / $$. 17/17 Chromium and WebKit.

## Previous: Wave 754 — locator-element-handle.spec.ts

### Wave 754
- [x] Port `tests/page/locator-element-handle.spec.ts`. Locator elementHandle/elementHandles + xpath. 5 titles.

## Previous: Wave 753 — frame-frame-element.spec.ts

### Wave 753
- [x] Port `tests/page/frame-frame-element.spec.ts`. FrameElement including shadow roots. 6 titles.

## Previous: Wave 752 — page-request-gc.spec.ts

### Wave 752
- [x] Port `tests/page/page-request-gc.spec.ts`. RequestGC + locator click handle release. 2 titles.

## Previous: Wave 751 — frame-goto.spec.ts

### Wave 751
- [x] Port `tests/page/frame-goto.spec.ts`. Navigate subframes, reject on detach, client redirect timeout, matching responses. 4 titles.

## Previous: Wave 750 — frame-hierarchy.spec.ts

### Wave 750
- [x] Port `tests/page/frame-hierarchy.spec.ts`. Frame tree, attach/detach/navigated, x-frame-options. 13 titles.

## Previous: Wave 749 — page-strict.spec.ts

### Wave 749
- [x] Port `tests/page/page-strict.spec.ts`. Strict violation message formatting. 10 titles.

## Previous: Wave 748 — page-evaluate-handle.spec.ts

### Wave 748
- [x] Port `tests/page/page-evaluate-handle.spec.ts`. Nested handle args; WebKit JSON handle trees. 11 titles.

## Previous: Wave 747 — workers.spec.ts

### Wave 747
- [x] Port `tests/page/workers.spec.ts`. Worker evaluate/console/network; unskip local workers expectation. 24 titles.

## Previous: Wave 746 — elementhandle-bounding-box.spec.ts

### Wave 746
- [x] Port `tests/page/elementhandle-bounding-box.spec.ts`. Page-relative box + iframe offset. WebKit 9/9; Chromium isolated 9/9.

## Previous: Wave 745 — page-set-extra-http-headers.spec.ts

### Wave 745
- [x] Port `tests/page/page-set-extra-http-headers.spec.ts`. Header type validation; single referer. 5/5 Chromium and WebKit.

## Previous: Wave 744 — page-listeners.spec.ts

### Wave 744
- [x] Port `tests/page/page-listeners.spec.ts`. Official `RemoveAllListenersAsync` ignoreErrors/wait. 3/3 Chromium and WebKit.

## Previous: Wave 743 — page-event-load.spec.ts

### Wave 743
- [x] Port `tests/page/page-event-load.spec.ts`. WebKit page Load is main-frame only. 2/2 Chromium and WebKit.

## Previous: Wave 742 — page-wait-for-load-state.spec.ts

### Wave 742
- [x] Port `tests/page/page-wait-for-load-state.spec.ts`. Chromium noopener resume-on-wait; WebKit `about:blank` readyState seed. 17 pass + 1 typed-API skip.

## Previous: Wave 741 — elementhandle-click.spec.ts

### Wave 741
- [x] Port `tests/page/elementhandle-click.spec.ts`. Detached/force/no-box/text-node scroll; visibility wait without `window.Node`. 9/9 Chromium and WebKit.

## Previous: Wave 740 — page-drag.spec.ts

### Wave 740
- [x] Port `tests/page/page-drag.spec.ts`. Measure both handles before mousedown. Chromium 18 pass + 3 skip; WebKit 17 pass + 4 skip.

## Previous: Wave 739 — page-wait-for-function.spec.ts

### Wave 739
- [x] Port `tests/page/page-wait-for-function.spec.ts`. Polling, primitive unbox, ambient timeout. 26/26 Chromium and WebKit.

## Previous: Wave 738 — page-event-request.spec.ts

### Wave 738
- [x] Port `tests/page/page-event-request.spec.ts`. Hide favicon/preflight; lazy response body; last 100 requests. Chromium 16/16; WebKit 15 pass + 1 official skip.

## Previous: Wave 737 — locator-frame.spec.ts

### Wave 737
- [x] Port `tests/page/locator-frame.spec.ts`. Frame-scoped queries, strict iframe errors, injected-script retries. 22/22 Chromium and WebKit.

## Previous: Wave 736 — page-event-popup.spec.ts

### Wave 736
- [x] Port `tests/page/page-event-popup.spec.ts`. Noopener successor popups; about:blank URL. 13/13 Chromium and WebKit.

## Previous: Wave 735 — page-expose-function.spec.ts

### Wave 735
- [x] Port `tests/page/page-expose-function.spec.ts`. Disposable expose/binding, handle results, WebKit process-swap. 21/21 Chromium and WebKit.

## Previous: Wave 734 — elementhandle-press.spec.ts

### Wave 734
- [x] Port `tests/page/elementhandle-press.spec.ts`. Press uses FocusForType so unfocused inputs insert at start. Chromium 5/5; WebKit 4 pass + 1 official skip.

## Previous: Wave 733 — page-filechooser.spec.ts

### Wave 733
- [x] Port `tests/page/page-filechooser.spec.ts`. Wait/cancel, protocol path setFiles, empty clear, user-gesture click. 25/25 Chromium and WebKit.

## Previous: Wave 732 — page-event-pageerror.spec.ts

### Wave 732
- [x] Port `tests/page/page-event-pageerror.spec.ts`. Typed `PageError` name/message/stack. Chromium 13/13; WebKit 12 pass + 1 official skip.

## Previous: Wave 731 — locator-misc-2.spec.ts

### Wave 731
- [x] Port `tests/page/locator-misc-2.spec.ts`. Iframe locator(locator), `visible=` engine, zero-size scroll. 16/16 Chromium and WebKit.

## Previous: Wave 730 — page-drop.spec.ts

### Wave 730
- [x] Port `tests/page/page-drop.spec.ts`. Official `DropAsync` file/clipboard payloads. 7/7 Chromium and WebKit.

## Previous: Wave 729 — elementhandle-type.spec.ts

### Wave 729
- [x] Port `tests/page/elementhandle-type.spec.ts`. Focus resets selection when not focused. Chromium 5/5; WebKit 4 pass + 1 official skip.

## Previous: Wave 728 — wheel.spec.ts

### Wave 728
- [x] Port `tests/page/wheel.spec.ts`. Chromium focus emulation and WebKit compositor wheel dispatch. 7/7 Chromium and WebKit.

## Previous: Wave 727 — page-select-option.spec.ts

### Wave 727
- [x] Port `tests/page/page-select-option.spec.ts`. Value-or-label match, wait/retry, empty deselect. 32 pass + 1 typed-API skip on Chromium and WebKit.

## Previous: Wave 726 — frame-evaluate.spec.ts

### Wave 726
- [x] Port `tests/page/frame-evaluate.spec.ts`. Frame evaluate, OOPIF sessions, WebKit cross-frame handles. Chromium/WebKit 11 pass + 2 Node-only skips.

## Previous: Wave 725 — eval-on-selector-all.spec.ts

### Wave 725
- [x] Port `tests/page/eval-on-selector-all.spec.ts`. `$$eval` returns a function and ignores page `Array.from`. 9/9 Chromium and WebKit.

## Previous: Wave 724 — locator-misc-1.spec.ts

### Wave 724
- [x] Port `tests/page/locator-misc-1.spec.ts`. Locator hover/fill/clear/check/select/focus already matched. 13/13 Chromium and WebKit.

## Previous: Wave 723 — page-add-init-script.spec.ts

### Wave 723
- [x] Port `tests/page/page-add-init-script.spec.ts`. Disposable init scripts; WebKit bootstrap survives process swap. 11/11 Chromium and WebKit.

## Previous: Wave 722 — elementhandle-misc.spec.ts

### Wave 722
- [x] Port `tests/page/elementhandle-misc.spec.ts`. Hover/fill/check/select/focus/dispose already matched upstream. 10/10 Chromium and WebKit.

## Previous: Wave 721 — eval-on-selector.spec.ts

### Wave 721
- [x] Port `tests/page/eval-on-selector.spec.ts`. Official engines, capture `*`, shadow-piercing `$eval`. 26/26 Chromium and WebKit.

## Previous: Wave 720 — locator-is-visible.spec.ts

### Wave 720
- [x] Port `tests/page/locator-is-visible.spec.ts`. Official visibility, unknown engine, navigation-safe isVisible. 8/8 Chromium and WebKit.

## Previous: Wave 719 — page-add-style-tag.spec.ts

### Wave 719
- [x] Port `tests/page/page-add-style-tag.spec.ts`. Official missing-options error, CSS sourceURL, CSP race. 8/8 Chromium and WebKit.

## Previous: Wave 718 — page-add-script-tag.spec.ts

### Wave 718
- [x] Port `tests/page/page-add-script-tag.spec.ts`. Official missing-options error, path sourceURL, CSP race. Chromium 12/12; WebKit 11 pass + 1 official skip.

## Previous: Wave 717 — elementhandle-convenience.spec.ts

### Wave 717
- [x] Port `tests/page/elementhandle-convenience.spec.ts`. Official element preview and convenience getters. 15/15 Chromium and WebKit.

## Previous: Wave 716 — locator-query.spec.ts

### Wave 716
- [x] Port `tests/page/locator-query.spec.ts`. Official `>>` / `has` / `hasText` / `and` / `or`. 23 pass + 1 Node-only skip on Chromium and WebKit.

## Previous: Wave 715 — jshandle-properties.spec.ts

### Wave 715
- [x] Port `tests/page/jshandle-properties.spec.ts`. Primitive getProperties, inherited properties, unserializable jsonValue. 7/7 Chromium and WebKit.

## Previous: Wave 714 — locator-convenience.spec.ts

### Wave 714
- [x] Port `tests/page/locator-convenience.spec.ts`. Locator `ToString()`, `IsEditable`/`InputValue`/`InnerText` messages. 22/22 Chromium and WebKit.

## Previous: Wave 713 — page-dispatchevent.spec.ts

### Wave 713
- [x] Port `tests/page/page-dispatchevent.spec.ts`. Atomic page/frame dispatch; shadow piercing. Chromium 17/17; WebKit 14 pass + 3 device-orientation/motion skips.

## Previous: Wave 712 — page-mouse.spec.ts

### Wave 712
- [x] Port `tests/page/page-mouse.spec.ts`. Mouse floors CSS pixels; 16/16 Chromium and WebKit.

## Previous: Wave 711 — page-keyboard.spec.ts

### Wave 711
- [x] Port `tests/page/page-keyboard.spec.ts`. Plus-separated chords, Unicode type, and official key codes.

## Previous: Wave 710 — locator-click.spec.ts

### Wave 710
- [x] Port `tests/page/locator-click.spec.ts`. Click/dblclick with Node deleted and pointer detach.

## Previous: Wave 709 — page-event-console.spec.ts

### Wave 709
- [x] Port `tests/page/page-event-console.spec.ts`. Console text, types, timestamps, and duplicate logs.

## Previous: Wave 708 — page-network-idle.spec.ts

### Wave 708
- [x] Port `tests/page/page-network-idle.spec.ts`. 500ms quiet period; EventSource and WebSocket do not block idle.

## Previous: Wave 707 — page-emulate-media.spec.ts

### Wave 707
- [x] Port `tests/page/page-emulate-media.spec.ts`. Empty options leave print; default color scheme is light.

## Previous: Wave 706 — page-set-content.spec.ts

### Wave 706
- [x] Port `tests/page/page-set-content.spec.ts`. Official HTML serialization, commit waitUntil, and navigation-safe `ContentAsync`.

## Previous: Wave 705 — page-fill.spec.ts

### Wave 705
- [x] Port `tests/page/page-fill.spec.ts`. Official fill types, malformed values, and composed input events.

## Previous: Wave 704 — page-history.spec.ts

### Wave 704
- [x] Port `tests/page/page-history.spec.ts`. Reload ignores same-document `pushState`; `data:` reload returns null.

## Previous: Wave 703 — page-focus.spec.ts

### Wave 703
- [x] Port `tests/page/page-focus.spec.ts`. Zero-box tabindex nodes focus; non-focusable focus is a no-op.

## Previous: Wave 702 — page-basic.spec.ts

### Wave 702
- [x] Port `tests/page/page-basic.spec.ts`. URL, title, opener, frames, press, and `navigator.webdriver`.

## Previous: Wave 701 — page-dialog.spec.ts

### Wave 701
- [x] Port `tests/page/page-dialog.spec.ts`. Official `DialogClosed` fires after accept, dismiss, and auto-dismiss.

## Previous: Wave 700 — jshandle-to-string.spec.ts

### Wave 700
- [x] Port `tests/page/jshandle-to-string.spec.ts`. `ToString()` uses the protocol preview, not the C# type name.

## Previous: Wave 699 — jshandle-evaluate.spec.ts

### Wave 699
- [x] Port `tests/page/jshandle-evaluate.spec.ts`. Handle `evaluate` accepts a function or an expression.

## Previous: Wave 698 — jshandle-as-element.spec.ts

### Wave 698
- [x] Port `tests/page/jshandle-as-element.spec.ts`. `AsElement` works for elements, text nodes, and primitives.

## Previous: Wave 697 — jshandle-json-value.spec.ts

### Wave 697
- [x] Port `tests/page/jshandle-json-value.spec.ts`. Dates and circular graphs round-trip through `JsonValueAsync`.

## Previous: Wave 696 — locator-evaluate.spec.ts

### Wave 696
- [x] Port `tests/page/locator-evaluate.spec.ts`. Official `evaluateAll` takes an element array.

## Previous: Wave 695 — page-check.spec.ts

### Wave 695
- [x] Port `tests/page/page-check.spec.ts` → `DirectPageCheckParityTests.cs`.

## Previous: Wave 694 — setTestIdAttribute list

### Wave 694
- [x] `SetTestIdAttribute` comma-separated names.

## Previous: Wave 693 — ToHaveRole AriaRole

### Wave 693
- [x] `ToHaveRoleAsync(AriaRole)`.

## Previous: Wave 692 — GetByRole AriaRole

### Wave 692
- [x] `GetByRole(AriaRole)`.

## Previous: Wave 691 — KeyboardModifier ControlOrMeta

### Wave 691
- [x] `KeyboardModifier.ControlOrMeta`.

## Previous: Wave 690 — GetByRole nameRegex

### Wave 690
- [x] `GetByRole` `nameRegex`.

## Previous: Wave 689 — Locator SelectOption value force

### Wave 689
- [x] `ILocator.SelectOptionAsync` `force`.

## Previous: Wave 688 — Locator SelectOption handles force

### Wave 688
- [x] `ILocator.SelectOptionAsync` `force`.

## Previous: Wave 687 — Locator SelectOption handle force

### Wave 687
- [x] `ILocator.SelectOptionAsync` `force`.

## Previous: Wave 686 — Locator SelectOption strings force

### Wave 686
- [x] `ILocator.SelectOptionAsync` `force`.

## Previous: Wave 685 — SelectOption value force

### Wave 685
- [x] `SelectOptionAsync` `force`.

## Previous: Wave 684 — SelectOption handles force

### Wave 684
- [x] `SelectOptionAsync` `force`.

## Previous: Wave 683 — SelectOption handle force

### Wave 683
- [x] `SelectOptionAsync` `force`.

## Previous: Wave 682 — SelectOption empty force

### Wave 682
- [x] `SelectOptionAsync` `force`.

## Previous: Wave 681 — SelectOption strings force

### Wave 681
- [x] `SelectOptionAsync` `force`.

## Previous: Wave 680 — ConnectOverCDP artifactsDir

### Wave 680
- [x] `ConnectOverCDPAsync` `artifactsDir`.

## Previous: Wave 679 — ConnectOverCDP headers

### Wave 679
- [x] `ConnectOverCDPAsync` `headers`.

## Previous: Wave 678 — Clock install options

### Wave 678
- [x] `InstallAsync(ClockInstallOptions)`.

## Previous: Wave 677 — SelectOption empty strict

### Wave 677
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 676 — SelectOption value strict

### Wave 676
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 675 — SelectOption handles strict

### Wave 675
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 674 — SelectOption handle strict

### Wave 674
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 673 — SelectOption strings strict

### Wave 673
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 672 — SelectOption string strict

### Wave 672
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 671 — ElementHandle WaitForSelector strict

### Wave 671
- [x] `WaitForSelectorAsync` `strict`.

## Previous: Wave 670 — QuerySelector strict

### Wave 670
- [x] `QuerySelectorAsync` `strict`.

## Previous: Wave 669 — DragAndDrop strict

### Wave 669
- [x] `DragAndDropAsync` `strict`.

## Previous: Wave 668 — EvalOnSelector strict

### Wave 668
- [x] `EvalOnSelectorAsync` `strict`.

## Previous: Wave 667 — WaitForSelector strict

### Wave 667
- [x] `WaitForSelectorAsync` `strict`.

## Previous: Wave 666 — IsHidden strict

### Wave 666
- [x] `IsHiddenAsync` `strict`.

## Previous: Wave 665 — IsVisible strict

### Wave 665
- [x] `IsVisibleAsync` `strict`.

## Previous: Wave 664 — SetChecked strict

### Wave 664
- [x] `SetCheckedAsync` `strict`.

## Previous: Wave 663 — InputValue strict

### Wave 663
- [x] `InputValueAsync` `strict`.

## Previous: Wave 662 — DispatchEvent strict

### Wave 662
- [x] `DispatchEventAsync` `strict`.

## Previous: Wave 653 — SelectOption strict

### Wave 653
- [x] `SelectOptionAsync` `strict`.

## Previous: Wave 661 — strict })

### Wave 661
- [x] `IsEditableAsync` `strict`.

## Previous: Wave 660 — strict })

### Wave 660
- [x] `IsDisabledAsync` `strict`.

## Previous: Wave 659 — strict })

### Wave 659
- [x] `IsEnabledAsync` `strict`.

## Previous: Wave 658 — strict })

### Wave 658
- [x] `IsCheckedAsync` `strict`.

## Previous: Wave 657 — strict })

### Wave 657
- [x] `TextContentAsync` `strict`.

## Previous: Wave 656 — strict })

### Wave 656
- [x] `InnerHTMLAsync` `strict`.

## Previous: Wave 655 — strict })

### Wave 655
- [x] `InnerTextAsync` `strict`.

## Previous: Wave 654 — strict })

### Wave 654
- [x] `GetAttributeAsync` `strict`.

## Previous: Wave 652 — strict })

### Wave 652
- [x] `TapAsync` `strict`.

## Previous: Wave 651 — strict })

### Wave 651
- [x] `PressAsync` `strict`.

## Previous: Wave 650 — strict })

### Wave 650
- [x] `TypeAsync` `strict`.

## Previous: Wave 649 — strict })

### Wave 649
- [x] `FocusAsync` `strict`.

## Previous: Wave 648 — strict })

### Wave 648
- [x] `HoverAsync` `strict`.

## Previous: Wave 647 — strict })

### Wave 647
- [x] `UncheckAsync` `strict`.

## Previous: Wave 646 — Check strict

### Wave 646
- [x] `CheckAsync` `strict`.

## Previous: Wave 645 — DblClick strict

### Wave 645
- [x] `DblClickAsync` `strict`.

## Previous: Wave 644 — Fill strict

### Wave 644
- [x] `FillAsync` `strict`.

## Previous: Wave 643 — Click strict

### Wave 643
- [x] `ClickAsync` `strict`.

## Previous: Wave 642 — SetInputFiles strict

### Wave 642
- [x] `SetInputFilesAsync` `strict`.

## Previous: Wave 641 — APIRequest Tracing

### Wave 641
- [x] `IAPIRequestContext.Tracing`.

## Previous: Wave 640 — StorageState credentials

### Wave 640
- [x] `StorageStateAsync` `credentials`.

## Previous: Wave 639 — Credentials Delete

### Wave 639
- [x] `ICredentials.DeleteAsync`.

## Previous: Wave 638 — Credentials Get

### Wave 638
- [x] `ICredentials.GetAsync`.

## Previous: Wave 637 — Credentials Create

### Wave 637
- [x] `ICredentials.CreateAsync`.

## Previous: Wave 636 — Credentials Install

### Wave 636
- [x] `IBrowserContext.Credentials` `InstallAsync`.

## Previous: Wave 635 — Selectors Register

### Wave 635
- [x] `ISelectors.RegisterAsync`.

## Previous: Wave 634 — Screencast Overlay Visibility

### Wave 634
- [x] `IScreencast.ShowOverlaysAsync` / `HideOverlaysAsync`.

## Previous: Wave 633 — Screencast Actions

### Wave 633
- [x] `IScreencast.ShowActionsAsync` / `HideActionsAsync`.

## Previous: Wave 632 — Screencast Chapter

### Wave 632
- [x] `IScreencast.ShowChapterAsync`.

## Previous: Wave 631 — Screencast Overlay

### Wave 631
- [x] `IScreencast.ShowOverlayAsync`.

## Previous: Wave 630 — Page Screencast Start/Stop

### Wave 630
- [x] `IPage.Screencast` `StartAsync` / `StopAsync`.

## Previous: Wave 629 — Tracing StartHar

### Wave 629
- [x] `ITracing.StartHarAsync` / `StopHarAsync`.

## Previous: Wave 628 — APIRequest Fetch IRequest

### Wave 628
- [x] `IAPIRequestContext.FetchAsync(IRequest)`.

## Previous: Wave 627 — ScreenshotType Webp

### Wave 627
- [x] `ScreenshotType.Webp`.

## Previous: Wave 626 — Locator ElementHandles

### Wave 626
- [x] `ILocator.ElementHandlesAsync`.

## Previous: compatibility campaigns exhausted (through Wave 625)

APIResponse leftover campaign is complete through Wave 625
(`IAPIResponse.Timing`).

## Previous: Wave 625 — APIResponse Timing

### Wave 625
- [x] `IAPIResponse.Timing`.

## Previous: Wave 624 — APIResponse SecurityDetails

### Wave 624
- [x] `IAPIResponse.SecurityDetailsAsync`.

## Previous: Wave 623 — APIResponse ServerAddr

### Wave 623
- [x] `IAPIResponse.ServerAddrAsync`.

## Previous: Wave 622 — Page SessionStorage

### Wave 622
- [x] `IPage.SessionStorage`.

## Previous: Wave 621 — Page LocalStorage

### Wave 621
- [x] `IPage.LocalStorage`.

## Previous: Wave 620 — ElementHandle DblClick steps

### Wave 620
- [x] `IElementHandle.DblClickAsync` `steps`.

## Previous: Wave 619 — Locator DblClick steps

### Wave 619
- [x] `ILocator.DblClickAsync` `steps`.

## Previous: Wave 618 — PageErrors filter

### Wave 618
- [x] `IPage.PageErrorsAsync` `filter`.

## Previous: Wave 617 — ConsoleMessages filter

### Wave 617
- [x] `IPage.ConsoleMessagesAsync` `filter`.

## Previous: Wave 616 — Page/Frame Click steps

### Wave 616
- [x] `IPage.ClickAsync` / `IFrame.ClickAsync` `steps`.

## Previous: Wave 615 — Locator Click steps

### Wave 615
- [x] `ILocator.ClickAsync` `steps`.

## Previous: Wave 614 — Page AriaSnapshotJson

### Wave 614
- [x] `IPage.AriaSnapshotJsonAsync()`.

## Previous: Wave 613 — Locator AriaSnapshotJson

### Wave 613
- [x] `ILocator.AriaSnapshotJsonAsync()`.

## Previous: Wave 612 — IFrameLocator.Locator(ILocator)

### Wave 612
- [x] `IFrameLocator.Locator(ILocator)`.

## Previous: Wave 611 — AriaSnapshot boxes

### Wave 611
- [x] `AriaSnapshotAsync` `boxes`.

## Previous: Wave 610 — AriaSnapshot depth

### Wave 610
- [x] `AriaSnapshotAsync` `depth`.

## Previous: Wave 609 — Page AriaSnapshot

### Wave 609
- [x] `IPage.AriaSnapshotAsync()` (no selector).

## Previous: compatibility campaigns exhausted (through Wave 608)

Locator filter campaign (`filter({ visible })`, `description`,
`ariaSnapshot({ mode })`, `page.locator(otherLocator)`) is on `main`
through Wave 608.

## Previous: Wave 608 — IPage.Locator(ILocator)

### Wave 608
- [x] `IPage.Locator(ILocator)`.

## Previous: Wave 607 — AriaSnapshot mode

### Wave 607
- [x] `AriaSnapshotAsync` `mode`.

## Previous: Wave 606 — Locator Description

### Wave 606
- [x] `ILocator.Description`.

## Previous: Wave 605 — Locator Filter visible

### Wave 605
- [x] `ILocator.Filter(bool visible)`.

## Previous: compatibility campaigns exhausted (through Wave 604)

v1.60 leftover campaign (`getByRole` description, highlight style,
`toHaveCSS` pseudo) is on `main` through Wave 604.

## Previous: Wave 604 — ToHaveCSS pseudo

### Wave 604
- [x] `ToHaveCSSAsync` `pseudo` (`::before` / `::after`).

## Previous: Wave 603 — Locator Highlight style

### Wave 603
- [x] `ILocator.HighlightAsync` `style` option.

## Previous: Wave 602 — GetByRole description Regex

### Wave 602
- [x] `GetByRole(..., descriptionRegex)`.

## Previous: Wave 601 — GetByRole description

### Wave 601
- [x] `GetByRole(..., description)` string.

## Previous: compatibility campaigns exhausted (through Wave 600)

Locator leftover campaign (`hideHighlight`, `normalize`, `drop`,
`waitForFunction`, `locator(otherLocator)`) is on `main` through Wave 600.

## Previous: Wave 600 — Locator Locator(ILocator)

### Wave 600
- [x] `ILocator.Locator(ILocator)`.

## Previous: Wave 599 — Locator WaitForFunctionAsync

### Wave 599
- [x] `ILocator.WaitForFunctionAsync`.

## Previous: Wave 598 — Locator DropAsync

### Wave 598
- [x] `ILocator.DropAsync`.

## Previous: Wave 597 — Locator NormalizeAsync

### Wave 597
- [x] `ILocator.NormalizeAsync`.

## Previous: Wave 596 — HideHighlight

### Wave 596
- [x] `ILocator.HideHighlightAsync` / `IPage.HideHighlightAsync`.

## Previous: compatibility campaigns exhausted (through Wave 595)

Firefox persistent launch and `FirefoxUserPrefs` are on `main`. Official
locator leftovers (`hideHighlight`, `normalize`, `drop`, …) continue at
Wave 596.

## Previous: Firefox persistent campaign exhausted (through Wave 595)

### Wave 595
- [x] `FirefoxUserPrefs` on Firefox launch / persistent options.

## Previous: Wave 594 — Firefox LaunchPersistentContext

### Wave 594
- [x] `LaunchPersistentContextAsync` on Firefox.

## Previous: IgnoreDefaultArgs campaign exhausted (through Wave 593)

Official `IgnoreDefaultArgs` bool (drop all defaults) and
`IgnoreDefaultArgsList` (omit named default switches such as
`--mute-audio`) are on `main`. Existing bool tests still pass.

## Previous: Wave 593 — IgnoreDefaultArgs list

### Wave 593
- [x] `BrowserTypeLaunchOptions.IgnoreDefaultArgs` list omits named default args.

## Previous: GetBy Regex campaign exhausted (through Wave 592)

Official `GetBy*(Regex)`, `Filter(Regex)`, and `HasNotText(Regex)` are on
`main`.

## Previous: Wave 592 — Filter / HasNotText Regex

### Wave 592
- [x] `Filter(Regex)` / `HasNotText(Regex)`.

## Previous: Wave 591 — GetByAltText / Title / TestId Regex

### Wave 591
- [x] `GetByAltText(Regex)` / `GetByTitle(Regex)` / `GetByTestId(Regex)`.

## Previous: Wave 590 — GetByLabel / GetByPlaceholder Regex

### Wave 590
- [x] `GetByLabel(Regex)` / `GetByPlaceholder(Regex)`.

## Previous: Wave 589 — GetByText Regex

### Wave 589
- [x] `GetByText(Regex)` on locator / page / frame.

## Previous: tracing groups campaign exhausted (through Wave 588)

`ITracing.GroupAsync` / `GroupEndAsync` write begin/end events into the
existing Chrome JSON tracer.

## Previous: Wave 588 — tracing GroupEndAsync

### Wave 588
- [x] `ITracing.GroupEndAsync`.

## Previous: Wave 587 — tracing GroupAsync

### Wave 587
- [x] `ITracing.GroupAsync`.

## Previous: compatibility campaigns exhausted (through Wave 586)

UnrouteBehavior (`Wait` / `IgnoreErrors` / `Default`) is on `IPage` and
`IBrowserContext` `UnrouteAsync` / `UnrouteAllAsync`. Tracing groups
start at Wave 587.

## Previous: Wave 586 — UnrouteBehavior context

### Wave 586
- [x] `IBrowserContext.UnrouteAsync` / `UnrouteAllAsync` behavior.

## Previous: Wave 585 — UnrouteBehavior page

### Wave 585
- [x] `IPage.UnrouteAsync` / `UnrouteAllAsync` behavior.

## Previous: Wave 584 — UnrouteBehavior enum

### Wave 584
- [x] `UnrouteBehavior` public type.

## Previous: tracing chunks campaign exhausted (through Wave 583)

`ITracing.StartChunkAsync` / `StopChunkAsync` (name, title, path) are on `main`.

## Previous: Wave 583 — tracing StopChunk

### Wave 583
- [x] `ITracing.StopChunkAsync` writes a file.

## Previous: Wave 582 — tracing StartChunk

### Wave 582
- [x] `ITracing.StartChunkAsync`.

## Previous: Firefox smoke campaign exhausted (through Wave 581)

Juggler connect and portable launch classes that can run here are on `main`.
Remaining persistent / downloads / artifacts Direct tests Ignore with a
real ABI reason (`LaunchPersistentContext` is not wired; HTTP GoTo does
not commit on this Firefox stack). Do not invent Firefox-only APIs.

## Previous: Wave 581 — Firefox persistent ABI ignores

### Wave 581
- [x] Document remaining persistent Direct tests with a real `LaunchPersistentContext` ABI ignore.

## Previous: Wave 580 — Firefox launch HandleSIGHUP

### Wave 580
- [x] Additional portable Direct class (`DirectLaunchHandleSighupTests`) that failed for the same handshake / BrowserType reason.

## Previous: Wave 579 — Firefox launch HandleSIGTERM

### Wave 579
- [x] Additional portable Direct class (`DirectLaunchHandleSigtermTests`) that failed for the same handshake / BrowserType reason.

## Previous: Wave 578 — Firefox launch HandleSIGINT

### Wave 578
- [x] Additional portable Direct class (`DirectLaunchHandleSigintTests`) that failed for the same handshake / BrowserType reason.

## Previous: Wave 577 — Firefox launch Timeout

### Wave 577
- [x] Additional portable Direct class (`DirectLaunchTimeoutTests`) that failed for the same handshake / BrowserType reason.

## Previous: Wave 576 — Firefox launch Args

### Wave 576
- [x] Additional portable Direct class (`DirectLaunchArgsTests`) that failed for the same handshake / BrowserType reason.

## Previous: Wave 575 — Firefox BrowserType.LaunchAsync

### Wave 575
- [x] Additional portable Direct class (`DirectBrowserTypeLaunchTests`) that failed for the same handshake / BrowserType reason.

## Previous: Wave 574 — Firefox first portable class

### Wave 574
- [x] One portable Direct class actually runs on Firefox (not Ignore).

## Previous: Wave 573 — Firefox Juggler connect

### Wave 573
- [x] Keep the Juggler session alive through `LaunchFirefoxAsync` / connect.

## Previous: expect options campaign exhausted (through Wave 572)

Official per-call expect options that can run on the direct CR/WK stacks
are on `main` (`ignoreCase`, `useInnerText`, `checked`/`indeterminate`,
`attached`, `enabled`/`visible`/`editable`, ToHaveAttribute presence).
Do not invent more unofficial matchers.

## Previous: Wave 572 — expect ToHaveAttribute name-only

### Wave 572
- [x] `ToHaveAttributeAsync(name)` presence.

## Previous: Wave 571 — expect enabled / visible / editable options

### Wave 571
- [x] `ToBeEnabledAsync(enabled:)` / `ToBeVisibleAsync(visible:)` / `ToBeEditableAsync(editable:)`.

## Previous: Wave 570 — expect a11y ignoreCase

### Wave 570
- [x] `ToHaveAccessibleNameAsync` / `ToHaveAccessibleDescriptionAsync` / `ToHaveAccessibleErrorMessageAsync` `ignoreCase`.

## Previous: Wave 569 — expect ToHaveAttribute ignoreCase

### Wave 569
- [x] `ToHaveAttributeAsync(..., ignoreCase)`.

## Previous: Wave 568 — expect attached / URL ignoreCase

### Wave 568
- [x] `ToBeAttachedAsync(attached:)` / `ToHaveURLAsync(ignoreCase:)`.

## Previous: Wave 567 — expect ToBeChecked options

### Wave 567
- [x] `ToBeCheckedAsync(checked:, indeterminate:)`.

## Previous: Wave 566 — expect useInnerText

### Wave 566
- [x] `ToHaveTextAsync` / `ToContainTextAsync` `useInnerText`.

## Previous: Wave 565 — expect ignoreCase text

### Wave 565
- [x] `ToHaveTextAsync` / `ToContainTextAsync` `ignoreCase`.

## Previous: compatibility campaigns exhausted (through Wave 564)

Firefox executable discovery and `BrowserLauncher` → `LaunchFirefoxAsync`
are on `main`. Portable Direct tests Ignore when this Linux Juggler
session closes during connect (`Session disposed`). Do not invent
more Firefox stub APIs. Expect option bags start at Wave 565.

## Previous: Wave 564 — Firefox launcher

### Wave 564
- [x] `BrowserLauncher` launches Firefox when `PRODUCT=FIREFOX`.

## Previous: Wave 563 — Firefox executable

### Wave 563
- [x] `BrowserExecutableFixture` resolves a Firefox executable when `PRODUCT=FIREFOX`.

## Previous: HAR update campaign exhausted (through Wave 562)

`RouteFromHARAsync` `update`, `updateMode`, and `updateContent` are on `main`.

## Previous: Wave 562 — HAR updateContent

### Wave 562
- [x] `RouteFromHARAsync(..., updateContent)` embeds or attaches recorded bodies.

## Previous: Wave 561 — HAR updateMode

### Wave 561
- [x] `RouteFromHARAsync(..., updateMode)` records a minimal or full HAR.

## Previous: Wave 560 — HAR update

### Wave 560
- [x] `RouteFromHARAsync(..., update: true)` writes or extends the HAR.

## Previous: client certificates campaign exhausted (through Wave 559)

`ClientCertificate`, context/persistent options, and standalone
`APIRequest.NewContextAsync(clientCertificates)` are on `main`.

## Previous: Wave 559 — standalone APIRequest ClientCertificates

### Wave 559
- [x] `IAPIRequest.NewContextAsync(clientCertificates)`.

## Previous: Wave 558 — persistent ClientCertificates

### Wave 558
- [x] `BrowserTypeLaunchPersistentContextOptions.ClientCertificates`.

## Previous: Wave 557 — context ClientCertificates

### Wave 557
- [x] `BrowserContextOptions.ClientCertificates` / `NewContextAsync`.

## Previous: Wave 556 — ClientCertificate model

### Wave 556
- [x] `ClientCertificate` public type.

## Previous: Wave 555 — page pause timeout

### Wave 555
- [x] `IPage.PauseAsync` times out using `DefaultTimeout`.

## Previous: pause campaign exhausted (through Wave 555)

`IPage.PauseAsync` waits without an inspector overlay. Close resumes;
`DefaultTimeout` fails the wait. No debugger UI.

## Previous: Wave 554 — page PauseAsync

### Wave 554
- [x] `IPage.PauseAsync`.

## Previous: Wave 553 — screenshot animations / caret / mask

### Wave 553
- [x] `ToHaveScreenshotAsync` `animations` / `caret` / `omitBackground` / `mask`.

## Previous: screenshot campaign exhausted (through Wave 553)

Official `ToHaveScreenshot` overloads that can run here are on `main`
(golden/bytes, page matcher, maxDiff/threshold, capture decorations).

## Previous: Wave 552 — screenshot maxDiff / threshold

### Wave 552
- [x] `ToHaveScreenshotAsync` `maxDiffPixels` / `maxDiffPixelRatio` / `threshold`.

## Previous: Wave 551 — screenshot page ToHaveScreenshot

### Wave 551
- [x] `IPageAssertions.ToHaveScreenshotAsync`.

## Previous: Wave 550 — screenshot locator ToHaveScreenshot

### Wave 550
- [x] `ILocatorAssertions.ToHaveScreenshotAsync`.

## Previous: expect campaign exhausted (through Wave 549)

Official expect matchers through Wave 549 are on `main`. Per-call option bags
(`ignoreCase`, `useInnerText`, `checked: false`) are `tasks/expect-options-campaign.md`.

### Wave 549
- [x] `Assertions.SetDefaultExpectTimeout`.

## Previous: Wave 548 — expect ToPass

### Wave 548
- [x] `ILocatorAssertions.ToPassAsync`.

## Previous: Wave 547 — expect ToContainClass list

### Wave 547
- [x] `ToContainClassAsync(IEnumerable<string>)`.

## Previous: Wave 546 — expect ToContainText / ToHaveValues Regex list

### Wave 546
- [x] `ToContainTextAsync(IEnumerable<Regex>)` / `ToHaveValuesAsync(IEnumerable<Regex>)`.

## Previous: Wave 545 — expect ToHaveClass list

### Wave 545
- [x] `ToHaveClassAsync(IEnumerable<string>)` / `ToHaveClassAsync(IEnumerable<Regex>)`.

## Previous: Wave 544 — expect ToHaveText list

### Wave 544
- [x] `ToHaveTextAsync(IEnumerable<string>)` / `ToHaveTextAsync(IEnumerable<Regex>)`.

## Previous: Wave 543 — expect ToHaveAccessibleDescription / ToHaveAccessibleErrorMessage Regex

### Wave 543
- [x] `ToHaveAccessibleDescriptionAsync(Regex)` / `ToHaveAccessibleErrorMessageAsync(Regex)`.

## Previous: Wave 542 — expect ToHaveClass / ToHaveAccessibleName Regex

### Wave 542
- [x] `ToHaveClassAsync(Regex)` / `ToHaveAccessibleNameAsync(Regex)`.

## Previous: Wave 541 — expect ToHaveId / ToHaveCSS Regex

### Wave 541
- [x] `ToHaveIdAsync(Regex)` / `ToHaveCSSAsync(name, Regex)`.

## Previous: Wave 540 — expect ToHaveAttribute / ToHaveValue Regex

### Wave 540
- [x] `ToHaveAttributeAsync(name, Regex)` / `ToHaveValueAsync(Regex)`.

## Previous: Wave 539 — expect ToHaveText / ToContainText Regex

### Wave 539
- [x] `ToHaveTextAsync(Regex)` / `ToContainTextAsync(Regex)`.

## Previous: Wave 538 — expect API ToBeOK

### Wave 538
- [x] `Assertions.Expect(IAPIResponse).ToBeOKAsync`.

## Previous: Wave 537 — expect page ToMatchAriaSnapshot

### Wave 537
- [x] `IPageAssertions.ToMatchAriaSnapshotAsync`.

## Previous: Wave 536 — expect page title/URL Regex

### Wave 536
- [x] `ToHaveTitleAsync(Regex)` / `ToHaveURLAsync(Regex)`.

## Previous: Wave 535 — expect page ToHaveTitle / ToHaveURL

### Wave 535
- [x] `Assertions.Expect(IPage).ToHaveTitleAsync` / `ToHaveURLAsync`.

## Previous: Wave 534 — expect ToHaveAccessibleErrorMessage / ToContainText list

### Wave 534
- [x] `ToHaveAccessibleErrorMessageAsync` / `ToContainTextAsync(IEnumerable<string>)`.

## Previous: Wave 533 — expect ToContainClass / ToHaveValues

### Wave 533
- [x] `ToContainClassAsync` / `ToHaveValuesAsync`.

## Previous: Wave 532 — expect ToBeEmpty / ToContainText

### Wave 532
- [x] `ToBeEmptyAsync` / `ToContainTextAsync`.

## Previous: Wave 531 — expect ToHaveAccessibleDescription / ToMatchAriaSnapshot

### Wave 531
- [x] `ToHaveAccessibleDescriptionAsync` / `ToMatchAriaSnapshotAsync`.

## Previous: Wave 530 — expect ToHaveRole / ToHaveAccessibleName

### Wave 530
- [x] `ToHaveRoleAsync` / `ToHaveAccessibleNameAsync`.

## Previous: Wave 529 — expect ToHaveJSProperty / ToBeInViewport

### Wave 529
- [x] `ToHaveJSPropertyAsync` / `ToBeInViewportAsync`.

## Previous: Wave 528 — expect ToHaveClass / ToHaveCSS

### Wave 528
- [x] `ToHaveClassAsync` / `ToHaveCSSAsync`.

## Previous: Wave 527 — expect ToBeAttached / ToBeFocused

### Wave 527
- [x] `ToBeAttachedAsync` / `ToBeFocusedAsync`.

## Previous: Wave 526 — expect text / attribute / value

### Wave 526
- [x] `ToHaveTextAsync` / `ToHaveAttributeAsync` / `ToHaveValueAsync` / `ToHaveIdAsync`.

## Previous: Wave 525 — expect Not / enabled / checked

### Wave 525
- [x] `ILocatorAssertions.Not` / `ToBeEnabledAsync` / `ToBeDisabledAsync` / `ToBeEditableAsync` / `ToBeCheckedAsync`.

## Previous: Wave 524 — expect ToBeVisible / ToHaveCount

### Wave 524
- [x] `Assertions.Expect(ILocator).ToBeVisibleAsync` / `ToBeHiddenAsync` / `ToHaveCountAsync`.

## Previous: Wave 523 — locator AriaSnapshot

### Wave 523
- [x] `ILocator.AriaSnapshotAsync` returns an accessibility tree snapshot.

## Previous: Wave 522 — locator Describe

### Wave 522
- [x] `ILocator.Describe` names a locator for strict errors.

## Previous: Wave 521 — locator Highlight / PressSequentially

### Wave 521
- [x] `ILocator.HighlightAsync` / `PressSequentiallyAsync`.

## Previous: Wave 520 — locator DragTo

### Wave 520
- [x] `ILocator.DragToAsync` drags this locator onto another.

## Previous: Wave 519 — frame locator GetBy*

### Wave 519
- [x] `IFrameLocator.GetByRole` / `GetByText` / `GetByLabel` / `GetByPlaceholder` / `GetByAltText` / `GetByTitle` / `GetByTestId`.

## Previous: Wave 518 — locator FrameLocator

### Wave 518
- [x] `ILocator.FrameLocator` finds descendant iframes of this locator.

## Previous: Wave 517 — locator AllInnerTexts / AllTextContents / EvaluateHandle

### Wave 517
- [x] `ILocator.AllInnerTextsAsync` / `AllTextContentsAsync` / `EvaluateHandleAsync`.

## Previous: Wave 516 — locator ScrollIntoView / Blur / SelectText

### Wave 516
- [x] `ILocator.ScrollIntoViewIfNeededAsync` / `BlurAsync` / `SelectTextAsync`.

## Previous: Wave 515 — locator SetInputFiles / Screenshot / DispatchEvent

### Wave 515
- [x] `ILocator.SetInputFilesAsync` / `ScreenshotAsync` / `DispatchEventAsync`.

## Previous: Wave 514 — locator Evaluate / BoundingBox

### Wave 514
- [x] `ILocator.EvaluateAsync` / `EvaluateAllAsync` / `BoundingBoxAsync`.

## Previous: Wave 513 — locator WaitFor / Clear / SelectOption

### Wave 513
- [x] `ILocator.WaitForAsync` / `ClearAsync` / `SelectOptionAsync`.

## Previous: Wave 512 — locator Filter hasNot

### Wave 512
- [x] `ILocator.HasNot` / `HasNotText`.

## Previous: Wave 511 — locator GetBy*

### Wave 511
- [x] `ILocator.GetByRole` / `GetByText` / `GetByLabel` / `GetByPlaceholder` / `GetByAltText` / `GetByTitle` / `GetByTestId`.

## Previous: Wave 510 — AddLocatorHandlerAsync

### Wave 510
- [x] `IPage.AddLocatorHandlerAsync` / `RemoveLocatorHandlerAsync`.

## Previous: Wave 509 — screenshot mask

### Wave 509
- [x] Screenshot <c>mask</c> accepts locators.

## Previous: Wave 508 — IFrameLocator

### Wave 508
- [x] `IPage` / `IFrame` `FrameLocator`, `IFrameLocator.Locator` / First / Last / Nth, `ILocator.ContentFrame`.

## Previous: Wave 507 — locator Filter / And / Or / Has

### Wave 507
- [x] `ILocator.Filter` / `And` / `Or` / `Has`.

## Previous: Wave 506 — GetBy* locator overloads

### Wave 506
- [x] `IPage` / `IFrame` `GetByRole` / `GetByText` / `GetByLabel` / `GetByPlaceholder` / `GetByAltText` / `GetByTitle` / `GetByTestId` return locators. Handle `*Async` methods remain.

## Previous: Wave 505 — locator text / press / type

### Wave 505
- [x] `ILocator.GetAttributeAsync` / `InnerTextAsync` / `InnerHTMLAsync` / `InputValueAsync` / `PressAsync` / `TypeAsync`.

## Previous: Wave 504 — locator visibility / enabled queries

### Wave 504
- [x] `ILocator.IsVisibleAsync` / `IsHiddenAsync` / `IsEnabledAsync` / `IsDisabledAsync` / `IsEditableAsync`.

## Previous: Wave 503 — locator Check / Uncheck / SetChecked

### Wave 503
- [x] `ILocator.CheckAsync` / `UncheckAsync` / `SetCheckedAsync` / `IsCheckedAsync`.

## Previous: Wave 502 — locator Hover / DblClick / Focus / Tap

### Wave 502
- [x] `ILocator.HoverAsync` / `DblClickAsync` / `FocusAsync` / `TapAsync`.

## Previous: Wave 501 — ILocator foundation

### Wave 501
- [x] `IPage.Locator` / `IFrame.Locator` returns a lazy, strict `ILocator` with First/Last/Nth, chaining, Count/All, Click, Fill, and TextContent.

## Previous: leftover Playwright API campaign exhausted

The leftover hunt is exhausted. Every official `BrowserContextOptions` leftover that can run on the direct Chromium and WebKit stacks is now on `BrowserTypeLaunchPersistentContextOptions`. Remaining official Playwright surface is already present, or is on the skip list (locators are now the active campaign in `tasks/locator-campaign.md`):

- Connect / LaunchServer remote-endpoint work (not a Node driver)
- `BrowserTypeLaunchOptions.TracesDir` mapped onto local `ITracing` (Playwright zip traces, not Chromium performance tracing)
- `ITracing.GroupAsync` / `GroupEndAsync` — `tasks/tracing-groups-campaign.md`
- `FirefoxUserPrefs` — `tasks/firefox-persistent-campaign.md`
- `SlowMo` (no clean hook)
- `IgnoreDefaultArgs` as `string[]` — `tasks/ignore-default-args-campaign.md`
- Screenshot `mask`, `IPage.PauseAsync`, `clientCertificates`, `RouteFromHAR(..., update: true)`
- `IPage` : `IAsyncDisposable` (CA2000)
- `UnrouteBehavior` — `tasks/unroute-behavior-campaign.md`
- `ITracing.StartChunk` / `StopChunk` — `tasks/tracing-chunks-campaign.md`

## Previous: Wave 500 — Persistent Context RecordHarUrlRegex

### Wave 500
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordHarUrlRegex` filters HAR entries by regex on `LaunchPersistentContextAsync`.

## Previous: Wave 499 — Persistent Context RecordHarContent

### Wave 499
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordHarContent` attaches HAR bodies as sidecar files on `LaunchPersistentContextAsync`.

## Previous: Wave 498 — Persistent Context RecordHarMode

### Wave 498
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordHarMode` writes a minimal HAR on `LaunchPersistentContextAsync`.

## Previous: Wave 497 — Persistent Context RecordHarUrl

### Wave 497
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordHarUrl` filters HAR entries on `LaunchPersistentContextAsync`.

## Previous: Wave 496 — Persistent Context StorageStatePath

### Wave 496
- [x] `BrowserTypeLaunchPersistentContextOptions.StorageStatePath` restores cookies from a file on `LaunchPersistentContextAsync`.

## Previous: Wave 495 — Persistent Context RecordVideoSize

### Wave 495
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordVideoSize` sets the video frame size on `LaunchPersistentContextAsync`.

## Previous: Wave 494 — Persistent Context RecordHarOmitContent

### Wave 494
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordHarOmitContent` omits HAR response bodies on `LaunchPersistentContextAsync`.

## Previous: Wave 493 — Persistent Context StorageState

### Wave 493
- [x] `BrowserTypeLaunchPersistentContextOptions.StorageState` restores cookies on `LaunchPersistentContextAsync`.

## Previous: Wave 492 — Persistent Context RecordVideoDir

### Wave 492
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordVideoDir` records page video on `LaunchPersistentContextAsync`.

## Previous: Wave 491 — Persistent Context RecordHarPath

### Wave 491
- [x] `BrowserTypeLaunchPersistentContextOptions.RecordHarPath` writes a HAR file on `LaunchPersistentContextAsync`.

## Previous: Wave 490 — Persistent Context Contrast

### Wave 490
- [x] `BrowserTypeLaunchPersistentContextOptions.Contrast` emulates `prefers-contrast` on `LaunchPersistentContextAsync`.

## Previous: Wave 489 — Persistent Context HttpCredentials

### Wave 489
- [x] `BrowserTypeLaunchPersistentContextOptions.HttpCredentials` sends HTTP Basic auth on `LaunchPersistentContextAsync`.

## Previous: Wave 488 — Persistent Context ServiceWorkers

### Wave 488
- [x] `BrowserTypeLaunchPersistentContextOptions.ServiceWorkers` blocks service worker registration on `LaunchPersistentContextAsync`.

## Previous: Wave 487 — Persistent Context StrictSelectors

### Wave 487
- [x] `BrowserTypeLaunchPersistentContextOptions.StrictSelectors` throws on ambiguous selectors on `LaunchPersistentContextAsync`.

## Previous: Wave 486 — Persistent Context BaseURL

### Wave 486
- [x] `BrowserTypeLaunchPersistentContextOptions.BaseURL` resolves relative navigation URLs on `LaunchPersistentContextAsync`.

## Previous: Wave 485 — Persistent Context AcceptDownloads

### Wave 485
- [x] `BrowserTypeLaunchPersistentContextOptions.AcceptDownloads` saves attachments on `LaunchPersistentContextAsync`.

## Previous: Wave 484 — Persistent Context ScreenSize

### Wave 484
- [x] `BrowserTypeLaunchPersistentContextOptions.ScreenSize` sets window.screen on `LaunchPersistentContextAsync`.

## Previous: Wave 483 — Persistent Context IsMobile

### Wave 483
- [x] `BrowserTypeLaunchPersistentContextOptions.IsMobile` emulates a mobile viewport on `LaunchPersistentContextAsync`.

## Previous: Wave 482 — Persistent Context DeviceScaleFactor

### Wave 482
- [x] `BrowserTypeLaunchPersistentContextOptions.DeviceScaleFactor` sets device pixel ratio on `LaunchPersistentContextAsync`.

## Previous: Wave 481 — Persistent Context JavaScriptEnabled

### Wave 481
- [x] `BrowserTypeLaunchPersistentContextOptions.JavaScriptEnabled` disables page scripts on `LaunchPersistentContextAsync`.

## Previous: Wave 480 — Persistent Context IgnoreHTTPSErrors

### Wave 480
- [x] `BrowserTypeLaunchPersistentContextOptions.IgnoreHTTPSErrors` accepts untrusted TLS certs on `LaunchPersistentContextAsync`.

## Previous: Wave 479 — Persistent Context BypassCSP

### Wave 479
- [x] `BrowserTypeLaunchPersistentContextOptions.BypassCSP` bypasses Content-Security-Policy on `LaunchPersistentContextAsync`.

## Previous: Wave 478 — Persistent Context Permissions

### Wave 478
- [x] `BrowserTypeLaunchPersistentContextOptions.Permissions` grants permissions on `LaunchPersistentContextAsync`.

## Previous: Wave 477 — Persistent Context Geolocation

### Wave 477
- [x] `BrowserTypeLaunchPersistentContextOptions.Geolocation` sets the geolocation on `LaunchPersistentContextAsync`.

## Previous: Wave 476 — Persistent Context ExtraHTTPHeaders

### Wave 476
- [x] `BrowserTypeLaunchPersistentContextOptions.ExtraHTTPHeaders` sends extra headers on `LaunchPersistentContextAsync`.

## Previous: Wave 475 — Persistent Context HasTouch

### Wave 475
- [x] `BrowserTypeLaunchPersistentContextOptions.HasTouch` emulates touch on `LaunchPersistentContextAsync`.

## Previous: Wave 474 — Persistent Context ForcedColors

### Wave 474
- [x] `BrowserTypeLaunchPersistentContextOptions.ForcedColors` emulates `forced-colors` on `LaunchPersistentContextAsync`.

## Previous: Wave 473 — Persistent Context ReducedMotion

### Wave 473
- [x] `BrowserTypeLaunchPersistentContextOptions.ReducedMotion` emulates `prefers-reduced-motion` on `LaunchPersistentContextAsync`.

## Previous: Wave 472 — Persistent Context ColorScheme

### Wave 472
- [x] `BrowserTypeLaunchPersistentContextOptions.ColorScheme` emulates `prefers-color-scheme` on `LaunchPersistentContextAsync`.

## Previous: Wave 471 — Persistent Context Offline

### Wave 471
- [x] `BrowserTypeLaunchPersistentContextOptions.Offline` emulates being offline on `LaunchPersistentContextAsync`.

## Previous: Wave 470 — Persistent Context UserAgent

### Wave 470
- [x] `BrowserTypeLaunchPersistentContextOptions.UserAgent` overrides `navigator.userAgent` on `LaunchPersistentContextAsync`.

## Previous: Wave 469 — Persistent Context Timezone

### Wave 469
- [x] `BrowserTypeLaunchPersistentContextOptions.TimezoneId` sets the IANA timezone on `LaunchPersistentContextAsync`.

## Previous: Wave 468 — Persistent Context Locale

### Wave 468
- [x] `BrowserTypeLaunchPersistentContextOptions.Locale` sets `navigator.language` on `LaunchPersistentContextAsync`.

## Previous: Wave 467 — Persistent Context Viewport

### Wave 467
- [x] `BrowserTypeLaunchPersistentContextOptions.ViewportSize` sets the default viewport on `LaunchPersistentContextAsync`.

## Previous: Wave 466 — Launch ArtifactsDir

### Wave 466
- [x] `BrowserTypeLaunchOptions.ArtifactsDir` saves downloads, videos, HAR, and traces into a directory that is not cleaned up when the browser closes.

## Previous: Wave 465 — Launch HandleSIGHUP

### Wave 465
- [x] `BrowserTypeLaunchOptions.HandleSIGHUP` closes the browser process on SIGHUP. Defaults to true.

## Previous: Wave 464 — Launch HandleSIGTERM

### Wave 464
- [x] `BrowserTypeLaunchOptions.HandleSIGTERM` closes the browser process on SIGTERM. Defaults to true.

## Previous: Wave 463 — Launch HandleSIGINT

### Wave 463
- [x] `BrowserTypeLaunchOptions.HandleSIGINT` closes the browser process on Ctrl-C. Defaults to true.

## Previous: Wave 462 — Launch Persistent Context

### Wave 462
- [x] `LaunchPersistentContextAsync` launches a browser that stores cookies and local storage in `userDataDir`.

## Previous: Wave 461 — ConnectOverCDP

### Wave 461
- [x] `ConnectOverCDPAsync` attaches to an existing Chromium over CDP.

## Previous: Wave 460 — Context BackgroundPage event

### Wave 460
- [x] `IBrowserContext.BackgroundPage` fires when a Chromium extension background page is created.

## Previous: Wave 459 — Context BackgroundPages

### Wave 459
- [x] `IBrowserContext.BackgroundPages` lists Chromium extension background pages.

## Previous: Wave 458 — Playwright Devices

### Wave 458
- [x] `Playwright.Devices` exposes official Playwright device descriptors.

## Previous: Wave 457 — Playwright browser types

### Wave 457
- [x] `Playwright.Chromium`, `Firefox`, and `Webkit` expose the official browser types.

## Previous: Wave 456 — BrowserType Launch

### Wave 456
- [x] Engine `LaunchAsync` launches a browser for that engine.

## Previous: Wave 455 — WebError Location

### Wave 455
- [x] `IWebError.Location` reports the file, line, and column of an uncaught page exception.

## Previous: Wave 454 — Route Fetch postDataText

### Wave 454
- [x] `IRoute.FetchAsync` accepts a UTF-8 `postDataText` body override.

## Previous: Wave 453 — SetTestIdAttribute

### Wave 453
- [x] `Playwright.SetTestIdAttribute` configures the attribute used by `GetByTestIdAsync`.

## Previous: Wave 452 — Context WebError

### Wave 452
- [x] `IBrowserContext.WebError` reports uncaught exceptions from pages in this context.

## Previous: Wave 451 — Context FrameNavigated

### Wave 451
- [x] `IBrowserContext.FrameNavigated` fires when a frame in this context navigates.

## Previous: Wave 450 — Context FrameDetached

### Wave 450
- [x] `IBrowserContext.FrameDetached` fires when a frame is detached in this context.

## Previous: Wave 449 — Context FrameAttached

### Wave 449
- [x] `IBrowserContext.FrameAttached` fires when a frame is attached in this context.

## Previous: Wave 448 — Context PageLoad

### Wave 448
- [x] `IBrowserContext.PageLoad` fires when a page in this context finishes loading.

## Previous: Wave 447 — Context PageClose

### Wave 447
- [x] `IBrowserContext.PageClose` fires when a page in this context is closed.

## Previous: Wave 446 — Context Dialog

### Wave 446
- [x] `IBrowserContext.Dialog` forwards page dialogs in this context.

## Previous: Wave 445 — Context Download

### Wave 445
- [x] `IBrowserContext.Download` forwards page downloads in this context.

## Previous: Wave 444 — Browser Context event

### Wave 444
- [x] `IBrowser.Context` fires when `NewContextAsync` creates a context.

## Previous: Wave 443 — Route postDataText

### Wave 443
- [x] `IRoute.ContinueAsync` / `ResumeAsync` accept a UTF-8 `postDataText` body override.

## Previous: Wave 442 — ClearPageErrors

### Wave 442
- [x] `IPage.ClearPageErrorsAsync` clears stored page errors.

## Previous: Wave 441 — ClearConsoleMessages

### Wave 441
- [x] `IPage.ClearConsoleMessagesAsync` clears stored console messages.

## Previous: Wave 440 — ExecutablePath

### Wave 440
- [x] Browser `ExecutablePath` is the bundled browser executable path.

## Previous: Wave 439 — context Route times

### Wave 439
- [x] `IBrowserContext.RouteAsync(..., times)` applies a context route only the given number of times, then removes it.

## Previous: Wave 438 — Route times

### Wave 438
- [x] `IPage.RouteAsync(..., times)` applies a page route only the given number of times, then removes it.

## Previous: Wave 437 — StrictSelectors

### Wave 437
- [x] `BrowserContextOptions.StrictSelectors` throws when a single-target action matches more than one node.

## Previous: Wave 436 — EmulateVisionDeficiency

### Wave 436
- [x] `IPage.EmulateVisionDeficiencyAsync` emulates a vision deficiency on Chromium.

## Previous: Wave 435 — launch Channel

### Wave 435
- [x] `BrowserTypeLaunchOptions.Channel` launches a system Chrome or Edge binary.

## Previous: Wave 434 — launch Devtools

### Wave 434
- [x] `BrowserTypeLaunchOptions.Devtools` adds `--auto-open-devtools-for-tabs` when launching Chromium.

## Previous: Wave 433 — ClearCookies URL

### Wave 433
- [x] `IBrowserContext.ClearCookiesAsync(url)` deletes cookies that would be sent to that URL.

## Previous: Wave 432 — Focus scroll

### Wave 432
- [x] `IPage.FocusAsync(scroll: ActionScroll.None)` focuses without scrolling into view.

## Previous: Wave 431 — SetInputFiles scroll

### Wave 431
- [x] `IPage.SetInputFilesAsync(scroll: ActionScroll.None)` sets files without scrolling into view.

## Previous: Wave 430 — SelectOption scroll

### Wave 430
- [x] `IPage.SelectOptionAsync(scroll: ActionScroll.None)` selects options without scrolling into view.

## Previous: Wave 429 — Press scroll

### Wave 429
- [x] `IPage.PressAsync(scroll: ActionScroll.None)` presses a key without scrolling into view.

## Previous: Wave 428 — Type scroll

### Wave 428
- [x] `IPage.TypeAsync(scroll: ActionScroll.None)` types without scrolling into view.

## Previous: Wave 427 — ClearCookies path Regex

### Wave 427
- [x] `IBrowserContext.ClearCookiesAsync(name, domain, pathRegex)` deletes cookies whose path matches a regular expression.

## Previous: Wave 426 — ClearCookies domain Regex

### Wave 426
- [x] `IBrowserContext.ClearCookiesAsync(name, domainRegex)` deletes cookies whose domain matches a regular expression.

## Previous: Wave 425 — Route.Fetch maxRetries

### Wave 425
- [x] `IRoute.FetchAsync(maxRetries:)` retries after a connection reset.

## Previous: Wave 424 — launch Env

### Wave 424
- [x] `BrowserTypeLaunchOptions.Env` overlays extra environment variables on the browser process.

## Previous: Wave 423 — IgnoreDefaultArgs

### Wave 423
- [x] `BrowserTypeLaunchOptions.IgnoreDefaultArgs` launches Chromium without built-in default flags.

## Previous: Wave 422 — launch DownloadsPath

### Wave 422
- [x] `BrowserTypeLaunchOptions.DownloadsPath` is the default directory for accepted downloads.

## Previous: Wave 421 — launch Timeout

### Wave 421
- [x] `BrowserTypeLaunchOptions.Timeout` fails Chromium launch when the browser does not connect in time.

## Previous: Wave 420 — ChromiumSandbox

### Wave 420
- [x] `BrowserTypeLaunchOptions.ChromiumSandbox` toggles Chromium's `--no-sandbox` launch flag.

## Previous: Wave 419 — launch Args

### Wave 419
- [x] `BrowserTypeLaunchOptions.Args` are forwarded to the Chromium, Firefox, and WebKit processes.

## Previous: Wave 418 — Route.Fetch maxRedirects

### Wave 418
- [x] `IRoute.FetchAsync(maxRedirects:)` limits followed redirects and returns the redirect when 0.

## Previous: Wave 417 — recordHarUrl Regex

### Wave 417
- [x] `IBrowser.NewContextAsync(recordHarUrlRegex:)` filters HAR entries by regular expression.

## Previous: Wave 416 — SelectText scroll

### Wave 416
- [x] `IPage.SelectTextAsync(scroll: ActionScroll.None)` selects text without scrolling into view.

## Previous: Wave 415 — SetChecked scroll

### Wave 415
- [x] `IPage.SetCheckedAsync(scroll: ActionScroll.None)` skips scrolling the checkbox into view.

## Previous: Wave 414 — Fill scroll

### Wave 414
- [x] `IPage.FillAsync(scroll: ActionScroll.None)` fills without scrolling the field into view.

## Previous: Wave 413 — Uncheck scroll

### Wave 413
- [x] `IPage.UncheckAsync(scroll: ActionScroll.None)` skips scrolling the checkbox into view.

## Previous: Wave 412 — Check scroll

### Wave 412
- [x] `IPage.CheckAsync(scroll: ActionScroll.None)` skips scrolling the checkbox into view.

## Previous: Wave 411 — Tap scroll

### Wave 411
- [x] `IPage.TapAsync(scroll: ActionScroll.None)` skips scrolling the target into view.

## Previous: Wave 410 — Hover scroll

### Wave 410
- [x] `IPage.HoverAsync(scroll: ActionScroll.None)` skips scrolling the target into view.

## Previous: Wave 409 — DblClick scroll

### Wave 409
- [x] `IPage.DblClickAsync(scroll: ActionScroll.None)` skips scrolling the target into view.

## Previous: Wave 408 — Click scroll

### Wave 408
- [x] `IPage.ClickAsync(scroll: ActionScroll.None)` skips scrolling the target into view.

## Previous: Wave 407 — ClearCookies Regex

### Wave 407
- [x] `IBrowserContext.ClearCookiesAsync(Regex name)` deletes cookies whose names match.

## Previous: Wave 406 — IRoute.Fulfill json

### Wave 406
- [x] `IRoute.FulfillAsync(json:)` serializes the object as an application/json body.

## Previous: Wave 405 — HttpCredentials.Send

### Wave 405
- [x] `HttpCredentials.Send` (`Always`) sends a preemptive Basic Authorization header.

## Previous: Wave 404 — HttpCredentials.Origin

### Wave 404
- [x] `HttpCredentials.Origin` restricts Basic auth to that origin.

## Previous: Wave 403 — HAR content Attach

### Wave 403
- [x] `IBrowser.NewContextAsync(recordHarContent: HarContentPolicy.Attach)` writes response bodies as sidecar files.

## Previous: Wave 402 — context contrast

### Wave 402
- [x] `IBrowser.NewContextAsync(contrast)` emulates `prefers-contrast` on new pages.

## Previous: Wave 401 — context forcedColors

### Wave 401
- [x] `IBrowser.NewContextAsync(forcedColors)` emulates `forced-colors` on new pages.

## Previous: Wave 400 — context reducedMotion

### Wave 400
- [x] `IBrowser.NewContextAsync(reducedMotion)` emulates `prefers-reduced-motion` on new pages.

## Previous: Wave 399 — drag scroll

### Wave 399
- [x] `IPage.DragAndDropAsync(scroll: ActionScroll.None)` skips scrolling the source and target into view.

## Previous: Wave 398 — serviceWorkers

### Wave 398
- [x] `IBrowser.NewContextAsync(serviceWorkers: ServiceWorkerPolicy.Block)` rejects `navigator.serviceWorker.register`.

## Previous: Wave 397 — HAR Mode

### Wave 397
- [x] `IBrowser.NewContextAsync(recordHarMode: HarMode.Minimal)` omits response bodies from the HAR.

## Previous: Wave 396 — context baseURL

### Wave 396
- [x] `IBrowser.NewContextAsync(baseURL)` resolves relative `GoToAsync` URLs against the prefix.

## Previous: Wave 395 — async predicate Route

### Wave 395
- [x] `IPage.RouteAsync(Func<string, bool>, Func<IRoute, Task>)` registers an awaited predicate route handler.

## Previous: Wave 394 — APIRequest Proxy

### Wave 394
- [x] `IAPIRequest.NewContextAsync(proxy)` sends standalone HTTP requests through the proxy.

## Previous: Wave 393 — async Regex Route

### Wave 393
- [x] `IPage.RouteAsync(Regex, Func<IRoute, Task>)` registers an awaited regex route handler.

## Previous: Wave 392 — recordHarUrl

### Wave 392
- [x] `IBrowser.NewContextAsync(recordHarUrl)` records only request URLs matching the glob.

## Previous: Wave 391 — Restore IndexedDB

### Wave 391
- [x] `IBrowserContext.SetStorageStateAsync` restores IndexedDB from a storage-state snapshot.

## Previous: Wave 390 — StorageState IndexedDB

### Wave 390
- [x] `IBrowserContext.StorageStateAsync(indexedDB: true)` includes per-origin IndexedDB databases.

## Previous: Wave 389 — HAR url Regex

### Wave 389
- [x] `IPage.RouteFromHARAsync(har, url: Regex)` intercepts only URLs matching the expression.

## Previous: Wave 388 — HAR notFound

### Wave 388
- [x] `IPage.RouteFromHARAsync(notFound: Fallback)` continues unmatched requests to the network.

## Previous: Wave 387 — ControlOrMeta

### Wave 387
- [x] `IKeyboard.PressAsync("ControlOrMeta+…")` sends Control on Linux/Windows and Meta on macOS.

## Previous: Wave 386 — DragAndDrop Steps

### Wave 386
- [x] `IPage.DragAndDropAsync(steps)` interpolates the mouse path between source and target.

## Previous: Wave 385 — DragAndDrop Trial

### Wave 385
- [x] `IPage.DragAndDropAsync(trial)` runs actionability checks without dispatching the drag.

## Previous: Wave 384 — Cookie Partition Key

### Wave 384
- [x] `Cookie.PartitionKey` is sent to Storage.setCookies and returned by GetCookiesAsync.

## Previous: Wave 383 — SelectText Force

### Wave 383
- [x] `IPage.SelectTextAsync(force)` skips the visibility wait so hidden controls can be selected.

## Previous: Wave 382 — WaitUntil Commit

### Wave 382
- [x] `WaitUntilState.Commit` waits for the navigation commit lifecycle instead of load.

## Previous: Wave 381 — Page Aria Snapshot

### Wave 381
- [x] `IPage.AriaSnapshotAsync(selector)` returns a YAML accessibility snapshot of the matched element.

## Previous: Wave 380 — Frame Aria Snapshot

### Wave 380
- [x] `IFrame.AriaSnapshotAsync(selector)` returns a YAML accessibility snapshot of the matched element.

## Previous: Wave 379 — Element Aria Snapshot

### Wave 379
- [x] `IElementHandle.AriaSnapshotAsync` returns a YAML accessibility snapshot of the element.

## Previous: Wave 378 — Frame CDP Session

### Wave 378
- [x] `IBrowserContext.NewCDPSessionAsync(IFrame)` attaches a Chromium CDP session to the frame's page.

## Previous: Wave 377 — Browser Close Reason

### Wave 377
- [x] `IBrowser.CloseAsync(reason)` stores the close reason and surfaces it on later page errors.

## Previous: Wave 376 — Context Close Reason

### Wave 376
- [x] `IBrowserContext.CloseAsync(reason)` stores the close reason and surfaces it on later page errors.

## Previous: Wave 375 — Page Close Reason

### Wave 375
- [x] `IPage.CloseAsync(reason)` stores the close reason and surfaces it on later errors.

## Previous: Wave 374 — SetInputFiles Force

### Wave 374
- [x] `IPage.SetInputFilesAsync(force)` skips the visibility wait so hidden file inputs can be set.

## Previous: Wave 373 — Press Force

### Wave 373
- [x] `IPage.PressAsync(force)` skips the visibility wait so hidden controls can be pressed.

## Previous: Wave 372 — Type Force

### Wave 372
- [x] `IPage.TypeAsync(force)` skips the visibility wait so hidden inputs can be typed into.

## Previous: Wave 371 — Emulate Contrast

### Wave 371
- [x] `IPage.EmulateMediaAsync(contrast)` sets the prefers-contrast media feature.

## Previous: Wave 370 — SelectOption Force

### Wave 370
- [x] `IPage.SelectOptionAsync(force)` skips the visibility wait so hidden selects can be changed.

## Previous: Wave 369 — Fill Force

### Wave 369
- [x] `IPage.FillAsync(force)` skips the visibility wait so hidden inputs can be filled.

## Previous: Wave 368 — SetChecked Trial

### Wave 368
- [x] `IPage.SetCheckedAsync(trial)` runs actionability checks without changing the checked state.

## Previous: Wave 367 — Uncheck Trial

### Wave 367
- [x] `IPage.UncheckAsync(trial)` runs actionability checks without unchecking the box.

## Previous: Wave 366 — Check Trial

### Wave 366
- [x] `IPage.CheckAsync(trial)` runs actionability checks without checking the box.

## Previous: Wave 365 — Tap Trial

### Wave 365
- [x] `IPage.TapAsync(trial)` runs actionability checks without dispatching the tap.

## Previous: Wave 364 — Hover Trial

### Wave 364
- [x] `IPage.HoverAsync(trial)` runs actionability checks without moving the mouse.

## Previous: Wave 363 — DblClick Trial

### Wave 363
- [x] `IPage.DblClickAsync(trial)` runs actionability checks without dispatching the double click.

## Previous: Wave 362 — Click Trial

### Wave 362
- [x] `IPage.ClickAsync(trial)` runs actionability checks without dispatching the click.

## Previous: Wave 361 — GetByRole Selected

### Wave 361
- [x] `IPage.GetByRoleAsync(selected)` filters roles by aria-selected or native selected.

## Previous: Wave 360 — GetByRole Pressed

### Wave 360
- [x] `IPage.GetByRoleAsync(pressed)` filters roles by aria-pressed.

## Previous: Wave 359 — GetByRole Level

### Wave 359
- [x] `IPage.GetByRoleAsync(level)` filters heading roles by h1–h6 or aria-level.

## Previous: Wave 358 — GetByRole IncludeHidden

### Wave 358
- [x] `IPage.GetByRoleAsync(includeHidden)` skips hidden matches when `false`.

## Previous: Wave 357 — GetByRole Expanded

### Wave 357
- [x] `IPage.GetByRoleAsync(expanded)` filters roles by aria-expanded.

## Previous: Wave 356 — GetByRole Disabled

### Wave 356
- [x] `IPage.GetByRoleAsync(disabled)` filters roles by disabled vs enabled.

## Previous: Wave 355 — GetByRole Checked

### Wave 355
- [x] `IPage.GetByRoleAsync(checkedState)` filters checkbox and aria-checked roles.

## Previous: Wave 354 — Element Screenshot Style

### Wave 354
- [x] `IElementHandle.ScreenshotAsync(style)` injects a caller stylesheet for the capture.

## Previous: Wave 353 — Element Screenshot Caret

### Wave 353
- [x] `IElementHandle.ScreenshotAsync(caret)` hides the text caret when `"hide"`.

## Previous: Wave 352 — Element Screenshot Animations

### Wave 352
- [x] `IElementHandle.ScreenshotAsync(animations)` disables CSS animations when `"disabled"`.

## Previous: Wave 351 — Element Screenshot Scale

### Wave 351
- [x] `IElementHandle.ScreenshotAsync(scale)` captures CSS or device pixels.

## Previous: Wave 350 — Screenshot Style

### Wave 350
- [x] `IPage.ScreenshotAsync(style)` injects a caller stylesheet for the capture.

## Previous: Wave 349 — Screenshot Caret

### Wave 349
- [x] `IPage.ScreenshotAsync(caret)` hides the text caret when `"hide"`.

## Previous: Wave 348 — Screenshot Animations

### Wave 348
- [x] `IPage.ScreenshotAsync(animations)` disables CSS animations when `"disabled"`.

## Previous: Wave 347 — Screenshot Scale

### Wave 347
- [x] `IPage.ScreenshotAsync(scale)` captures CSS or device pixels.

## Previous: Wave 346 — PDF Outline

### Wave 346
- [x] `IPage.PdfAsync(outline)` embeds a document outline on Chromium.

## Previous: Wave 345 — PDF Tagged

### Wave 345
- [x] `IPage.PdfAsync(tagged)` generates a tagged accessible PDF on Chromium.

## Previous: Wave 344 — Browser Type

### Wave 344
- [x] `IBrowser.BrowserType` reports the launched engine (chromium, firefox, or webkit).

## Previous: Wave 343 — Clear Cookies Filter

### Wave 343
- [x] `IBrowserContext.ClearCookiesAsync(name, domain, path)` deletes matching cookies.

## Previous: Wave 342 — Page Requests

### Wave 342
- [x] `IPage.RequestsAsync` returns network requests recorded on the page.

## Previous: Wave 341 — Page Errors

### Wave 341
- [x] `IPage.PageErrorsAsync` returns uncaught page errors recorded on the page.

## Previous: Wave 340 — Console Messages

### Wave 340
- [x] `IPage.ConsoleMessagesAsync` returns console messages recorded on the page.

## Previous: Wave 339 — Console Worker

### Wave 339
- [x] `IConsoleMessage.Worker` returns the dedicated worker that logged the message.

## Previous: Wave 338 — Worker Console

### Wave 338
- [x] `IWorker.Console` reports console messages from a dedicated worker.

## Previous: Wave 337 — Frame GetByTestId

### Wave 337
- [x] `IFrame.GetByTestIdAsync` finds an element by data-testid in the frame.

## Previous: Wave 336 — Frame GetByTitle

### Wave 336
- [x] `IFrame.GetByTitleAsync` finds an element by title in the frame.

## Previous: Wave 335 — Frame GetByAltText

### Wave 335
- [x] `IFrame.GetByAltTextAsync` finds an image by alt text in the frame.

## Previous: Wave 334 — Frame GetByPlaceholder

### Wave 334
- [x] `IFrame.GetByPlaceholderAsync` finds an input by placeholder in the frame.

## Previous: Wave 333 — Frame GetByLabel

### Wave 333
- [x] `IFrame.GetByLabelAsync` finds a labeled control in the frame.

## Previous: Wave 332 — Frame GetByText

### Wave 332
- [x] `IFrame.GetByTextAsync` finds an element by text in the frame.

## Previous: Wave 331 — Frame GetByRole

### Wave 331
- [x] `IFrame.GetByRoleAsync` finds an element by ARIA role in the frame.

## Previous: Wave 330 — Frame FrameElement

### Wave 330
- [x] `IFrame.FrameElementAsync` returns the hosting iframe or frame element.

## Previous: Wave 329 — Response HttpVersion

### Wave 329
- [x] `IResponse.HttpVersionAsync` returns the protocol version (e.g. HTTP/1.1).

## Previous: Wave 328 — Element WaitForSelector

### Wave 328
- [x] `IElementHandle.WaitForSelectorAsync` waits for a descendant selector scoped to the element.

## Previous: Wave 327 — Frame DragAndDrop

### Wave 327
- [x] `IFrame.DragAndDropAsync` drags a source element onto a target in the frame.

## Previous: Wave 326 — Frame AddStyleTag

### Wave 326
- [x] `IFrame.AddStyleTagAsync` injects a STYLE or LINK tag in the frame and returns a handle.

## Previous: Wave 325 — Frame AddScriptTag

### Wave 325
- [x] `IFrame.AddScriptTagAsync` injects a SCRIPT tag in the frame and returns a handle.

## Previous: Wave 324 — Context SetStorageState

### Wave 324
- [x] `IBrowserContext.SetStorageStateAsync` restores cookies and origins from a snapshot.

## Previous: Wave 323 — Context IsClosed

### Wave 323
- [x] `IBrowserContext.IsClosed` is true after `CloseAsync`.

## Previous: Wave 322 — Context UnrouteWebSocket Predicate

### Wave 322
- [x] `IBrowserContext.UnrouteWebSocketAsync(Func<string, bool>)` removes a context WebSocket predicate route.

## Previous: Wave 321 — Context UnrouteWebSocket Regex

### Wave 321
- [x] `IBrowserContext.UnrouteWebSocketAsync(Regex)` removes a context WebSocket regex route.

## Previous: Wave 320 — Context RouteWebSocket Predicate

### Wave 320
- [x] `IBrowserContext.RouteWebSocketAsync(Func<string, bool>)` matches WebSocket URLs with a predicate.

## Previous: Wave 319 — Context RouteWebSocket Regex

### Wave 319
- [x] `IBrowserContext.RouteWebSocketAsync(Regex)` matches WebSocket URLs with a regular expression.

## Previous: Wave 318 — Route ContinueAsync

### Wave 318
- [x] `IRoute.ContinueAsync` aliases `ResumeAsync`.

## Previous: Wave 317 — JSHandle AsElement nested

### Wave 317
- [x] `IJSHandle.AsElement` wraps nested EvaluateHandle DOM nodes as element handles.

## Previous: Wave 316 — Context RunAndWaitForServiceWorker

### Wave 316
- [x] `IBrowserContext.RunAndWaitForServiceWorkerAsync` waits for a service worker registered by an action.

## Previous: Wave 315 — Page RouteWebSocket Predicate

### Wave 315
- [x] `IPage.RouteWebSocketAsync(Func<string, bool>)` matches WebSocket URLs with a predicate.

## Previous: Wave 314 — Page RouteWebSocket Regex

### Wave 314
- [x] `IPage.RouteWebSocketAsync(Regex)` matches WebSocket URLs with a regular expression.

## Previous: Wave 313 — Context RunAndWaitForRequestFailed

### Wave 313
- [x] `IBrowserContext.RunAndWaitForRequestFailedAsync` waits for a failed request from an action.

## Previous: Wave 312 — Context RunAndWaitForRequestFinished

### Wave 312
- [x] `IBrowserContext.RunAndWaitForRequestFinishedAsync` waits for a request finished by an action.

## Previous: Wave 311 — Context RunAndWaitForResponse

### Wave 311
- [x] `IBrowserContext.RunAndWaitForResponseAsync` waits for a response produced by an action.

## Previous: Wave 310 — Context RunAndWaitForRequest

### Wave 310
- [x] `IBrowserContext.RunAndWaitForRequestAsync` waits for a request issued by an action.

## Previous: Wave 309 — Frame RunAndWaitForNavigation

### Wave 309
- [x] `IFrame.RunAndWaitForNavigationAsync` waits for a frame navigation started by an action.

## Previous: Wave 308 — Context RunAndWaitForConsole

### Wave 308
- [x] `IBrowserContext.RunAndWaitForConsoleMessageAsync` waits for a console message from an action.

## Previous: Wave 307 — Context RunAndWaitForPage

### Wave 307
- [x] `IBrowserContext.RunAndWaitForPageAsync` waits for a page created by an action.

## Previous: Wave 306 — RunAndWaitForRequestFailed

### Wave 306
- [x] `IPage.RunAndWaitForRequestFailedAsync` waits for a failed request from an action.

## Previous: Wave 305 — RunAndWaitForRequestFinished

### Wave 305
- [x] `IPage.RunAndWaitForRequestFinishedAsync` waits for a request finished by an action.

## Previous: Wave 304 — Context WaitForConsole

### Wave 304
- [x] `IBrowserContext.WaitForConsoleMessageAsync` waits for a console message from any page.

## Previous: Wave 303 — Context Console

### Wave 303
- [x] `IBrowserContext.Console` forwards console messages from pages in the context.

## Previous: Wave 302 — RunAndWaitForDialog

### Wave 302
- [x] `IPage.RunAndWaitForDialogAsync` waits for a dialog opened by an action.

## Previous: Wave 301 — RunAndWaitForFileChooser

### Wave 301
- [x] `IPage.RunAndWaitForFileChooserAsync` waits for a file chooser opened by an action.

## Previous: Wave 300 — RunAndWaitForResponse

### Wave 300
- [x] `IPage.RunAndWaitForResponseAsync` waits for a response produced by an action.

## Previous: Wave 299 — RunAndWaitForRequest

### Wave 299
- [x] `IPage.RunAndWaitForRequestAsync` waits for a request issued by an action.

## Previous: Wave 298 — RunAndWaitForNavigation

### Wave 298
- [x] `IPage.RunAndWaitForNavigationAsync` waits for a navigation started by an action.

## Previous: Wave 297 — RunAndWaitForWebSocket

### Wave 297
- [x] `IPage.RunAndWaitForWebSocketAsync` waits for a WebSocket opened by an action.

## Previous: Wave 296 — RunAndWaitForWorker

### Wave 296
- [x] `IPage.RunAndWaitForWorkerAsync` waits for a worker created by an action.

## Previous: Wave 295 — RunAndWaitForConsole

### Wave 295
- [x] `IPage.RunAndWaitForConsoleMessageAsync` waits for a console message from an action.

## Previous: Wave 294 — RunAndWaitForPopup

### Wave 294
- [x] `IPage.RunAndWaitForPopupAsync` waits for a popup opened by an action.

## Previous: Wave 293 — RunAndWaitForDownload

### Wave 293
- [x] `IPage.RunAndWaitForDownloadAsync` waits for a download started by an action.

## Previous: Wave 292 — Console Timestamp

### Wave 292
- [x] `IConsoleMessage.Timestamp` is populated from the browser.

## Previous: Wave 291 — Console Args

### Wave 291
- [x] `IConsoleMessage.Args` is populated from the browser.

## Previous: Wave 290 — Download CancelAsync

### Wave 290
- [x] `IDownload.CancelAsync` cancels an in-progress download.

## Previous: Wave 289 — WK SetContent waitUntil

### Wave 289
- [x] WebKit `IPage.SetContentAsync` honors waitUntil.

## Previous: Wave 288 — WK SetContent timeout

### Wave 288
- [x] WebKit `IPage.SetContentAsync` honors timeout.

## Previous: Wave 287 — JSCoverage ScriptId

### Wave 287
- [x] `JSCoverageEntry.ScriptId` is populated from Chromium.

## Previous: Wave 286 — SetChecked force

### Wave 286
- [x] `IElementHandle.SetCheckedAsync` honors force.

## Previous: Wave 285 — Request ExistingResponse

### Wave 285
- [x] `IRequest.ExistingResponse` returns the already-received response.

## Previous: Wave 284 — Dialog Page

### Wave 284
- [x] `IDialog.Page` returns the page that opened the dialog.

## Previous: Wave 283 — ConsoleMessage Page

### Wave 283
- [x] `IConsoleMessage.Page` returns the page that produced the message.

## Previous: Wave 282 — Download Page

### Wave 282
- [x] `IDownload.Page` returns the page that started the download.

## Previous: Wave 281 — CSS coverage resetOnNavigation

### Wave 281
- [x] `ICoverage.StartCSSCoverageAsync` honors resetOnNavigation.

## Previous: Wave 280 — JS coverage reportAnonymousScripts

### Wave 280
- [x] `ICoverage.StartJSCoverageAsync` honors reportAnonymousScripts.

## Previous: Wave 279 — JS coverage resetOnNavigation

### Wave 279
- [x] `ICoverage.StartJSCoverageAsync` honors resetOnNavigation.

## Previous: Wave 278 — JSHandle JsonAsync

### Wave 278
- [x] `IJSHandle.JsonAsync` aliases JsonValueAsync.

## Previous: Wave 277 — Screenshot timeout

### Wave 277
- [x] `IPage.ScreenshotAsync` honors timeout.

## Previous: Wave 276 — Pdf preferCSSPageSize

### Wave 276
- [x] `IPage.PdfAsync` honors preferCSSPageSize.

## Previous: Wave 275 — Pdf header/footer templates

### Wave 275
- [x] `IPage.PdfAsync` honors headerTemplate and footerTemplate.

## Previous: Wave 274 — Pdf displayHeaderFooter

### Wave 274
- [x] `IPage.PdfAsync` honors displayHeaderFooter.

## Previous: Wave 273 — Pdf pageRanges

### Wave 273
- [x] `IPage.PdfAsync` honors pageRanges.

## Previous: Wave 272 — Pdf margin

### Wave 272
- [x] `IPage.PdfAsync` honors margin.

## Previous: Wave 271 — Pdf format

### Wave 271
- [x] `IPage.PdfAsync` honors format.

## Previous: Wave 270 — Pdf paper size

### Wave 270
- [x] `IPage.PdfAsync` honors width and height.

## Previous: Wave 269 — Pdf scale

### Wave 269
- [x] `IPage.PdfAsync` honors scale.

## Previous: Wave 268 — DragAndDrop force

### Wave 268
- [x] `IPage.DragAndDropAsync` honors force.

## Previous: Wave 267 — SelectOption params wait

### Wave 267
- [x] `IPage` / `IFrame.SelectOptionAsync` params overloads wait for the selector.

## Previous: Wave 266 — Focus attach timeout

### Wave 266
- [x] `IPage` / `IFrame.FocusAsync` waits for the selector and honors timeout.

## Previous: Wave 265 — SetChecked timeout

### Wave 265
- [x] `IPage` / `IFrame.SetCheckedAsync` honors timeout.

## Previous: Wave 264 — Uncheck timeout

### Wave 264
- [x] `IPage` / `IFrame.UncheckAsync` honors timeout.

## Previous: Wave 263 — Check timeout

### Wave 263
- [x] `IPage` / `IFrame.CheckAsync` honors timeout.

## Previous: Wave 262 — Tap timeout

### Wave 262
- [x] `IPage` / `IFrame.TapAsync` honors timeout.

## Previous: Wave 261 — Hover timeout

### Wave 261
- [x] `IPage` / `IFrame.HoverAsync` honors timeout.

## Previous: Wave 260 — DblClick timeout

### Wave 260
- [x] `IPage` / `IFrame.DblClickAsync` honors timeout.

## Previous: Wave 259 — Click timeout

### Wave 259
- [x] `IPage` / `IFrame.ClickAsync` honors timeout.

## Previous: Wave 258 — Type timeout

### Wave 258
- [x] `IPage` / `IFrame.TypeAsync` honors timeout.

## Previous: Wave 257 — Press timeout

### Wave 257
- [x] `IPage` / `IFrame.PressAsync` honors timeout.

## Previous: Wave 256 — Fill timeout

### Wave 256
- [x] `IPage` / `IFrame.FillAsync` honors timeout.

## Previous: Wave 255 — SetInputFiles timeout

### Wave 255
- [x] `IPage` / `IFrame.SetInputFilesAsync` honors timeout.

## Previous: Wave 254 — SelectOption timeout

### Wave 254
- [x] `IPage` / `IFrame.SelectOptionAsync` honors timeout.

## Previous: Wave 253 — DispatchEvent timeout

### Wave 253
- [x] `IPage` / `IFrame.DispatchEventAsync` honors timeout.

## Previous: Wave 252 — ScrollIntoView timeout

### Wave 252
- [x] `IPage` / `IFrame.ScrollIntoViewIfNeededAsync` honors timeout.

## Previous: Wave 251 — SelectText timeout

### Wave 251
- [x] `IPage` / `IFrame.SelectTextAsync` honors timeout.

## Previous: Wave 250 — InputValue timeout

### Wave 250
- [x] `IPage` / `IFrame.InputValueAsync` honors timeout.

## Previous: Wave 249 — IsEnabled timeout

### Wave 249
- [x] `IPage` / `IFrame.IsEnabledAsync` honors timeout.

## Previous: Wave 248 — IsEditable timeout

### Wave 248
- [x] `IPage` / `IFrame.IsEditableAsync` honors timeout.

## Previous: Wave 247 — IsDisabled timeout

### Wave 247
- [x] `IPage` / `IFrame.IsDisabledAsync` honors timeout.

## Previous: Wave 246 — IsChecked timeout

### Wave 246
- [x] `IPage` / `IFrame.IsCheckedAsync` honors timeout.

## Previous: Wave 245 — TextContent timeout

### Wave 245
- [x] `IPage` / `IFrame.TextContentAsync` honors timeout.

## Previous: Wave 244 — InnerHTML timeout

### Wave 244
- [x] `IPage` / `IFrame.InnerHTMLAsync` honors timeout.

## Previous: Wave 243 — InnerText timeout

### Wave 243
- [x] `IPage` / `IFrame.InnerTextAsync` honors timeout.

## Previous: Wave 242 — GetAttribute timeout

### Wave 242
- [x] `IPage` / `IFrame.GetAttributeAsync` honors timeout.

## Previous: Wave 241 — Tap modifiers

### Wave 241
- [x] `TapAsync` honors modifiers.

## Previous: Wave 240 — Hover modifiers

### Wave 240
- [x] `HoverAsync` honors modifiers.

## Previous: Wave 239 — DblClick modifiers

### Wave 239
- [x] `DblClickAsync` honors modifiers.

## Previous: Wave 238 — Click modifiers

### Wave 238
- [x] `ClickAsync` honors modifiers.

## Previous: Wave 237 — SetChecked position

### Wave 237
- [x] `SetCheckedAsync` honors Position.

## Previous: Wave 236 — Uncheck force

### Wave 236
- [x] `UncheckAsync` honors force (skips visibility wait).

## Previous: Wave 235 — Check force

### Wave 235
- [x] `CheckAsync` honors force (skips visibility wait).

## Previous: Wave 234 — Tap force

### Wave 234
- [x] `TapAsync` honors force (skips visibility wait).

## Previous: Wave 233 — Hover force

### Wave 233
- [x] `HoverAsync` honors force (skips visibility wait).

## Previous: Wave 232 — DblClick force

### Wave 232
- [x] `DblClickAsync` honors force (skips visibility wait).

## Previous: Wave 231 — Click force

### Wave 231
- [x] `ClickAsync` honors force (skips visibility wait).

## Previous: Wave 230 — JSHandle PropertiesAsync

### Wave 230
- [x] `IJSHandle.PropertiesAsync` aliases GetPropertiesAsync.

## Previous: Wave 229 — JSHandle PropertyAsync

### Wave 229
- [x] `IJSHandle.PropertyAsync` aliases GetPropertyAsync.

## Previous: Wave 228 — CookiesAsync

### Wave 228
- [x] `IBrowserContext.CookiesAsync` aliases GetCookiesAsync.

## Previous: Wave 227 — response JsonAsync T

### Wave 227
- [x] `IResponse.JsonAsync<T>` aliases GetJsonAsync<T>.

## Previous: Wave 226 — page Focus timeout

### Wave 226
- [x] `IPage` / `IFrame.FocusAsync` honors timeout.

## Previous: Wave 225 — Focus timeout

### Wave 225
- [x] `FocusAsync` waits for visible and honors timeout.

## Previous: Wave 224 — response HeaderValues

### Wave 224
- [x] `IResponse.HeaderValuesAsync` returns matching header values.

## Previous: Wave 223 — request HeaderValues

### Wave 223
- [x] `IRequest.HeaderValuesAsync` returns matching header values.

## Previous: Wave 222 — request ResponseAsync

### Wave 222
- [x] `IRequest.ResponseAsync` aliases GetResponseAsync.

## Previous: Wave 221 — Uncheck position

### Wave 221
- [x] `UncheckAsync` honors Position.

## Previous: Wave 220 — Check position

### Wave 220
- [x] `CheckAsync` honors Position.

## Previous: Wave 219 — SetInputFiles timeout

### Wave 219
- [x] `SetInputFilesAsync` waits for visible and honors timeout.

## Previous: Wave 218 — SelectOption timeout

### Wave 218
- [x] `SelectOptionAsync` waits for visible and honors timeout.

## Previous: Wave 217 — Tap timeout

### Wave 217
- [x] `TapAsync` waits for visible and honors timeout.

## Previous: Wave 216 — Hover timeout

### Wave 216
- [x] `HoverAsync` waits for visible and honors timeout.

## Previous: Wave 215 — DblClick timeout

### Wave 215
- [x] `DblClickAsync` waits for visible and honors timeout.

## Previous: Wave 214 — Click timeout

### Wave 214
- [x] `ClickAsync` waits for visible and honors timeout.

## Previous: Wave 213 — Press timeout

### Wave 213
- [x] `PressAsync` waits for visible and honors timeout.

## Previous: Wave 212 — Type timeout

### Wave 212
- [x] `TypeAsync` waits for visible and honors timeout.

## Previous: Wave 211 — Uncheck timeout

### Wave 211
- [x] `UncheckAsync` waits for visible and honors timeout.

## Previous: Wave 210 — Check timeout

### Wave 210
- [x] `CheckAsync` waits for visible and honors timeout.

## Previous: Wave 209 — WaitForDisconnected

### Wave 209
- [x] `IBrowser.WaitForDisconnectedAsync` waits for disconnect.

## Previous: Wave 208 — WaitForFrameAttached

### Wave 208
- [x] `IPage.WaitForFrameAttachedAsync` waits for an attached frame.

## Previous: Wave 207 — WaitForCrash

### Wave 207
- [x] `IPage.WaitForCrashAsync` waits for the Crash event.

## Previous: Wave 206 — context WaitForRequestFailed

### Wave 206
- [x] `IBrowserContext.WaitForRequestFailedAsync` matches failed requests.

## Previous: Wave 205 — context WaitForRequestFinished

### Wave 205
- [x] `IBrowserContext.WaitForRequestFinishedAsync` matches finished requests.

## Previous: Wave 204 — FormData float

### Wave 204
- [x] `IFormData.Set` / `Append(float)`.

## Previous: Wave 203 — request SizesAsync

### Wave 203
- [x] `IRequest.SizesAsync` aliases GetSizesAsync.

## Previous: Wave 202 — Tap Position

### Wave 202
- [x] `TapAsync` honors Position.

## Previous: Wave 201 — Hover Position

### Wave 201
- [x] `HoverAsync` honors Position.

## Previous: Wave 200 — SetChecked timeout

### Wave 200
- [x] `SetCheckedAsync` waits for visible and honors timeout.

## Previous: Wave 199 — Fill timeout

### Wave 199
- [x] `FillAsync` waits for visible and honors timeout.

## Previous: Wave 198 — Screenshot timeout

### Wave 198
- [x] `IElementHandle.ScreenshotAsync` waits for visible and honors timeout.

## Previous: Wave 197 — DispatchEvent timeout

### Wave 197
- [x] `DispatchEventAsync` honors timeout.

## Previous: Wave 196 — response FinishedAsync

### Wave 196
- [x] `IResponse.FinishedAsync` aliases GetFinishedAsync.

## Previous: Wave 195 — response BodyAsync

### Wave 195
- [x] `IResponse.BodyAsync` aliases GetBodyAsync.

## Previous: Wave 194 — response TextAsync

### Wave 194
- [x] `IResponse.TextAsync` aliases GetTextAsync.

## Previous: Wave 193 — response JsonAsync

### Wave 193
- [x] `IResponse.JsonAsync` aliases GetJsonAsync.

## Previous: Wave 192 — context WaitForResponse

### Wave 192
- [x] `IBrowserContext.WaitForResponseAsync` matches responses.

## Previous: Wave 191 — context WaitForRequest

### Wave 191
- [x] `IBrowserContext.WaitForRequestAsync` matches requests.

## Previous: Wave 190 — WaitForFrameDetached

### Wave 190
- [x] `IPage.WaitForFrameDetachedAsync` waits for a detached frame.

## Previous: Wave 189 — WaitForFrameNavigated

### Wave 189
- [x] `IPage.WaitForFrameNavigatedAsync` waits for frame navigation.

## Previous: Wave 188 — WaitForPageError

### Wave 188
- [x] `IPage.WaitForPageErrorAsync` waits for uncaught exceptions.

## Previous: Wave 187 — WaitForDOMContentLoaded

### Wave 187
- [x] `IPage.WaitForDOMContentLoadedAsync` waits for DOMContentLoaded.

## Previous: Wave 186 — WaitForLoad

### Wave 186
- [x] `IPage.WaitForLoadAsync` waits for the next load.

## Previous: Wave 185 — click Position

### Wave 185
- [x] `ClickAsync` honors Position.

## Previous: Wave 184 — video DeleteAsync

### Wave 184
- [x] `IVideo.DeleteAsync` removes the recorded file.

## Previous: Wave 183 — video SaveAsAsync

### Wave 183
- [x] `IVideo.SaveAsAsync` copies the recorded file.

## Previous: Wave 182 — video PathAsync

### Wave 182
- [x] `IVideo.PathAsync` aliases GetPathAsync.

## Previous: Wave 181 — PostDataJSON

### Wave 181
- [x] `IRequest.PostDataJSON` aliases GetPayloadAsJson.

## Previous: Wave 180 — FormData decimal

### Wave 180
- [x] `IFormData.Set` / `Append` accept decimal.

## Previous: Wave 179 — InputValue timeout

### Wave 179
- [x] `InputValueAsync` honors timeout.

## Previous: Wave 178 — SelectText timeout

### Wave 178
- [x] `SelectTextAsync` honors timeout.

## Previous: Wave 177 — WaitForRequestFailed

### Wave 177
- [x] `IPage.WaitForRequestFailedAsync` matches failed requests.

## Previous: Wave 176 — WaitForRequestFinished

### Wave 176
- [x] `IPage.WaitForRequestFinishedAsync` matches finished requests.

## Previous: Wave 175 — ScrollIntoViewIfNeeded timeout

### Wave 175
- [x] `ScrollIntoViewIfNeededAsync` honors timeout.

## Previous: Wave 174 — WaitForFileChooser predicate

### Wave 174
- [x] `IPage.WaitForFileChooserAsync` accepts a predicate.

## Previous: Wave 173 — WaitForDownload predicate

### Wave 173
- [x] `IPage.WaitForDownloadAsync` accepts a predicate.

## Previous: Wave 172 — WaitForWebSocket regex

### Wave 172
- [x] `IPage.WaitForWebSocketAsync(Regex)` matches a URL regex.

## Previous: Wave 171 — SetDefaultTimeout methods

### Wave 171
- [x] `SetDefaultTimeout` / `SetDefaultNavigationTimeout` set the properties.

## Previous: Wave 170 — page AddInitScript arg

### Wave 170
- [x] `IPage.AddInitScriptAsync` accepts an evaluation argument.

## Previous: Wave 169 — WebSocket WaitForEvent

### Wave 169
- [x] `IWebSocket.WaitForEventAsync` waits for Close and frames.

## Previous: Wave 168 — FrameByUrl overloads

### Wave 168
- [x] `IPage.FrameByUrl` string, regex, and predicate overloads.

## Previous: Wave 167 — page Frame by name

### Wave 167
- [x] `IPage.Frame(string)` finds a frame by name.

## Previous: Wave 166 — API request Put/Patch/Delete dataBytes

### Wave 166
- [x] `IAPIRequestContext` Put/Patch/Delete dataBytes send a raw request body.

## Previous: Wave 165 — mouse wheel

### Wave 165
- [x] `IMouse.WheelAsync` dispatches a wheel event.

## Previous: Wave 164 — frame InputValue

### Wave 164
- [x] `IFrame.InputValueAsync` reads a selector's input value.

## Previous: Wave 163 — page InputValue

### Wave 163
- [x] `IPage.InputValueAsync` reads a selector's input value.

## Previous: Wave 162 — element InputValue

### Wave 162
- [x] `IElementHandle.InputValueAsync` reads input, textarea, and select values.

## Previous: Wave 161 — WaitForWebSocket URL glob

### Wave 161
- [x] `IPage.WaitForWebSocketAsync(string)` matches a URL glob.

## Previous: Wave 160 — form double fields

### Wave 160
- [x] `IFormData.Set` / `Append` accept `double`.

## Previous: Wave 159 — form long fields

### Wave 159
- [x] `IFormData.Set` / `Append` accept `long`.

## Previous: Wave 158 — API request dataBytes

### Wave 158
- [x] `IAPIRequestContext` dataBytes sends a raw request body.

## Previous: Wave 157 — API response JsonAsync T

### Wave 157
- [x] `IAPIResponse.JsonAsync<T>` deserializes the body.

## Previous: Wave 156 — standalone API request httpCredentials

### Wave 156
- [x] `Playwright.APIRequest.NewContextAsync(httpCredentials)` sends HTTP Basic auth.

## Previous: Wave 155 — UnrouteAll WebSocket routes

### Wave 155
- [x] `UnrouteAllAsync` also removes page and context WebSocket routes.

## Previous: Wave 154 — context UnrouteWebSocket

### Wave 154
- [x] `IBrowserContext.UnrouteWebSocketAsync` removes a context WebSocket route.

## Previous: Wave 153 — page UnrouteWebSocket

### Wave 153
- [x] `IPage.UnrouteWebSocketAsync` removes a page WebSocket route.

## Previous: Wave 152 — WebSocket route protocols

### Wave 152
- [x] `IWebSocketRoute.Protocols` exposes constructor subprotocols.

## Previous: Wave 151 — standalone API request storageState

### Wave 151
- [x] `Playwright.APIRequest.NewContextAsync(storageState / storageStatePath)` sends cookies.

## Previous: Wave 150 — standalone API request maxRedirects

### Wave 150
- [x] `Playwright.APIRequest.NewContextAsync(maxRedirects)` is the default redirect limit.

## Previous: Wave 149 — standalone API request failOnStatusCode

### Wave 149
- [x] `Playwright.APIRequest.NewContextAsync(failOnStatusCode)` throws on non-2xx.

## Previous: Wave 148 — standalone API request timeout

### Wave 148
- [x] `Playwright.APIRequest.NewContextAsync(timeout)` is the default request timeout.

## Previous: Wave 147 — standalone API request userAgent

### Wave 147
- [x] `Playwright.APIRequest.NewContextAsync(userAgent)` sets User-Agent.

## Previous: Wave 146 — standalone API request baseURL

### Wave 146
- [x] `Playwright.APIRequest.NewContextAsync(baseURL)` resolves relative URLs.

## Previous: Wave 145 — standalone API request

### Wave 145
- [x] `Playwright.APIRequest.NewContextAsync` creates a no-browser HTTP client.

## Previous: Wave 144 — API request maxRetries

### Wave 144
- [x] `IAPIRequestContext` maxRetries retries connection resets.

## Previous: Wave 143 — API request query params

### Wave 143
- [x] `IAPIRequestContext` queryParams append to the request URL.

## Previous: Wave 142 — API request multipart parameter

### Wave 142
- [x] `IAPIRequestContext` `multipart` always sends multipart/form-data.

## Previous: Wave 141 — API request form typed fields

### Wave 141
- [x] `IFormData.Set` / `Append` accept `bool` and `int`.

## Previous: Wave 140 — API request form Append

### Wave 140
- [x] `IFormData.Append` keeps duplicate field names; Set replaces them.

## Previous: Wave 139 — API request multipart

### Wave 139
- [x] `IFormData.Set(FilePayload)` sends multipart/form-data.

## Previous: Wave 138 — API request form

### Wave 138
- [x] `IAPIRequestContext.CreateFormData` sends urlencoded form bodies.

## Previous: Wave 137 — API request JSON

### Wave 137
- [x] `IAPIRequestContext` json serializes as application/json.

## Previous: Wave 136 — API request dispose

### Wave 136
- [x] `IAPIRequestContext` / `IAPIResponse` implement DisposeAsync.

## Previous: Wave 135 — API request ignoreHTTPSErrors

### Wave 135
- [x] `IAPIRequestContext` ignoreHTTPSErrors accepts untrusted TLS certs.

## Previous: Wave 134 — API request maxRedirects

### Wave 134
- [x] `IAPIRequestContext` maxRedirects limits redirect following.

## Previous: Wave 133 — API request timeout

### Wave 133
- [x] `IAPIRequestContext` timeout throws after the given milliseconds.

## Previous: Wave 132 — API request extra headers

### Wave 132
- [x] `IAPIRequestContext` sends context extra HTTP headers; per-request headers win.

## Previous: Wave 131 — API request failOnStatusCode

### Wave 131
- [x] `IAPIRequestContext` failOnStatusCode throws on non-2xx responses.

## Previous: Wave 130 — API request storage state

### Wave 130
- [x] `IAPIRequestContext.StorageStateAsync` and `IAPIResponse.HeadersArray`.

## Previous: Wave 129 — API request verbs

### Wave 129
- [x] `IAPIRequestContext` Head/Put/Patch/Delete plus per-request headers.

## Previous: Wave 128 — API request

### Wave 128
- [x] `IPage` / `IBrowserContext.APIRequest` GET/POST/fetch with context cookies.

## Previous: Wave 127 — WebSocket ConnectToServer

### Wave 127
- [x] `IWebSocketRoute.ConnectToServer` bridges a routed socket to the real server.

## Previous: Wave 126 — route WebSocket

### Wave 126
- [x] `IPage` / `IBrowserContext.RouteWebSocketAsync` mocks matching page WebSockets.

## Previous: Wave 125 — route from HAR

### Wave 125
- [x] `IPage` / `IBrowserContext.RouteFromHARAsync` fulfills from a HAR 1.2 file.

## Previous: Wave 124 — request service worker

### Wave 124
- [x] `IRequest.ServiceWorker` for Chromium service-worker-issued requests.

## Previous: Wave 123 — response from service worker

### Wave 123
- [x] `IResponse.FromServiceWorker` from the Chromium `fromServiceWorker` flag.

## Previous: Wave 122 — page wait helpers

### Wave 122
- [x] `IPage.WaitForPopupAsync` / `WaitForDialogAsync` / `WaitForWorkerAsync` / `WaitForWebSocketAsync`.

## Previous: Wave 121 — context service workers

### Wave 121
- [x] `IBrowserContext.ServiceWorkers` / `ServiceWorker` / `WaitForServiceWorkerAsync` on Chromium.

## Previous: Wave 120 — clock pause and resume

### Wave 120
- [x] `IClock.PauseAtAsync` / `ResumeAsync` / `SetSystemTimeAsync` on Chromium and WebKit.

## Previous: Wave 119 — clock install

### Wave 119
- [x] `IClock.InstallAsync` / `FastForwardAsync` / `RunForAsync` on Chromium and WebKit.

## Previous: Wave 118 — clock fixed time

### Wave 118
- [x] `IClock.SetFixedTimeAsync` on context and page (Chromium and WebKit).

## Previous: Wave 117 — CDP session

### Wave 117
- [x] `ICDPSession` via `IPage`/`IBrowserContext.NewCDPSessionAsync` and `IBrowser.NewBrowserCDPSessionAsync` (Chromium).

## Previous: Wave 116 — wait for console and page

### Wave 116
- [x] `IPage.WaitForConsoleMessageAsync` and `IBrowserContext.WaitForPageAsync`.

## Previous: Wave 115 — request GC

### Wave 115
- [x] `IPage.RequestGCAsync` on Chromium and WebKit.

## Previous: Wave 114 — emulate media features

### Wave 114
- [x] `IPage.EmulateMediaAsync(ReducedMotion?, ForcedColors?)` on Chromium and WebKit.

## Previous: Wave 113 — response server address

### Wave 113
- [x] `IResponse.ServerAddrAsync` / `SecurityDetailsAsync` on Chromium and WebKit.

## Previous: Wave 112 — tracing

### Wave 112
- [x] `IBrowserContext.Tracing` writes a Chromium performance trace; `WaitForCloseAsync` on the context.

## Previous: Wave 111 — coverage

### Wave 111
- [x] `IPage.Coverage` JS/CSS coverage on Chromium.

## Previous: Wave 110 — video recording

### Wave 110
- [x] `recordVideoDir` / `IPage.Video` write an MP4 via Chromium screencast + ffmpeg.

## Previous: Wave 109 — HTTP challenge auth

### Wave 109
- [x] Chromium answers server Basic/Digest challenges via `Fetch.authRequired`.

## Previous: Wave 108 — Route.Fallback

### Wave 108
- [x] `IRoute.FallbackAsync` chains to the next matching handler or the network.

## Previous: Wave 107 — Route.Fetch

### Wave 107
- [x] `IRoute.FetchAsync` / `FulfillAsync(RouteFetchResult)` and async `RouteAsync` handlers.

## Previous: Wave 106 — UnrouteAll

### Wave 106
- [x] `IPage.UnrouteAllAsync` / `IBrowserContext.UnrouteAllAsync` on Chromium and WebKit.

## Previous: Wave 105 — WaitForClose

### Wave 105
- [x] `IPage.WaitForCloseAsync` on Chromium and WebKit.

## Previous: Wave 104 — AllHeaders

### Wave 104
- [x] `IRequest` / `IResponse` `AllHeadersAsync`, `HeaderValueAsync`, and `HeadersArrayAsync`.

## Previous: Wave 103 — SetHttpCredentials

### Wave 103
- [x] `IBrowserContext.SetHttpCredentialsAsync` on Chromium and WebKit.

## Previous: Wave 102 — HAR recording

### Wave 102
- [x] `recordHarPath` / `recordHarOmitContent` write a HAR 1.2 file on context close.

## Previous: Wave 101 — SetChecked

### Wave 101
- [x] `SetCheckedAsync` on page, frame, and element handle.

## Previous: Wave 100 — WaitForElementState

### Wave 100
- [x] `IElementHandle.WaitForElementStateAsync` on Chromium and WebKit.

## Previous: Wave 99 — IElementHandle Screenshot

### Wave 99
- [x] `IElementHandle.ScreenshotAsync` on Chromium and WebKit.

## Previous: Wave 98 — SelectText

### Wave 98
- [x] `SelectTextAsync` on page, frame, and element handle.

## Previous: Wave 97 — ScrollIntoViewIfNeeded

### Wave 97
- [x] `ScrollIntoViewIfNeededAsync` on page, frame, and element handle.

## Previous: Wave 96 — DispatchEvent

### Wave 96
- [x] `DispatchEventAsync` on page, frame, and element handle.

## Previous: Wave 95 — IElementHandle ContentFrame / OwnerFrame

### Wave 95
- [x] `IElementHandle.ContentFrameAsync` / `OwnerFrameAsync` on Chromium and WebKit.

## Previous: Wave 94 — EvalOnSelector

### Wave 94
- [x] `EvalOnSelectorAsync` / `EvalOnSelectorAllAsync` on page, frame, and element handle.

## Previous: Wave 93 — IElementHandle QuerySelector

### Wave 93
- [x] `IElementHandle.QuerySelectorAsync` / `QuerySelectorAllAsync` on Chromium and WebKit.

## Previous: Wave 92 — QuerySelectorAll

### Wave 92
- [x] `IPage.QuerySelectorAllAsync` / `IFrame.QuerySelectorAllAsync` on Chromium and WebKit.

## Previous: Wave 91 — AddScriptTag / AddStyleTag handles

### Wave 91
- [x] `AddScriptTagAsync` / `AddStyleTagAsync` return the injected element handle on Chromium and WebKit.

## Previous: Wave 90 — IRequest.GetSizesAsync

### Wave 90
- [x] `IRequest.GetSizesAsync` on Chromium and WebKit.

## Previous: Wave 89 — IBrowserContext network events

### Wave 89
- [x] `IBrowserContext.Request` / `Response` / `RequestFailed` / `RequestFinished` and `WaitForEventAsync` on Chromium and WebKit.

## Previous: Wave 88 — IBrowserContext.Close

### Wave 88
- [x] `IBrowserContext.Close` and `WaitForEventAsync(BrowserContextEvent.Close)` on Chromium and WebKit.

## Previous: Wave 87 — IBrowserContext.Page

### Wave 87
- [x] `IBrowserContext.Page` and `WaitForEventAsync(BrowserContextEvent.Page)` on Chromium and WebKit.

## Previous: Wave 86 — WK JPEG screenshot

### Wave 86
- [x] WebKit `ScreenshotAsync(type: Jpeg)` re-encodes `Page.snapshotRect` PNG.

## Previous: Wave 85 — Proxy credentials

### Wave 85
- [x] Context proxy username/password on Chromium (`Fetch.authRequired`) and WebKit (userinfo in proxy URL).

## Previous: Wave 84 — NewContext proxy

### Wave 84
- [x] Context-level `proxy` on Chromium (`Target.createBrowserContext`) and WebKit (`Playwright.createContext`).

## Previous: Wave 83 — IPage.Crash

### Wave 83
- [x] `IPage.Crash` and `WaitForEventAsync(PageEvent.Crash)` on Chromium and WebKit.

## Previous: Wave 82 — IPage.WebSocket

### Wave 82
- [x] `IPage.WebSocket` and frame events on Chromium and WebKit.

## Previous: Wave 81 — IPage.DragAndDrop

### Wave 81
- [x] `IPage.DragAndDropAsync` on Chromium and WebKit.

## Previous: Wave 80 — IWorker.EvaluateHandle

### Wave 80
- [x] `IWorker.EvaluateHandleAsync` on Chromium (WebKit already evaluates).

## Previous: Wave 79 — IPage.Accessibility

### Wave 79
- [x] `IPage.Accessibility.SnapshotAsync` on Chromium and WebKit.

## Previous: Wave 78 — IPage.Workers

### Wave 78
- [x] `IPage.Worker` / `Workers` and `IWorker.EvaluateAsync` on Chromium and WebKit.

## Previous: Wave 77 — IFrame.WaitForFunction

### Wave 77
- [x] `IFrame.WaitForFunctionAsync` / `WaitForTimeoutAsync` on Chromium and WebKit.

## Previous: Wave 76 — IFrame.WaitForLoadState

### Wave 76
- [x] `IFrame.WaitForLoadStateAsync` on Chromium and WebKit.

## Previous: Wave 75 — IFrame.WaitForSelector

### Wave 75
- [x] `IFrame.WaitForSelectorAsync` on Chromium and WebKit.

## Previous: Wave 74 — IFrame.WaitForURL

### Wave 74
- [x] `IFrame.WaitForURLAsync` on Chromium and WebKit.

## Previous: Wave 73 — IFrame.WaitForNavigation

### Wave 73
- [x] `IFrame.WaitForNavigationAsync` on Chromium and WebKit.

## Previous: Wave 72 — WaitForEvent

### Wave 72
- [x] `IPage.WaitForEventAsync` on Chromium and WebKit.

## Previous: Wave 71 — FileChooser

### Wave 71
- [x] `IPage.FileChooser` / `WaitForFileChooserAsync` on Chromium and WebKit.

## Previous: Wave 70 — BringToFront

### Wave 70
- [x] `IPage.BringToFrontAsync` activates the page tab (CR + WK).

## Previous: Wave 69 — context ExposeBinding / ExposeFunction

### Wave 69
- [x] `IBrowserContext.ExposeBindingAsync` / `ExposeFunctionAsync` on Chromium and WebKit.

## Previous: Wave 68 — WaitForNavigation

### Wave 68
- [x] `IPage.WaitForNavigationAsync` waits for a future main-frame navigation (CR + WK).

## Previous: Wave 67 — runtime context overrides

### Wave 67
- [x] `SetGeolocationAsync` / `SetOfflineAsync` / `GrantPermissionsAsync` / `ClearPermissionsAsync` on Chromium and WebKit.

## Previous: Wave 66 — storage state

### Wave 66
- [x] `IBrowserContext.StorageStateAsync` exports cookies and localStorage.
- [x] `NewContextAsync(storageState / storageStatePath)` restores them (CR + WK).

## Previous: Wave 65 — context cookies

### Wave 65
- [x] `IBrowserContext.AddCookiesAsync` / `GetCookiesAsync` / `ClearCookiesAsync` on Chromium and WebKit.

## Previous: Wave 64 — page history navigation

### Wave 64
- [x] `ReloadAsync` / `GoBackAsync` / `GoForwardAsync` on Chromium and WebKit.

## Previous: Wave 63 — IPage.Download

### Wave 63
- [x] `IPage.Download` / `WaitForDownloadAsync` and `IDownload` on Chromium and WebKit.

## Previous: Wave 62 — NewContext acceptDownloads

### Wave 62
- [x] `NewContextAsync` applies acceptDownloads allow / deny (CR + WK).

## Previous: Wave 61 — NewContext screenSize

### Wave 61
- [x] `NewContextAsync` applies screenSize to `window.screen` (CR + WK).

## Previous: Wave 60 — NewContext HTTP credentials

### Wave 60
- [x] `NewContextAsync` applies httpCredentials as HTTP Basic auth (CR + WK).

## Previous: Wave 59 — NewContext deviceScaleFactor and isMobile

### Wave 59
- [x] `NewContextAsync` applies deviceScaleFactor and isMobile to new pages (CR + WK).

## Previous: Wave 58 — NewContext JavaScript and HTTPS errors

### Wave 58
- [x] `NewContextAsync` applies javaScriptEnabled and ignoreHTTPSErrors to new pages (CR + WK).

## Previous: Wave 57 — NewContext geolocation and permissions

### Wave 57
- [x] `NewContextAsync` applies geolocation and permissions to new pages (CR + WK).

## Previous: Wave 56 — ExposeBinding handle mode

### Wave 56
- [x] `ExposeBindingAsync(name, Func<BindingSource, IJSHandle, object>)` on Chromium and WebKit.
- [x] Page-side handle installer (`__pw_install_binding_handle__`) plus per-seq handle map.

## Previous: Wave 55 — WebKit child-frame GoTo

### Wave 55
- [x] WebKit `IFrame.GoToAsync` on child frames via `Playwright.navigate` + frame readyState.

## Previous: Wave 54 — WebKit child-frame worlds

### Wave 54
- [x] WebKit per-frame execution contexts for evaluate / query / SetContent / ElementQuery actions.

## Previous: Wave 53 — context hasTouch and bypassCSP

### Wave 53
- [x] `NewContextAsync` applies hasTouch and bypassCSP to new pages (CR + WK).

## Previous: Wave 52 — context timezone, locale, offline, color scheme

### Wave 52
- [x] `NewContextAsync` applies timezone, locale, offline, and color scheme to new pages (CR + WK).

## Previous: Wave 51 — context viewport and user-agent

### Wave 51
- [x] `NewContextAsync` applies viewport, user-agent, and extra headers to new pages (CR + WK).

## Previous: Wave 50 — GoTo response + frame EvaluateHandle

### Wave 50
- [x] Chromium `IFrame.EvaluateHandleAsync` in the frame's own world.
- [x] WebKit main-frame `EvaluateHandleAsync`.
- [x] `GoToAsync` passes waitUntil / timeout / referer and returns the document `IResponse`.

## Previous: Wave 49 — Request.Timing

### Wave 49
- [x] Chromium and WebKit `IRequest.Timing` from wallTime / ResourceTiming / loadingFinished.

## Previous: Wave 48 — browser Contexts / Disconnected / NewPage

### Wave 48
- [x] `IBrowser.Contexts` identity-stable on Chromium and WebKit.
- [x] `IBrowser.Disconnected` fires once on close.
- [x] `IBrowser.NewPageAsync` creates an implicit context + page.
- [x] `NewContextAsync(BrowserContextOptions)` uses the default context path.

## Previous: Wave 47 — frame query / actions / goto

### Wave 47
- [x] Chromium `IFrame.QuerySelectorAsync` plus ElementQuery actions (click/fill/focus/…).
- [x] Chromium `IFrame.GoToAsync` / `SetContentAsync` wait on that frame's lifecycle.
- [x] WebKit main-frame QuerySelector / actions / GoTo / SetContent (child frames still throw).

## Previous: Wave 46 — frame evaluate

### Wave 46
- [x] Chromium `IFrame.EvaluateAsync` / `TitleAsync` / `ContentAsync` run in the frame's own execution context.
- [x] WebKit main-frame `TitleAsync` / `ContentAsync` (child-frame worlds still page-delegated).

## Previous: Wave 45 — WK screenshot clip + omitBackground

### Wave 45 (this branch)
- [x] WebKit `ScreenshotAsync` clip rect via `Page.snapshotRect`.
- [x] WebKit `omitBackground` via `Page.setDefaultBackgroundColorOverride`.
- [x] JPEG still needs a re-encode step (no encoder in the library).

## Previous: Wave 44 — ExposeBinding + WK Route

Local branch → test → merge to `main` (no per-wave PRs). Do not start shadow DOM, `ILocator`, Windows fd, or Chromium 1228 pin.

### Wave 44 (this branch)
- [x] `ExposeBindingAsync` (no-handle) on Chromium and WebKit, delegated to `ExposeFunctionAsync`.
- [x] WebKit `RouteAsync` / `UnrouteAsync` on page and context via `Network.setInterceptionEnabled`.

### Wave 43
- [x] Chromium `IPage.FrameAttached` / `FrameDetached` / `FrameNavigated` (wired from `CRFrameManager`).
- [x] WebKit `MainFrame` / `Frames` / child+parent via `WKFrame` + `WKFrameAdapter`.
- [x] WebKit `Request.Frame` / `Response.Frame`.
- [x] `FrameByUrl` on Chromium and WebKit.
- [ ] Chromium 1228 CI confirm — still local-CDN / later.
- [ ] Windows fd plumbing — still later.
- [ ] Shadow-DOM piercing, layout engines, `ILocator` restore — backlog.

### Wave 42
- [x] `ExposeFunctionAsync` argument overloads (`Action<T>`, `Func<T,TResult>`, 2–4 args) on Chromium and WebKit.
- [x] `IPage.Console` / `PageError` on Chromium (`Runtime.consoleAPICalled` / `exceptionThrown`) and WebKit (`Console.messageAdded`).
- [x] `OpenerAsync` plus popup adapter identity via context page cache (Chromium) / `openerId` (WebKit).
- [x] Keyboard / Mouse / Touchscreen setters.
- [x] Chromium `Frames` / `MainFrame` child+parent links and `Request`/`Response.Frame`.

### Wave 41
- [x] `IResponse` body APIs: `GetBodyAsync` / `GetTextAsync` / `GetJsonAsync` / `GetFinishedAsync` on Chromium and WebKit.
- [x] `IRequest.PostDataBuffer`, `RedirectedFrom` / `RedirectedTo`, `GetPayloadAsJson` (CR / WK / FF adapters).
- [x] `IJSHandle.GetPropertyAsync` / `GetPropertiesAsync` / `EvaluateHandleAsync` via `returnByValue: false`.
- [x] Chromium `EmulateMediaAsync(Media.Print)` + screenshot `omitBackground` + `Route.FulfillAsync(path:)`.

### Waves 37–40
- [x] Path overloads: `AddInitScriptAsync` / `AddScriptTagAsync` / `AddStyleTagAsync` read files; `ScreenshotAsync` / `PdfAsync` write bytes.
- [x] `IBrowserContext` chrome: `Browser` getter, `DefaultTimeout` / `DefaultNavigationTimeout`, `SetExtraHttpHeadersAsync`, `AddInitScriptAsync` (inline + path) applied to current and future pages.
- [x] Chromium `RouteAsync` regex / predicate + `UnrouteAsync` (page and context). Page routes take precedence over context routes.
- [x] `IJSHandle.JsonValueAsync` + `EvaluateAsync` with argument on CR / WK / FF handles.

### Waves 34–36
- [x] Page-level `DblClickAsync` / `SelectOptionAsync` / `TapAsync` / `SetInputFilesAsync` via `ElementQuery`.
- [x] WebKit/Firefox handle select-option + set-input-files (JSON + DataTransfer). Firefox dblclick/hover/tap via JS events.
- [x] `EvaluateAsync` with argument + `EvaluateHandleAsync` on CR/WK/FF.
- [x] `ViewportSize` getter, `DefaultTimeout` / `DefaultNavigationTimeout`, `SetExtraHttpHeadersAsync`.
- [ ] Chromium 1228 CI confirm — still local-CDN / later.
- [ ] Windows fd plumbing — still later.
- [ ] Shadow-DOM piercing, layout engines, `ILocator` restore — backlog.

### Wave 33
- [x] Page-level `ClickAsync` / `FillAsync` / `FocusAsync` / `HoverAsync` / `PressAsync` / `TypeAsync` / `CheckAsync` / `UncheckAsync` via shared `ElementQuery.RunAsync`.
- [x] `IPage.TitleAsync` — `document.title` on Chromium and Firefox (WebKit already had it).
- [x] Firefox element-handle click/fill/focus/check/uncheck via shared `ElementStateScript` JS.
- [ ] Chromium 1228 CI confirm — still local-CDN / later.
- [ ] Windows fd plumbing — still later.
- [ ] Shadow-DOM piercing, layout engines, `ILocator` restore — backlog.

### Wave 32
- [x] `IPage.WaitForRequestAsync` / `WaitForResponseAsync` — shared `WaitForEventHelper` + `UrlMatcher` (glob / regex / predicate). Timeout messages contain `page.waitForRequest` / `page.waitForResponse`.
- [x] Page-level `GetAttributeAsync` / `InnerHTMLAsync` / `InnerTextAsync` / `TextContentAsync` / `IsCheckedAsync` / `IsDisabledAsync` / `IsEnabledAsync` / `IsEditableAsync` via shared `ElementQuery` (query + element handle). Firefox element-handle state methods wired.
- [ ] Chromium 1228 CI confirm — still local-CDN / later.
- [ ] Windows fd plumbing — still later.
- [ ] Shadow-DOM piercing, layout engines, `ILocator` restore — backlog.

### Wave 31
- [x] `IPage.WaitForSelectorAsync` — shared `WaitForSelectorHelper` on CRPageAdapter / WKPage / FFPageAdapter (`attached` / `detached` / `visible` / `hidden`). Timeout message contains `page.waitForSelector`.
- [x] `IPage.IsVisibleAsync` / `IsHiddenAsync` — one-shot query + shared `DomVisibility` heuristic (computed style + bounding box). Firefox `QuerySelectorAsync` + element-handle visibility wired so the waiter can run on all three stacks.
- [x] Locator-less `GetByTitleAsync` / `GetByTestIdAsync` returning `IElementHandle` (no `ILocator`).
- [ ] Chromium 1228 CI confirm — still local-CDN / later.
- [ ] Windows fd plumbing — still later.
- [ ] Shadow-DOM piercing, layout engines, `ILocator` restore — backlog.

### Wave 30
- [x] `IPage.WaitForURLAsync` — shared `UrlMatcher` + `WaitForUrlHelper` on CRPageAdapter / WKPage / FFPageAdapter. Glob / regex / predicate; then `WaitForLoadStateAsync`. Timeout message contains `page.waitForURL`.
- [x] Locator-less `GetByLabelAsync` / `GetByPlaceholderAsync` / `GetByAltTextAsync` returning `IElementHandle` (no `ILocator`). First-match tests in `DirectGetByTests`.

### Wave 29
- [x] `IPage.WaitForLoadStateAsync` — shared `LifecycleWaiter` on CRFrame / WKPage / FFPage (`load` / `DOMContentLoaded` / `networkidle`). Timeout message contains `page.waitForLoadState`.
- [x] `IPage.WaitForFunctionAsync` + `WaitForTimeoutAsync` — shared `WaitForFunctionHelper` poll loop (rAF default, interval optional, retry on execution-context disposal).
- [x] Locator-less `GetByRoleAsync` / `GetByTextAsync` returning `IElementHandle` (no `ILocator`). Polls via existing evaluate/query; first-match tests in `DirectGetByTests`.

### Status
- [x] Phase 0: Transport Foundation (#1)
- [x] Phase 1: Chromium Connection Layer (#2)
- [x] Phase 2: Chromium Page Creation (#3)
- [x] Phase 3: Navigation & JS Evaluation (#4)
- [x] Phase 4: Network & Request Interception (#5)
- [x] Phase 5a.1: Input primitives — Keyboard/Mouse/Touchscreen + 35 tests
- [x] Phase 5a.2: Element handles + Fill — CRJSHandle/CRElementHandle + 20 tests
- [x] Phase 5a.3: Form elements — SelectOption + Check/Uncheck/IsChecked + 15 tests
- [x] Phase 5a.4: Specialized — Tap + SetInputFiles + Drag + 10 tests
- [x] **Phase 5a: Input — COMPLETE** (DirectConnection: 143 tests)
- [x] Phase 5b.1: Content + Script/Style tags + Init scripts + 15 tests
- [x] Phase 5b.2: exposeFunction (Runtime.addBinding + bindingCalled) + 6 tests
- [x] **Phase 5b: Content & Scripts — COMPLETE** (DirectConnection: 164 tests)
- [x] Phase 5c: Screenshots & Media — Screenshots + PDF + Emulation + 13 tests
- [~] Phase 5d: Remaining APIs — Dialogs + Popups shipped (6 tests). Downloads, Workers, Accessibility deferred.
- [x] Phase 6.1: Compatibility audit — 368 method inventory + gap analysis
- [x] Phase 6 trim spec — retained-surface committed (~155 of 368; ILocator cut)
- [x] Phase 6.2a: Direct entry point — Playwright.LaunchChromiumAsync + 4 Direct* skeletons + 6 smoke tests
- [x] Phase 6.2b: Direct* events (page.Dialog, Popup, Request, Response, Load, etc.)
- [x] Phase 6.2c: Direct* element handle + JS handle wrappers
- [x] Phase 6.2d: Direct* input delegation (page.Mouse/Keyboard/Touchscreen accessors)
- [x] Phase 6.2e: Direct* content/screenshot/PDF/emulation methods
- [x] Phase 6.2f: Direct* route interception
- [x] Phase 6.3: Delete ILocator/IFrameLocator + orphaned driver-era interfaces (selectors, page assertions, etc.)
- [x] Phase 6.4+6.5: Delete legacy channel-based classes + Transport plumbing (~120 files, ~11k lines)
- [x] Phase 6.6: Delete driver tooling + bundled binaries
- [x] Phase 6.7a: Purge legacy driver-based tests (~239 files, ~23.7k LoC)
- [x] Phase 6.3b: Trim IPage/IBrowserContext/IFrame/IElementHandle to retained surface (remove stubs for cut methods)
- [x] Phase 6.3c: Trim IFrame and IElementHandle to retained surface
- [x] Phase 6.7b: CI update + CHANGELOG / README (explain breaking change)
- [x] Phase 7: Firefox Support (#8) — Juggler protocol via direct pipe (`a44dde7`, PR #12). FF*Adapter surface + LaunchFirefoxAsync.
- [~] Phase 8: WebKit Support (#9) — 8a (launch + smoke) MERGED (PR #15, commits b0d13f5..25765da). WKConnection/Session/Browser/Context/Page + AnonymousPipeServerStream fd 3/4 plumbing on macOS-14 + 6 smoke tests. Phase 8b status:
  - [x] Inner Target session: `WKTargetSession` wraps outbound via `Target.sendMessageToTarget`, unwraps inbound via `Target.dispatchMessageFromTarget`. WKPage runs Page.enable/getResourceTree/Runtime.enable/Console.enable/Network.enable on the inner session. Provisional target swap (`Target.didCommitProvisionalTarget`) handles cross-process navigation. `Target.resume` sent after init (targets are created paused). 31 new tests across `WKNavigationTests` (17), `WKEvaluationTests` (11 — `awaitPromise` deferred), `WKPageLifecycleTests` (3).
  - [x] Linux WebKit: `libWPEWebKit-2.0.so.1: file too short` was Unix symlink entries left as 30-byte text files by `ZipFile.ExtractToDirectory`. `ArchiveExtractor` now detects S_IFLNK entries and restores them via `File.CreateSymbolicLink`. Linux WebKit job re-added to CI matrix.
  - Windows fd plumbing: `STARTUPINFOEX` + `UpdateProcThreadAttribute(PROC_THREAD_ATTRIBUTE_HANDLE_LIST)`. `BrowserProcessManager` throws clearly on Windows until then.
  - [x] Mauro refactor leftovers: `TransportMode` enum + `InheritablePipes` record + `BuildFdRemapShellInvocation` helper (branch `feature/phase8b-mauro-refactor`).
- [ ] Phase 8c: WebKit input + network + DOM — ~70 tests, once 8b's Target session lands.
- [~] Phase 9: Cleanup & Polish (#10) — Direct→CR* rename shipped (`1580326`, PR #13). Remaining: dead-code sweep, packaging, docs.
- [x] BrowserFetcher: programmatic download + auto-resolved LaunchAsync (Puppeteer-Sharp interface, playwright-dotnet options bag)
- [x] CI uses BrowserFetcher (drops `npx playwright install`) + `actions/cache` for browser binaries (PR #14)

### Local-Only Development (April-May 2026) — archived
Driver-era note: at the time, old driver-based tests could not run on mac26-arm64
and validation used `dotnet test --filter "Category=DirectConnection"`.
CI now runs the full `PlaywrightNative.Tests` suite with `PRODUCT=CHROMIUM` or
`PRODUCT=WEBKIT` (see `.github/workflows/dotnet.yml`).

### Weekly Targets (April 13 - May 13)
- Week 1: Phase 4 core (CRNetworkManager, request/response objects) → ~88 tests
- Week 2: Phase 4 done + Phase 5a start (input) → ~168 tests
- Week 3: Phase 5a done + 5b + 5c → ~298 tests
- Week 4: Phase 5d (remaining APIs) → ~478 tests

## Notes
- See `docs/superpowers/specs/2026-04-13-local-first-driver-removal-plan.md` for full spec
- See `tasks/architecture.md` for architecture details and reference file paths
- See GitHub project: https://github.com/orgs/hardkoded/projects/4
- Always start work on a new branch (never commit to main)
- Each phase branch stacks on the previous one

### Phase 4 summary
- DirectConnection: 63 tests passing (was 38 before Phase 4)
- Phase 4 commits: c983fc4..77e5109 (branch `feature/remove-driver-phase4`)
  - c983fc4 fix(phase4): omit null fields from Fetch.continueRequest/fulfillRequest params
  - 5932d37 fix(phase4): lock _routes and drop CDP trace noise per code review
  - 290b5d1 test(phase4): add route continue-with-overrides, fulfill, and non-matching tests
  - 0035944 test(phase4): tighten Task 6 test assertions per review
  - cb0a27b test(phase4): implement ShouldFireRequestFailedEvent (was stubbed)
  - c5f2c72 feat(phase4): add context-level route registration
  - da0f1b7 fix(phase4): suppress TargetClosedException noise on context teardown
  - d4fb1d2 feat(phase4): add WaitUntilState.NetworkIdle via inflight request tracking
  - 77e5109 fix(phase4): guard CRFrame lifecycle state under inflight lock
