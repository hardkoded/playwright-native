# Firefox persistent campaign

Wave 581 documented that `LaunchPersistentContext` is not wired for
Firefox (`BrowserTypeInfo` throws). Portable persistent Direct tests
Ignore with that ABI reason. `FirefoxUserPrefs` was leftover-skipped as
Firefox-only before Juggler could launch. Both are official now that
`LaunchFirefoxAsync` works.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/ignore-default-args-campaign.md`
**Next campaign:** none — chain ends

## Goal

Official Playwright:

- `LaunchPersistentContextAsync` on Firefox (same public
  API CR/WK already expose)
- `FirefoxUserPrefs` on Firefox launch / persistent options if still
  missing

Do **not** invent Firefox-only stub APIs. Do **not** hide a connect bug
with `TestExpectations` skips. Ignore only with a **real** ABI/reason.

Linux CI may still skip Firefox if ABI is incompatible (see `CLAUDE.md`).
Still land the persistent launcher when the executable exists.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 Firefox-persistent waves** (or until exhausted), one wave at a time.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official Firefox persistent / prefs slice.
2. `feat` commit, `git push -u origin <branch>`.
3. Gate: Chromium **and** WebKit still green for touched tests. Firefox:
   run `PRODUCT=FIREFOX` when the executable exists; Ignore only with a
   real ABI/reason.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md` after ignore-default-args is exhausted.
First wave is **594**.

| Wave | Slice |
|------|--------|
| **594** | `LaunchPersistentContextAsync` on Firefox — done |
| **595** | `FirefoxUserPrefs` if official and missing — done |
| Smoke | Covered by Wave 594 localStorage persist test (Ignore when Firefox is missing) |

When persistent launch that can run on this Juggler is on `main` (or
documented with a real ABI ignore) and official prefs that can run are
done, the campaign is exhausted.

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Do not add
`TestExpectations` skips to hide a launcher bug.

## When exhausted

The chain is done. Set Current Phase to:

```
## Current Phase: compatibility campaigns exhausted
```

Stop. Do **not** invent filler waves. Do **not** open a new campaign unless a
human adds a playbook and points **Next campaign** here.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
