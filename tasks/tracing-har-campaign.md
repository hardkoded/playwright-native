# Tracing HAR leftover campaign

Official leftover Waves 626–628 are on `main`. Official Playwright still
has `tracing.startHar` / `tracing.stopHar` this repo lacked.

If you were started to keep moving, **do not stop after one wave** and
**do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/official-leftover-campaign.md`
**Next campaign:** `tasks/screencast-campaign.md`

## Goal

Port official playwright-dotnet `ITracing.StartHarAsync` /
`StopHarAsync`. This is context network recording (HAR 1.2), not
Chromium performance tracing. Reuse `Helpers/HarRecorder.cs`. Must
run on **CR + WK**.

Do **not** invent inspector-only APIs (`pickLocator`, `browser.bind`).
Do **not** invent zip packaging as a separate wave if Start+Stop is
the slice. Extra optional parameters go at the **end**.

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

First wave is **629**.

| Wave | Slice |
|------|--------|
| **629** | `ITracing.StartHarAsync` / `StopHarAsync` — done |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.
Use a Direct spec id such as `direct/tracing-start-har.cs`.

## When exhausted

Open `tasks/screencast-campaign.md`. Set Current Phase to Wave 630.
Do **not** invent filler waves.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
