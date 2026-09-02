# Selectors leftover campaign

The screencast table ended at Wave 634. Official Playwright still
has `ISelectors` / `Playwright.Selectors.RegisterAsync` this repo
lacks.

If you were started to keep moving, **do not stop after one wave** and
**do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/screencast-campaign.md`
**Next campaign:** `tasks/credentials-campaign.md`

## Goal

Port official playwright-dotnet `ISelectors`:

- `RegisterAsync(name, script)` — custom selector engines (`name=body`)
- `SetTestIdAttribute` already exists on `Playwright`

Must run on **CR + WK**. Do **not** invent inspector-only APIs.
Extra optional parameters go at the **end**.

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

First wave is **635**.

| Wave | Slice |
|------|--------|
| **635** | `ISelectors.RegisterAsync` — done |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.
Use a Direct spec id such as `direct/selectors-register.cs`.

## When exhausted

Open `tasks/credentials-campaign.md`. Set Current Phase to Wave 636.
Do **not** invent filler waves.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
