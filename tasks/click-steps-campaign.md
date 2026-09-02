# Official click steps leftover campaign

The aria snapshot JSON table ended at Wave 614. Official Playwright
still has `locator.click({ steps })` this repo lacked.

If you were started to keep moving, **do not stop after one wave** and
**do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/aria-snapshot-json-campaign.md`
**Next campaign:** `tasks/console-filter-campaign.md`

## Goal

Port official Playwright click `steps` (Node v1.57 / playwright-dotnet)
that this repo still lacks:

- `locator.click({ steps })` — intermediate `mousemove` events
- `page.click({ steps })` / `frame.click({ steps })` selector form

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

First wave is **615**.

| Wave | Slice |
|------|--------|
| **615** | `ILocator.ClickAsync` `steps` — done |
| **616** | `IPage.ClickAsync` / `IFrame.ClickAsync` `steps` — done |

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
