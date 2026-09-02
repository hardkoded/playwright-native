# HAR update campaign

Leftover Wave 500 skipped `RouteFromHAR(..., update: true)` as invasive. This campaign ports that official option in a small, tested slice.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/client-certificates-campaign.md`
**Next campaign:** `tasks/firefox-campaign.md`

## Goal

Official Playwright:

- `IPage.RouteFromHARAsync` / `IBrowserContext.RouteFromHARAsync` with `update: true`
- Official companions if missing: `updateMode`, `updateContent`

`RouteFromHAR` playback already exists. This campaign is **record/update only**. Do not rewrite the player. Do not invent a second HAR format.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 HAR-update waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official update slice with Direct CR + WK tests against the local test server.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`.

| Slice | API |
|-------|-----|
| Update flag | `RouteFromHARAsync(..., update: true)` writes/extends the HAR |
| Update mode | `updateMode` if official and missing |
| Update content | `updateContent` if official and missing |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/firefox-campaign.md` using `tasks/campaign-chain.md` (**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
