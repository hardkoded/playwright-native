# IgnoreDefaultArgs list campaign

Leftover Wave 500 skipped `IgnoreDefaultArgs` as `string[]` because the
bool form already shipped. Official Playwright still accepts a list of
default switches to drop (for example `--mute-audio`).

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/getby-regex-campaign.md`
**Next campaign:** `tasks/firefox-persistent-campaign.md`

## Goal

Official Playwright:

- `BrowserTypeLaunchOptions.IgnoreDefaultArgs` as a list of default
  argument names to omit, without dropping the rest of PlaywrightSharp's
  defaults
- Keep the existing `bool` form (`true` = ignore all defaults except
  required plumbing such as remote debugging)

Do **not** replace the bool. Extra optional parameters / overloads go at
the **end**, or use a type that can represent both official forms without
breaking existing callers. Wire CR (and WK if it has default args).
Do **not** invent launch flags that upstream does not document.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 IgnoreDefaultArgs waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official IgnoreDefaultArgs-list slice with Direct CR (+ WK if it applies) tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green for touched tests.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md` after getby-regex is exhausted.
First wave is **593**.

| Wave | Slice |
|------|--------|
| **593** | Omit named default args (official example: `--mute-audio`) — done |
| Bool still works | Covered by existing `IgnoreDefaultArgs = true` Direct tests |

Then follow **Next campaign**.

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Local gate is **CR + WK**.

## When exhausted

Hand off to `tasks/firefox-persistent-campaign.md` using
`tasks/campaign-chain.md` (**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
