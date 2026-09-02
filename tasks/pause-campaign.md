# Pause / inspector campaign

Locator and leftover playbooks deferred `IPage.PauseAsync`. This campaign ports the official inspector pause on Chromium and WebKit.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/screenshot-campaign.md`
**Next campaign:** `tasks/client-certificates-campaign.md`

## Goal

Official Playwright:

- `IPage.PauseAsync` — stop and wait for the inspector / a resume signal
- Only what upstream documents. Do **not** invent a debugger UI.

If a faithful pause cannot run headless in this environment, implement the public API plus a tested no-inspector path (resume / timeout) and document the gap. Do not fake a GUI.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 pause waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official pause slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`.

| Slice | API |
|-------|-----|
| Page pause | `IPage.PauseAsync` |
| Resume / timeout | Documented resume behavior that can be tested headless |

One or two real slices is enough. Then follow **Next campaign**.

## Conventions

Same C# rules as `tasks/locator-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/client-certificates-campaign.md` using `tasks/campaign-chain.md` (**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
