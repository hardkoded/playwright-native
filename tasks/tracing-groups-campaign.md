# Tracing groups campaign

Leftover Wave 500 skipped `ITracing.GroupAsync` / `GroupEndAsync`
("do not no-op"). Start / Stop / StartChunk / StopChunk are on `main`.
This campaign adds official groups against that tracer.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/unroute-behavior-campaign.md`
**Next campaign:** `tasks/getby-regex-campaign.md`

## Goal

Official Playwright:

- `ITracing.GroupAsync(name)` (location / title if official)
- `ITracing.GroupEndAsync`

Implement against the existing `ITracing` in
`src/PlaywrightSharp/Contracts/ITracing.cs`. Groups must show up in the
trace the same session already writes (Chrome JSON after Start/Stop, or
the chunk file). Do **not** no-op. Do **not** invent a zip Trace Viewer
or a second tracer. Do **not** map `TracesDir` onto zip traces (still
leftover-skip).

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 tracing-group waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official group slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`. First wave is **587**.

| Wave | Slice |
|------|--------|
| **587** | `ITracing.GroupAsync` |
| **588** | `ITracing.GroupEndAsync` |

Waves 587–588 are on `main`. Nested groups use the same stack. Then follow
**Next campaign**.

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/getby-regex-campaign.md` using `tasks/campaign-chain.md`
(**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
