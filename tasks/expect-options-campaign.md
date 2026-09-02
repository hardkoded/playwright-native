# Expect options campaign

Expect matchers landed through Wave 549. Screenshot, pause, certs, HAR, and
Firefox launcher are on `main`. This campaign adds official per-call option
bags that Wave 549 deferred (`ignoreCase`, `useInnerText`, `checked: false`).

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/firefox-campaign.md`
**Next campaign:** `tasks/firefox-smoke-campaign.md`

## Goal

Official Playwright .NET expect options still missing on `ILocatorAssertions`
and `IPageAssertions`:

- `ignoreCase` on `ToHaveText` / `ToContainText` (and a11y name/description
  if official and still missing)
- `useInnerText` on `ToHaveText` / `ToContainText`
- `ToBeCheckedAsync(checked:, indeterminate:)`
- `ToBeAttachedAsync(attached:)` if still missing
- `ToHaveURLAsync(..., ignoreCase:)` if still missing
- `ToHaveAttributeAsync` name-only (presence) if official and still missing

Reuse the existing poll / `ExpectWaiter` / `UniqueStateAsync` path. Extra
optional parameters go at the **end**. Do **not** invent `ToPass`-style APIs
(Wave 548 already shipped; no more unofficial matchers).

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 expect-options waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official expect-option slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`. First wave is **565**.

| Wave | Slice |
|------|--------|
| **565** | `ToHaveTextAsync` / `ToContainTextAsync` `ignoreCase` (on `main`) |
| **566** | `ToHaveTextAsync` / `ToContainTextAsync` `useInnerText` (on `main`) |
| **567** | `ToBeCheckedAsync(checked:, indeterminate:)` (on `main`) |
| **568** | `ToBeAttachedAsync(attached:)` / `ToHaveURLAsync(ignoreCase:)` (on `main`) |
| **569** | `ToHaveAttributeAsync(..., ignoreCase)` (on `main`) |
| **570** | a11y `ignoreCase` (on `main`) |
| **571** | `ToBeEnabledAsync(enabled:)` / `ToBeVisibleAsync(visible:)` / `ToBeEditableAsync(editable:)` (on `main`) |
| **572** | `ToHaveAttributeAsync(name)` presence (on `main`) |

The expect-options hunt is exhausted after Wave 572. Do **not** invent
filler option bags. **Next campaign** is `tasks/firefox-smoke-campaign.md`.

## Conventions

Same C# rules as `tasks/leftover-campaign.md` and `tasks/expect-campaign.md`.
Local gate is **CR + WK**. Firefox may `Assert.Ignore`.

## When exhausted

Hand off to `tasks/firefox-smoke-campaign.md` using `tasks/campaign-chain.md`
(**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
