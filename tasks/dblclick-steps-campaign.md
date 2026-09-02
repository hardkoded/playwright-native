# Official dblclick steps leftover campaign

The console filter table ended at Wave 618. Official Playwright still
has `locator.dblclick({ steps })` this repo lacked.

If you were started to keep moving, **do not stop after one wave** and
**do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/console-filter-campaign.md`
**Next campaign:** `tasks/web-storage-campaign.md`

## Goal

Port official Playwright dblclick `steps` (Node / playwright-dotnet)
that this repo still lacks:

- `locator.dblclick({ steps })` — intermediate `mousemove` events
- `elementHandle.dblclick({ steps })` — same option on the handle

Official `page.dblclick` / `frame.dblclick` do **not** take `steps`.
Do **not** invent inspector-only APIs (`pickLocator`). Extra optional
parameters go at the **end**. Wire CR + WK.

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

First wave is **619**.

| Wave | Slice |
|------|--------|
| **619** | `ILocator.DblClickAsync` `steps` — done |
| **620** | `IElementHandle.DblClickAsync` `steps` — done |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Set Current Phase to:

```
## Current Phase: compatibility campaigns exhausted
```

Stop. Do **not** invent filler waves.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
