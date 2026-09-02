# Official leftover campaign

The APIResponse leftover table ended at Wave 625. Official Playwright still
has public surface this repo lacks that can run on the direct Chromium and
WebKit stacks.

If you were started to keep moving, **do not stop after one wave** and
**do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/api-response-leftover-campaign.md`
**Next campaign:** `tasks/tracing-har-campaign.md`

## Goal

Port official Playwright (Node / `microsoft/playwright-dotnet`) leftovers
this repo still lacks:

- `locator.elementHandles()` — official alias of existing `AllAsync()`
- `ScreenshotType.Webp` — official 1.62; Chromium CDP native
- `APIRequestContext.fetch(request)` — replay an `IRequest`

Do **not** invent inspector-only APIs (`pickLocator`, `browser.bind`).
Do **not** invent `ConsoleMessage` line/column (official .NET is still
`string Location`). Extra optional parameters go at the **end**. Wire
CR + WK.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50** waves (or until exhausted), one wave at a time.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

First wave is **626**.

| Wave | Slice |
|------|--------|
| **626** | `ILocator.ElementHandlesAsync` — done |
| **627** | `ScreenshotType.Webp` — done |
| **628** | `IAPIRequestContext.FetchAsync(IRequest)` — done |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.
WebKit may `Assert.Ignore` for WebP if the engine cannot encode it.

## When exhausted

Open `tasks/tracing-har-campaign.md`. Set Current Phase to Wave 629.
Do **not** invent filler waves.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
