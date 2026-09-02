# Tracing chunks campaign

Leftover Wave 500 skipped `ITracing.StartChunk` / `StopChunk` until the
existing Start/Stop model was redesigned. Start/Stop are on `main`. This
campaign adds the official chunk APIs against that tracer.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/firefox-smoke-campaign.md`
**Next campaign:** `tasks/unroute-behavior-campaign.md`

## Goal

Official Playwright:

- `ITracing.StartChunkAsync` (name / title if official)
- `ITracing.StopChunkAsync` (path)

Implement against the existing `ITracing` Start/Stop in
`src/PlaywrightSharp/Contracts/ITracing.cs`. Do **not** invent a second
tracer. Do **not** implement `GroupAsync` / `GroupEndAsync` as no-ops
(those stay on the leftover skip list).

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 tracing-chunk waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official chunk slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md` after firefox-smoke is exhausted.

| Wave | Slice |
|------|--------|
| **582** | `ITracing.StartChunkAsync` |
| **583** | `ITracing.StopChunkAsync` writes a file |

`StartChunkAsync` / `StopChunkAsync` (name, title, path) are on `main`.
This campaign is exhausted. Follow **Next campaign**.

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/unroute-behavior-campaign.md` using `tasks/campaign-chain.md`
(**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
