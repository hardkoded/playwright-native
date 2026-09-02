# Client certificates campaign

Leftover Wave 500 skipped `clientCertificates` as too invasive. This campaign ports the official option on the direct Chromium and WebKit stacks.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/pause-campaign.md`
**Next campaign:** `tasks/har-update-campaign.md`

## Goal

Official Playwright:

- `ClientCertificate` (origin, cert/key or pfx, passphrase)
- `BrowserNewContextOptions.ClientCertificates`
- `BrowserTypeLaunchPersistentContextOptions.ClientCertificates` if upstream has it

Wire through CR and WK context create. WebKit may `Assert.Ignore` with a reason if the stack cannot present a client cert. Do **not** invent a custom TLS helper that is not on the official public surface.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If Current Phase names another playbook, follow that file instead.
4. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
5. Create `cursor/<descriptive-name>-554a` from latest `main`.
6. Implement the next **50 client-certificate waves** (or until exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one official cert slice with Direct CR + WK tests (HTTPS test server / `ignoreHTTPSErrors` as upstream does).
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green (or WK ignore with a clear reason).
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

Continue Wave numbers from `tasks/todo.md`.

| Slice | API |
|-------|-----|
| Model | `ClientCertificate` public type |
| Context | `NewContextAsync` / `BrowserNewContextOptions.ClientCertificates` |
| Persistent | persistent-context option if official |

## Conventions

Same C# rules as `tasks/leftover-campaign.md`. HTTPS fixtures: `dotnet dev-certs https -ep src/PlaywrightSharp.TestServer/testCert.cer` when needed.

## When exhausted

Hand off to `tasks/har-update-campaign.md` using `tasks/campaign-chain.md` (**Handoff**). Do not stop the run.

## Automation prompt

Paste the prompt in `tasks/campaign-chain.md`.
