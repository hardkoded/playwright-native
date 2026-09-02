# Leftover Playwright API campaign

Unattended Cloud Agents continue this campaign from `origin/main` without a human typing "next".

If you were started by a Cursor Automation, or the user said "keep going", **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Next campaign:** `tasks/locator-campaign.md` (complete). Active chain: `tasks/campaign-chain.md`.

## Goal

Port the remaining official Playwright public API that PlaywrightSharp still lacks, one wave at a time, until the hunt is exhausted.

A leftover is a **real** method, option, overload, or enum that:

1. Exists on official Playwright (Node) and/or `microsoft/playwright-dotnet`, and
2. Is missing or incomplete on PlaywrightSharp's public surface, and
3. Can be implemented against the **direct** Chromium (CDP) and WebKit stacks in this repo (no Node.js driver).

Do **not** invent convenience wrappers, rename existing APIs, or pad the campaign with fakes.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase says the leftover hunt is **exhausted**, stop. Do not invent work.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, assume another agent is in flight and **stop**. Do not race on `main`.
5. Create `cursor/<descriptive-name>-554a` from latest `main`. Never commit implementation on `main`.
6. Implement the next **20 leftover waves** (or until the hunt is exhausted), one wave at a time.

## Per-wave loop (mandatory)

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Hunt **one** leftover (see Hunt). Put it in `tasks/todo.md` as Current Phase Wave NNN, unchecked.
2. Implement it (CR + WK at minimum; Firefox may `Assert.Ignore` when the API is Chromium-only).
3. `git add` / `git commit` the feature (`feat: … (Wave NNN)`).
4. `git push -u origin <branch>`.
5. Run Chromium **and** WebKit tests for the new coverage. Both must be green.
6. Docs commit: check the box, move the wave to Previous, set Current Phase to NNN+1 (`docs: mark Wave NNN … complete`).
7. Fast-forward merge to `main` and `git push origin main`.
8. Repeat.

No pull requests. No `ManagePullRequest`. Owner merges by ff-merge to `main`.

## Hunt

Compare, in this order:

- `src/PlaywrightSharp/Contracts/` (`IPage`, `IFrame`, `IElementHandle`, `IBrowser`, `IBrowserContext`, `IRoute`, `IRequest`, `IResponse`, `ITracing`, `IAPIRequest`, `BrowserContextOptions`, `BrowserTypeLaunchOptions`)
- Official Playwright docs / Node types
- `microsoft/playwright-dotnet` public interfaces when the checkout exists (`../../microsoft/playwright-dotnet` or clone it)

Prefer a leftover that is **one public API** with a test you can write against the local test server.

The leftover hunt is **exhausted** after Wave 500. Do not invent filler waves. Remaining official surface is on the skip list below.

### Skip (do not pad the campaign)

These were already judged fake, locator-shaped, or too invasive. Do not use them as waves:

- `ILocator` and locator-only APIs — moved to `tasks/locator-campaign.md` (Wave 501+)
- GetBy / Filter Regex overloads — `tasks/getby-regex-campaign.md`
- Shadow DOM piercing as a new public surface
- `UnrouteBehavior` — `tasks/unroute-behavior-campaign.md`
- `ITracing.StartChunk` / `StopChunk` — `tasks/tracing-chunks-campaign.md`
- `ITracing.GroupAsync` / `GroupEndAsync` — `tasks/tracing-groups-campaign.md`
- `IgnoreDefaultArgs` as `string[]` — `tasks/ignore-default-args-campaign.md` (bool form already shipped)
- Screenshot `mask` (locator Wave 509). Screenshot **expect** is `tasks/screenshot-campaign.md`
- `IPage.PauseAsync` (inspector) — `tasks/pause-campaign.md`
- `clientCertificates` — `tasks/client-certificates-campaign.md`
- Firefox-only stubs that cannot run in this environment
- `FirefoxUserPrefs` / Firefox `LaunchPersistentContext` — `tasks/firefox-persistent-campaign.md`
- `IRequest.Sizes` (exists)
- `IResponse.ServerAddr` / `SecurityDetails` (CR/WK already implement)
- `RequestGC` (exists)
- `AddInitScript` path overload (exists)
- `SlowMo` (no clean hook)
- `RouteFromHAR(..., update: true)` — `tasks/har-update-campaign.md`

Chromium-only APIs are allowed. WebKit tests should `Assert.Ignore` with a clear reason. Do **not** wrap `Assert.Ignore` in `Assert.CatchAsync`.

## Implementation conventions

