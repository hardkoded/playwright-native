# Screencast leftover campaign

The tracing HAR table ended at Wave 629. Official Playwright still
has `IPage.Screencast` / `IScreencast` this repo lacks.

If you were started to keep moving, **do not stop after one wave** and
**do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/tracing-har-campaign.md`
**Next campaign:** `tasks/selectors-campaign.md`

## Goal

Port official playwright-dotnet `IPage.Screencast`:

- `StartAsync` / `StopAsync` — JPEG frames and optional video path
- `ShowOverlayAsync` — HTML overlay (CR + WK inject)
- later: chapters / actions if still unused

Chromium already has `Page.startScreencast` via
`Helpers/VideoRecorder.cs`. Overlays are HTML inject and must work
on **CR + WK**. WebKit may `Assert.Ignore` video encode if the
engine cannot write the file.

Do **not** invent inspector-only APIs. Extra optional parameters go
at the **end**.

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

First wave is **630**.

| Wave | Slice |
|------|--------|
| **630** | `IPage.Screencast` `StartAsync` / `StopAsync` — done |
| **631** | `IScreencast.ShowOverlayAsync` — done |
| **632** | `IScreencast.ShowChapterAsync` — done |
| **633** | `IScreencast.ShowActionsAsync` / `HideActionsAsync` — done |
| **634** | `IScreencast.ShowOverlaysAsync` / `HideOverlaysAsync` — done |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.
Use a Direct spec id such as `direct/page-screencast.cs`.

## When exhausted

Open `tasks/selectors-campaign.md`. Set Current Phase to Wave 635.
Do **not** invent filler waves.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
