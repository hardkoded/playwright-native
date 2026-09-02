# Expect campaign

Locator core landed through Wave 523. This campaign adds official `expect(locator)` assertions on Chromium and WebKit.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/locator-campaign.md`
**Next campaign:** `tasks/screenshot-campaign.md`

## Goal

`Assertions.Expect(ILocator)` matching official Playwright .NET:

- Poll until the assertion passes or the timeout expires
- Strict locator actions stay on `ILocator`; expect uses non-waiting queries (`IsVisibleAsync`, `CountAsync`, …)
- Later: `Not`, text/attribute/value, enabled/editable/checked

Do **not** invent a second assertion model. Reuse locator queries.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
4. Create `cursor/<descriptive-name>-554a` from latest `main`.
5. Implement the next **50 expect waves** (or until the campaign is exhausted), one wave at a time. If this campaign ends mid-run, hand off to **Next campaign**.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one assertion slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

| Wave | Slice |
|------|--------|
| **524** | `Assertions.Expect` + `ToBeVisible` / `ToBeHidden` / `ToHaveCount` |
| **525** | `Not` + `ToBeEnabled` / `ToBeDisabled` / `ToBeEditable` / `ToBeChecked` |
| **526** | `ToHaveText` / `ToHaveAttribute` / `ToHaveValue` / `ToHaveId` |
| **527** | `ToBeAttached` / `ToBeFocused` |
| **528** | `ToHaveClass` / `ToHaveCSS` |
| **529** | `ToHaveJSProperty` / `ToBeInViewport` |
| **530** | `ToHaveRole` / `ToHaveAccessibleName` |
| **531** | `ToHaveAccessibleDescription` / `ToMatchAriaSnapshot` |
| **532** | `ToBeEmpty` / `ToContainText` |
| **533** | `ToContainClass` / `ToHaveValues` |
| **534** | `ToHaveAccessibleErrorMessage` / `ToContainText` list |
| **535** | `Assertions.Expect(IPage)` + `ToHaveTitle` / `ToHaveURL` |
| **536** | `ToHaveTitle(Regex)` / `ToHaveURL(Regex)` |
| **537** | `IPageAssertions.ToMatchAriaSnapshot` |
| **538** | `Assertions.Expect(IAPIResponse).ToBeOK` |
| **539** | `ToHaveText(Regex)` / `ToContainText(Regex)` |
| **540** | `ToHaveAttribute(Regex)` / `ToHaveValue(Regex)` |
| **541** | `ToHaveId(Regex)` / `ToHaveCSS(Regex)` |
| **542** | `ToHaveClass(Regex)` / `ToHaveAccessibleName(Regex)` |
| **543** | `ToHaveAccessibleDescription(Regex)` / `ToHaveAccessibleErrorMessage(Regex)` |
| **544** | `ToHaveText` list / `ToHaveText(IEnumerable<Regex>)` |
| **545** | `ToHaveClass` list / `ToHaveClass(IEnumerable<Regex>)` |
| **546** | `ToContainText(IEnumerable<Regex>)` / `ToHaveValues(IEnumerable<Regex>)` |
| **547** | `ToContainClass(IEnumerable<string>)` |
| **548** | `ToPass` (already on `main`; do not invent more unofficial matchers) |
| **549** | `Assertions.SetDefaultExpectTimeout` |

The expect matcher hunt is exhausted after Wave 549. Do **not** invent filler
assertions. Option bags (`ignoreCase`, `useInnerText`, `checked: false`) are
`tasks/expect-options-campaign.md` (Wave 565+). **Next campaign** after Wave
549 was `tasks/screenshot-campaign.md` (already complete).

## Conventions

Same C# rules as `tasks/locator-campaign.md` (CRLF, `ConfigureAwait(false)`, no `var` for built-ins, `_camelCase`, NUnit `CatchAsync`).

Kill leftover Chrome before CR runs. Local gate is **CR + WK**. Firefox may `Assert.Ignore`.

## When exhausted

Hand off to `tasks/screenshot-campaign.md` using `tasks/campaign-chain.md` (**Handoff**). Do not stop the run.

## Automation prompt (for cursor.com/automations)

Paste the **chain** prompt in `tasks/campaign-chain.md` (not an expect-only prompt). That is what lets one schedule walk leftover → locator → expect → screenshot → pause → certs → HAR → Firefox → expect-options → firefox-smoke → tracing-chunks → UnrouteBehavior.
