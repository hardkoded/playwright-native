# GetBy Regex campaign

Locator core landed string `GetByText` / `GetByLabel` / `GetByPlaceholder`
/ `GetByAltText` / `GetByTitle` / `GetByTestId`. Official Playwright also
takes `Regex` on those helpers (and on `Filter` / `HasNotText`).

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/tracing-groups-campaign.md`
**Next campaign:** `tasks/ignore-default-args-campaign.md`

## Goal

Official Playwright .NET Regex overloads still missing on `ILocator`,
`IPage`, `IFrame`, and `IFrameLocator`:

- `GetByText(Regex)`
- `GetByLabel(Regex)`
- `GetByPlaceholder(Regex)`
- `GetByAltText(Regex)`
- `GetByTitle(Regex)`
- `GetByTestId(Regex)`
- `Filter(Regex)` / `HasNotText(Regex)` if official and still missing

Reuse the existing GetBy / Filter query path. Do **not** invent a second
locator stack. Do **not** add `ISelectors.Register` here (custom engines
stay out of this campaign).

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 GetBy-Regex waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official Regex GetBy / Filter slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md` after tracing-groups is exhausted.
First wave is **589**.

| Wave | Slice |
|------|--------|
| **589** | `GetByText(Regex)` on locator / page / frame |
| **590** | `GetByLabel(Regex)` / `GetByPlaceholder(Regex)` |
| **591** | `GetByAltText(Regex)` / `GetByTitle(Regex)` / `GetByTestId(Regex)` |
| **592** | `Filter(Regex)` / `HasNotText(Regex)` |

Waves 589–592 are on `main`. Official GetBy/Filter Regex overloads that
can run on the direct CR/WK stacks are done. Then follow **Next campaign**.

## Conventions

Same C# rules as `tasks/leftover-campaign.md` and `tasks/locator-campaign.md`.
Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/ignore-default-args-campaign.md` using
`tasks/campaign-chain.md` (**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
