# Screenshot expect campaign

Expect matchers other than screenshots land in `tasks/expect-campaign.md`. This campaign adds official `ToHaveScreenshot` on Chromium and WebKit.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/expect-campaign.md`
**Next campaign:** `tasks/pause-campaign.md`

## Goal

Official Playwright .NET:

- `ILocatorAssertions.ToHaveScreenshotAsync`
- `IPageAssertions.ToHaveScreenshotAsync`
- Compare against a golden / expected image (this repo already has ImageSharp)
- Options that exist upstream: `timeout`, `maxDiffPixels`, `maxDiffPixelRatio`, `threshold`, `animations`, `caret`, `scale`, `mask`, `maskColor`, `omitBackground`

`IPage.Screenshot` `mask` already landed in Wave 509. Reuse it. Do **not** invent a second screenshot stack.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 screenshot waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official screenshot-expect slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`. First wave is **550**.

| Wave | Slice |
|------|--------|
| **550** | `ILocatorAssertions.ToHaveScreenshotAsync` (golden / bytes) |
| **551** | `IPageAssertions.ToHaveScreenshotAsync` |
| **552** | `maxDiffPixels` / `maxDiffPixelRatio` / `threshold` |
| **553** | `animations` / `caret` / `omitBackground` / existing `mask` |

Stop when every official `ToHaveScreenshot` overload that can run here is on `main`. Then follow **Next campaign**.

## Conventions

Same C# rules as `tasks/locator-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/pause-campaign.md` using the steps in `tasks/campaign-chain.md` (section **Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
