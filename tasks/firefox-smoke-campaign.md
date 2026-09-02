# Firefox smoke campaign

Wave 563–564 landed Firefox executable discovery and
`BrowserLauncher` → `LaunchFirefoxAsync`. Portable Direct tests still
`Assert.Ignore` when this Linux Juggler session closes during connect
(`Session disposed`). This campaign makes portable Direct tests **run** on
`PRODUCT=FIREFOX`.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/expect-options-campaign.md`
**Next campaign:** `tasks/tracing-chunks-campaign.md`

## Goal

- Fix the Firefox connect / handshake / session lifetime so Juggler stays
  open long enough for portable Direct tests
- Portable Direct tests that already run on CR+WK should run on Firefox, or
  `Assert.Ignore` with a **real** ABI/reason (not a blanket skip)
- Do **not** invent Firefox-only stub APIs
- Do **not** hide the connect bug with `TestExpectations` skips

Linux CI may still skip Firefox if ABI is incompatible (see `CLAUDE.md`).
Still land a launcher that works here when the executable exists.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 Firefox-smoke waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official Firefox connect / portable-test slice.
2. `feat` commit, `git push -u origin <branch>`.
3. Gate: Chromium **and** WebKit still green for touched tests. Firefox: run
   `PRODUCT=FIREFOX` when the executable exists; Ignore only with a real
   ABI/reason.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md` after expect-options is exhausted.

| Wave | Slice |
|------|--------|
| **573** | Keep the Juggler session alive through `LaunchFirefoxAsync` / connect |
| **574** | One portable Direct class actually runs on Firefox (not Ignore) |
| **575** | Additional portable Direct class (`DirectBrowserTypeLaunchTests`) |
| **576** | Additional portable Direct class (`DirectLaunchArgsTests`) |
| **577** | Additional portable Direct class (`DirectLaunchTimeoutTests`) |
| **578** | Additional portable Direct class (`DirectLaunchHandleSigintTests`) |
| **579** | Additional portable Direct class (`DirectLaunchHandleSigtermTests`) |
| **580** | Additional portable Direct class (`DirectLaunchHandleSighupTests`) |
| **581** | Document remaining persistent Direct tests with a real `LaunchPersistentContext` ABI ignore |

Portable launch/handshake classes that can run on this Juggler are green.
Persistent / downloads / artifacts remain Ignore with a real ABI reason.
This campaign is exhausted. Follow **Next campaign**.

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. Do not add `TestExpectations`
skips to hide a connect bug.

## When exhausted

Hand off to `tasks/tracing-chunks-campaign.md` using `tasks/campaign-chain.md`
(**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