- Extra optional parameters go at the **end** of generated signatures.
- Map new `BrowserContextOptions` / `BrowserTypeLaunchOptions` through CR, WK, and FF adapters.
- After adding a generated parameter, every `cref` on `IPage.cs` / `IFrame.cs` / `IElementHandle.cs` / `IBrowser.cs` must list **every** generated param type (CS1574).
- New enums: `Undefined = 0` as the default.
- One type per file (SA1402).
- CRLF on all `.cs` files **and** `tasks/todo.md`. After a `Write`, convert `\n` → `\r\n`.
- `ConfigureAwait(false)` on every `await`.
- No `var` for built-in types (`int`, `string`, `bool`, …).
- Private fields `_camelCase`. XML docs on public APIs. No "puppeteer".
- SA1204 static before instance; SA1201 properties before methods; SA1202 public before internal before private.
- SA1513 blank line after a closing brace; SA1508 no blank line before a closing brace.
- CA1308 `ToUpperInvariant`.
- CS1573: every documented method's parameters need `<param>`.
- NUnit 4: `Assert.ThrowsAsync<Exception>` does **not** match derived types — use `Assert.CatchAsync` or the exact type. `Assert.CatchAsync` is **sync** — do not `.ConfigureAwait` it.
- `EvaluateAsync` is raw `Runtime.evaluate` — wrap page scripts in an IIFE.
- `ActionScroll`: `WaitRunAsync` / `WaitQueryAsync` default to `ActionScroll.None`. Public actions pass the caller's `scroll`. Convenience overloads that should Auto-scroll pass `ActionScroll.Undefined`. Focus-based actions use `focus({ preventScroll })`, not `ScrollIntoViewIfNeededAsync`. Type/Press also snapshot/restore ancestor scroll (`ElementStateScript.CaptureAncestorScrollsFunction` / `RestoreAncestorScrollsFunction`) because Chrome caret-scrolls overflow ancestors.
- Empty `CookieClearFilter` still clears the whole store. URL filter uses `ContextCookies.MatchesUrl`.
- Per-call `HttpClient` for `IRoute.FetchAsync` options (`maxRedirects`, `maxRetries`).
- After editing tests, **rebuild**. `--no-build` after a source-only fix reuses a stale DLL.

## Test locally (before merge)

```bash
# Always set PRODUCT. Kill leftover Chrome first.
killall -9 chrome chrome_crashpad_handler || true

export DOTNET_ROOT="$HOME/.dotnet"
export PATH="$DOTNET_ROOT:$PATH"

dotnet build ./src/PlaywrightSharp/PlaywrightSharp.csproj -f net10.0

PRODUCT=CHROMIUM dotnet test ./src/PlaywrightSharp.Tests/PlaywrightSharp.Tests.csproj \
  -f net10.0 --filter "FullyQualifiedName~<YourNewTestClass>" --no-restore

PRODUCT=WEBKIT dotnet test ./src/PlaywrightSharp.Tests/PlaywrightSharp.Tests.csproj \
  -f net10.0 --filter "FullyQualifiedName~<YourNewTestClass>" --no-restore
```

- Chrome: `/opt/google/chrome/chrome` or `/usr/local/bin/chrome`
- WebKit: `~/.cache/ms-playwright/webkit-2276/pw_run.sh`
- Test server: `TestConstants.ServerUrl` (`http://localhost:8081`), `EmptyPage`, `CrossProcessHttpPrefix`
- `netstandard2.1` still has pre-existing `OperatingSystem.IsMacOS` / `TrimEntries` errors — ignore those; build `-f net10.0`
- Firefox launch may `Assert.Ignore` here. Local gate is **CR + WK** only.
- Never skip tests or add `TestExpectations` entries to make a leftover wave "green".

## Docs format (`tasks/todo.md`)

```
## Current Phase: Wave NNN — short name

### Wave NNN
- [ ] `IWhatever.Foo(...)` one-line leftover.

## Previous: Wave MMM — …

### Wave MMM
- [x] `IWhatever.Bar(...)` …
```

Keep Wave 417+ history. Do not rewrite old waves.

## When the hunt is exhausted

Leftover ended at Wave 500. **Next campaign:** `tasks/locator-campaign.md` (already complete). Active chain: `tasks/campaign-chain.md`.

Do not reopen leftover waves. Skip-list items now have their own playbooks:

- Screenshot expect → `tasks/screenshot-campaign.md`
- `IPage.PauseAsync` → `tasks/pause-campaign.md`
- `clientCertificates` → `tasks/client-certificates-campaign.md`
- `RouteFromHAR(..., update: true)` → `tasks/har-update-campaign.md`
- Expect option bags → `tasks/expect-options-campaign.md`
- Firefox smoke → `tasks/firefox-smoke-campaign.md`
- `ITracing.StartChunk` / `StopChunk` → `tasks/tracing-chunks-campaign.md`
- `UnrouteBehavior` → `tasks/unroute-behavior-campaign.md`
- `ITracing.GroupAsync` / `GroupEndAsync` → `tasks/tracing-groups-campaign.md`
- GetBy / Filter Regex → `tasks/getby-regex-campaign.md`
- `IgnoreDefaultArgs` as `string[]` → `tasks/ignore-default-args-campaign.md`
- Firefox `LaunchPersistentContext` / `FirefoxUserPrefs` → `tasks/firefox-persistent-campaign.md`

## Automation prompt (for cursor.com/automations)

Leftover is exhausted after Wave 500. Paste the prompt in `tasks/campaign-chain.md`.
