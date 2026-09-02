# UnrouteBehavior campaign

Leftover Wave 500 skipped official `UnrouteBehavior` as too invasive.
`UnrouteAsync` / `UnrouteAllAsync` already exist on page and context
(CR/WK wired; Firefox adapters throw `NotImplemented`). This campaign adds
the official wait-vs-ignore-pending-handlers option.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/tracing-chunks-campaign.md`
**Next campaign:** `tasks/tracing-groups-campaign.md`

## Goal

Official Playwright:

- `UnrouteBehavior` enum (`Wait` / `IgnoreErrors` / default as upstream documents)
- `IPage.UnrouteAsync` / `UnrouteAllAsync` accept the behavior
- `IBrowserContext.UnrouteAsync` / `UnrouteAllAsync` accept the behavior

Wire CR and WK. Extra optional parameters go at the **end**. Do **not**
invent a second router. Do **not** pad leftover waves.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 UnrouteBehavior waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official UnrouteBehavior slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md` after tracing-chunks is exhausted.

| Wave | Slice |
|------|--------|
| **584** | `UnrouteBehavior` public type |
| **585** | `IPage.UnrouteAsync` / `UnrouteAllAsync` behavior |
| **586** | `IBrowserContext.UnrouteAsync` / `UnrouteAllAsync` behavior |

When the official wait/ignore behavior is on `main` and tested on CR+WK,
the campaign is exhausted. Waves 584–586 are on `main`.

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.
New enums: `Undefined = 0` as the default.

## When exhausted

Hand off to `tasks/tracing-groups-campaign.md` using `tasks/campaign-chain.md`
(**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
