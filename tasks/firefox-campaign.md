# Firefox campaign

Direct tests use `BrowserLauncher`, which today `Assert.Ignore`s Firefox. This campaign wires official Firefox launch so `PRODUCT=FIREFOX` can run the portable Direct suite.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/har-update-campaign.md`
**Next campaign:** `tasks/expect-options-campaign.md`

## Goal

- Resolve a Firefox executable the same way Chromium/WebKit do (`BrowserExecutableFixture`)
- `BrowserLauncher.LaunchAsync` launches Firefox when `PRODUCT=FIREFOX`
- Portable Direct tests that already run on CR+WK should run on Firefox or `Assert.Ignore` with a **real** ABI/reason (not a blanket skip)
- Do **not** invent Firefox-only stub APIs. Do **not** pad with wrappers

Linux Firefox may stay skipped in CI if ABI is incompatible (see `CLAUDE.md`). Still land the launcher and any test that can run here.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 Firefox waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official Firefox-launch / portable-test slice.
2. `feat` commit, `git push -u origin <branch>`.
3. Gate: Chromium **and** WebKit still green for touched tests. Firefox: run `PRODUCT=FIREFOX` when the executable exists; otherwise `Assert.Ignore` with a reason.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`.

| Slice | API |
|-------|-----|
| Executable | Discover Firefox path in `BrowserExecutableFixture` |
| Launcher | `BrowserLauncher` + `Playwright.LaunchFirefoxAsync` when `PRODUCT=FIREFOX` |
| Smoke | One portable Direct class green on Firefox, or documented ignore |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Do not add `TestExpectations` skips to hide a launcher bug.

## When exhausted

Hand off to `tasks/expect-options-campaign.md` using `tasks/campaign-chain.md`
(**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
