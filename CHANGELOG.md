# Changelog

## Unreleased — Direct browser architecture (Breaking)

This release rewrites the internals from a Node.js-driver-based architecture to a
**pure .NET implementation** that talks to browsers over their native protocols
(CDP, Juggler, WebKit Inspection Protocol). The external API surface is the
direct launch entry points plus the retained page/context/frame contracts.

### Breaking changes

#### Entry point

`Playwright.CreateAsync()` and the driver-era browser-type factory are removed.
The old `LaunchChromiumDirectAsync` name is also gone — use `LaunchChromiumAsync`,
`LaunchFirefoxAsync`, and `LaunchWebkitAsync`.

```csharp
// Before (removed)
using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync();

// After
await using IBrowser browser = await Playwright.LaunchChromiumAsync();
```

When `ExecutablePath` is omitted, `BrowserFetcher` downloads the pinned browser
build into the local cache (`PLAYWRIGHT_BROWSERS_PATH` if set).

#### Removed interfaces

- `IPlaywright` — replaced by the `Playwright` static class
- Driver-era `IBrowserType` obtained from `CreateAsync()` — replaced by
  `Playwright.LaunchChromiumAsync` / `LaunchFirefoxAsync` / `LaunchWebkitAsync`
- `ILocator` / `IFrameLocator` — not yet implemented in the earliest direct cut
  (later waves restored locator work; see `tasks/todo.md`)
- `IPageAssertions` / `ILocatorAssertions` — not yet implemented in the earliest
  direct cut
- `ISelectors` — not yet implemented in the earliest direct cut

#### Trimmed IPage surface

Removed in the first direct-CDP cut (not yet implemented at that time):

| Removed | Reason |
|---|---|
| `BringToFrontAsync` | planned Phase 7+ |
| `DispatchEventAsync` | planned Phase 7+ |
| `EvalOnSelectorAsync` / `EvalOnSelectorAllAsync` | planned Phase 7+ |
| `ExposeBindingAsync` overloads | planned Phase 7+ |
| `Frame(name)` / `FrameByUrl` overloads | planned Phase 7+ |
| `GoBackAsync` / `GoForwardAsync` / `ReloadAsync` | planned Phase 7+ |
| `PauseAsync` | planned Phase 7+ |
| `QuerySelectorAllAsync` | planned Phase 7+ |
| `WaitForFunctionAsync` | planned Phase 7+ |
| `WaitForNavigationAsync` (overloads) | planned Phase 7+ |
| `WaitForRequestAsync` / `WaitForResponseAsync` | planned Phase 7+ |
| `WaitForSelectorAsync` | planned Phase 7+ |
| `WaitForTimeoutAsync` | planned Phase 7+ |
| `WaitForURLAsync` | planned Phase 7+ |
| Crash / Download / FileChooser / WebSocket / Worker events | planned Phase 7+ |
| `Accessibility` / `Video` / `Workers` properties | planned Phase 7+ |

Retained in that cut: `GotoAsync`, `ClickAsync`, `FillAsync`, `TypeAsync`, `PressAsync`,
`EvaluateAsync`, `EvaluateHandleAsync`, `QuerySelectorAsync`, `WaitForLoadStateAsync`,
`ScreenshotAsync`, `PdfAsync`, `RouteAsync`, `UnrouteAsync`, `ExposeFunction*`,
`AddScriptTagAsync`, `AddStyleTagAsync`, `AddInitScriptAsync`, `EmulateMediaAsync`,
`SetViewportSizeAsync`, `SetExtraHTTPHeadersAsync`, `SetInputFilesAsync`,
`SelectOptionAsync`, `CheckAsync`, `UncheckAsync`, `IsCheckedAsync`, `TapAsync`,
`DragAndDropAsync`, `Dialog` / `Popup` / `Request` / `Response` / `Load` events.

#### Microsoft.Playwright contracts

Public `IPage` / `IFrame` / `IElementHandle` / `IBrowserContext` (and related
models) now come from the `Microsoft.Playwright` package. In-repo generated
duplicates of those types were removed. PlaywrightNative-only extras stay in
`Contracts/` (`IAccessibility`, `ICoverage`, `IGenericAssertions`,
`BrowserContextOptions`, coverage models, and related enums).

#### Trimmed IFrame surface

Removed: `AddScriptTagAsync`, `AddStyleTagAsync`, `DispatchEventAsync`,
`EvalOnSelector*`, `FrameElementAsync`, `QuerySelectorAllAsync`, all `WaitFor*` methods.

#### Trimmed IElementHandle surface

Removed: `ContentFrameAsync`, `DispatchEventAsync`, `EvalOnSelector*`,
`OwnerFrameAsync`, `QuerySelectorAsync`, `QuerySelectorAllAsync`, `ScreenshotAsync`,
`ScrollIntoViewIfNeededAsync`, `SelectTextAsync`, `WaitForElementStateAsync`,
`WaitForSelectorAsync`.

#### Trimmed IBrowserContext surface

Removed: `AddCookiesAsync`, `ClearCookiesAsync`, `GetCookiesAsync`,
`GrantPermissionsAsync`, `ClearPermissionsAsync`, `SetGeolocationAsync`,
`SetOfflineAsync`, `StorageStateAsync`, `ExposeBindingAsync`, `ExposeFunctionAsync`,
`WaitForEventAsync`, `WaitForPageAsync`.

### What's new

- **Zero Node.js dependency** — no driver process, no PowerShell install scripts
- **Direct protocols** — Chromium (`CRBrowser` / `CRPage` / `CRNetworkManager` over CDP),
  Firefox (Juggler), and WebKit (Inspection Protocol)
- **`Playwright.LaunchChromiumAsync` / `LaunchFirefoxAsync` / `LaunchWebkitAsync`** —
  static entry points; browsers download via `BrowserFetcher` when needed
- Chromium is the primary CI target; WebKit runs in CI; Firefox launches today
  but is not in the CI matrix yet

### Roadmap

- Phase 7 (Firefox launch) and Phase 8 (WebKit launch) have landed — they are
  not future-only work
- Remaining work is tracked on GitHub (cleanup, packaging, docs, and
  browser-specific gaps). This changelog does not invent a new roadmap
