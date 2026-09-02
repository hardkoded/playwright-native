# Locator campaign

Full Playwright compatibility starts with `ILocator`. The leftover options campaign ended at Wave 500. This campaign adds the official locator surface on Chromium and WebKit.

If you were started to keep moving, **do not stop after one wave** and **do not wait for another prompt**. Follow this file, then `tasks/todo.md`.

**Previous campaign:** `tasks/leftover-campaign.md`
**Next campaign:** `tasks/expect-campaign.md`

## Goal

`ILocator` / `IFrameLocator` matching official Playwright .NET:

- Lazy: re-query the DOM on every action
- Strict: an action throws when more than one element matches
- Chainable: `First`, `Last`, `Nth`, nested `Locator`, later `Filter` / `And` / `Or`
- `GetBy*` return locators (keep existing handle overloads)
- Then `AddLocatorHandlerAsync`, screenshot `mask`, `IFrameLocator`

Do **not** invent a second locator model. Build on `IFrame.QuerySelectorAllAsync` and `IElementHandle` actions.

## Start of every run

1. `git fetch origin main` and `git checkout main && git pull origin main`.
2. Read **Current Phase** in `tasks/todo.md`.
3. If another `cursor/*-554a` branch on `origin` was pushed in the last 6 hours and is **not** merged to `main`, stop.
4. Create `cursor/<descriptive-name>-554a` from latest `main`.
5. Implement the next locator waves, one wave at a time.

## Per-wave loop

Do **not** start wave N+1 until wave N is on `origin/main`.

1. Implement one locator slice with Direct CR + WK tests.
2. `feat` commit, `git push -u origin <branch>`.
3. Chromium **and** WebKit tests green.
4. Docs commit: check the box, set Current Phase to NNN+1.
5. Fast-forward merge to `main` and `git push origin main`.

No pull requests.

## Wave plan

| Wave | Slice |
|------|--------|
| **501** | `ILocator` + `IPage.Locator` / `IFrame.Locator`, First/Last/Nth, nested Locator, Count/All, Click/Fill/TextContent/ElementHandle |
| **502** | Hover, DblClick, Focus, Tap |
| **503** | Check, Uncheck, SetChecked, IsChecked |
| **504** | Visibility / enabled / editable / hidden / disabled queries |
| **505** | GetAttribute, InnerText, InnerHTML, InputValue, Press, Type |
| **506** | `GetBy*` locator overloads on page/frame (keep handle methods) |
| **507** | Filter / And / Or / Has |
| **508** | `IFrameLocator` |
| **509** | Screenshot mask (locator-shaped) |
| **510** | `AddLocatorHandlerAsync` |
| **511** | `GetBy*` on `ILocator` (descendants of the locator) |
| **512** | `Filter` `hasNot` / `hasNotText` |
| **513** | `ILocator.WaitFor` / `Clear` / `SelectOption` |
| **514** | `Evaluate` / `EvaluateAll` / `BoundingBox` |
| **515** | `SetInputFiles` / `Screenshot` / `DispatchEvent` |
| **516** | `ScrollIntoViewIfNeeded` / `Blur` / `SelectText` |
| **517** | `AllInnerTexts` / `AllTextContents` / `EvaluateHandle` |
| **518** | `ILocator.FrameLocator` |
| **519** | `IFrameLocator` `GetBy*` |
| **520** | `ILocator.DragTo` |
| **521** | `Highlight` / `PressSequentially` |
| **522** | `ILocator.Describe` |
| **523** | `ILocator.AriaSnapshot` |

Locator core is on `main` through Wave 523. **Next campaign:** `tasks/expect-campaign.md`.

Inspector `PauseAsync` is `tasks/pause-campaign.md` (after screenshot expect).

## Conventions

Same C# rules as `tasks/leftover-campaign.md` (CRLF, `ConfigureAwait(false)`, no `var` for built-ins, `_camelCase`, CS1574 crefs, NUnit `CatchAsync`).

Class `Locator` cannot declare a method named `Locator` (CS0542). Implement `ILocator.Locator` explicitly.

`EvaluateAsync` is raw `Runtime.evaluate` — do not wrap page scripts in unused arrow functions; use statements or a called IIFE.

Kill leftover Chrome before CR runs. Local gate is **CR + WK**. Firefox may `Assert.Ignore`.
