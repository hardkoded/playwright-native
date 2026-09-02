# PlaywrightNative Architecture

## Project Goal

Replace the external Node.js Playwright driver process with native C# implementations
that connect directly to browsers using their native protocols (CDP for Chromium,
Juggler for Firefox, WebKit Inspection Protocol for WebKit).

**Why**: Eliminates Node.js runtime dependency, reduces latency (no interprocess
serialization hop), gives full control, and dramatically reduces package size.

That replacement is done. Public launch entry points are
`Playwright.LaunchChromiumAsync`, `LaunchFirefoxAsync`, and `LaunchWebkitAsync`.

## GitHub Tracking

- **Project**: https://github.com/orgs/hardkoded/projects/4
- **Issues**: hardkoded/playwright-sharp#1 through hardkoded/playwright-sharp#10
  (driver-removal phases). Later work is tracked as follow-up issues.

## Current Architecture

```
User Code → Playwright.LaunchChromiumAsync / LaunchFirefoxAsync / LaunchWebkitAsync
         → IPage / IBrowser (public API)
         → CR* / FF* / WK* implementations (and adapters)
         → CRSession / FFSession / WKSession
         → CRConnection / FFConnection / WKConnection
         → IConnectionTransport (PipeTransport or WebSocketTransport)
         → Browser process (BrowserProcessManager)
```

`Transport/` is browser pipe and WebSocket I/O (`PipeTransport`,
`WebSocketTransport`, `BrowserProcessManager`, `InheritablePipes`). It is not
leftover Node-driver or Channel RPC plumbing.

`BrowserFetcher` downloads pinned browser builds when `ExecutablePath` is omitted.

### Directory layout (library)

```
src/PlaywrightNative/
  ├── Transport/
  │   ├── Protocol/               # ProtocolRequest, ProtocolResponse
  │   ├── WebSocketTransport.cs   # WebSocket-based transport
  │   ├── PipeTransport.cs        # Pipe-based transport
  │   ├── BrowserProcessManager.cs
  │   └── IConnectionTransport.cs
  ├── Chromium/                   # CRConnection, CRSession, CRBrowser, CRPage, ...
  ├── Firefox/                    # FFConnection, FFSession, FFBrowser, FFPage, ...
  ├── WebKit/                     # WKConnection, WKSession, WKBrowser, WKPage, ...
  ├── Helpers/
  └── Contracts/
```

Chromium is the primary, fully supported engine. Firefox and WebKit launch today;
Firefox is not in CI yet.

## Archived: Channel + Node driver (removed)

The pre-removal architecture spawned a Node.js process and spoke length-prefixed
JSON over stdin/stdout:

```
User Code → Channel classes (RPC) → Connection → StdIOTransport → Node.js Driver → Browser
```

Those pieces are gone from the tree:

- `Transport/Connection.cs`, `StdIOTransport.cs`, `Channels/`, Channel initializers
- Bundled Node runtime under `Drivers/`
- Driver-spawning entry point on `Playwright`

Do not treat the block above as current state.

## Driver-removal phases (historical)

Completed. Not a new roadmap.

| Phase | Issue | Description | Depends On |
|-------|-------|-------------|------------|
| 0 | #1 | Transport Foundation (WebSocket, Pipe, ProcessManager) | — |
| 1 | #2 | Chromium Connection (CRConnection, CRSession, CDP types) | Phase 0 |
| 2 | #3 | Chromium Page Creation (CRPage, IPageDelegate) | Phase 1 |
| 3 | #4 | Navigation & JS Evaluation (CRExecutionContext, Frames) | Phase 2 |
| 4 | #5 | Network & Request Interception (CRNetworkManager) | Phase 3 |
| 5 | #6 | Input, Screenshots, Remaining APIs | Phase 4 |
| 6 | #7 | Entry Point Refactor (remove driver dependency) | Phase 5 |
| 7 | #8 | Firefox (Juggler Protocol) | Phase 6 |
| 8 | #9 | WebKit (Inspection Protocol) | Phase 7 |
| 9 | #10 | Cleanup & Polish | Phase 8 |

Phases 7 and 8 (Firefox / WebKit launch) have landed. Remaining polish is
tracked on GitHub, not as a new phase table here.

## Reference Codebases

### Old PlaywrightNative (direct connections)
**Path**: `../../hardkoded/playwright-sharp-old/`

Useful for C#-specific patterns:
- `src/PlaywrightNative.Abstractions/Transport/WebSocketTransport.cs` — WebSocket transport
- `src/PlaywrightNative.Chromium/ChromiumProcessManager.cs` — process lifecycle state machine
- `src/PlaywrightNative.Chromium/ChromiumConnection.cs` — CDP connection routing
- `src/PlaywrightNative.Chromium/ChromiumSession.cs` — CDP session handling
- `src/PlaywrightNative.Firefox/FirefoxConnection.cs` — Firefox connection
- `src/PlaywrightNative.Abstractions/` — shared abstractions

### Upstream Playwright (TypeScript canonical implementation)
**Path**: `../../microsoft/playwright/`

The source of truth for browser communication logic:
- `packages/playwright-core/src/server/transport.ts` — WebSocket transport
- `packages/playwright-core/src/server/pipeTransport.ts` — pipe transport
- `packages/playwright-core/src/server/browserType.ts` — browser launch logic
- `packages/playwright-core/src/server/browser.ts` — abstract browser base
- `packages/playwright-core/src/server/page.ts` — shared page logic
- `packages/playwright-core/src/server/frames.ts` — frame management
- `packages/playwright-core/src/server/chromium/crConnection.ts` — CDP connection
- `packages/playwright-core/src/server/chromium/crBrowser.ts` — Chromium browser
- `packages/playwright-core/src/server/chromium/crPage.ts` — Chromium page delegate
- `packages/playwright-core/src/server/chromium/crNetworkManager.ts` — network
- `packages/playwright-core/src/server/chromium/crExecutionContext.ts` — JS eval
- `packages/playwright-core/src/server/chromium/crInput.ts` — input
- `packages/playwright-core/src/server/firefox/ffConnection.ts` — Firefox connection
- `packages/playwright-core/src/server/firefox/ffBrowser.ts` — Firefox browser
- `packages/playwright-core/src/server/firefox/ffPage.ts` — Firefox page
- `packages/playwright-core/src/server/webkit/wkConnection.ts` — WebKit connection
- `packages/playwright-core/src/server/webkit/wkBrowser.ts` — WebKit browser
- `packages/playwright-core/src/server/webkit/wkPage.ts` — WebKit page

## Protocol Details

### Chromium (CDP)
- **Transport**: WebSocket (`--remote-debugging-port=0`) or Pipe (`--remote-debugging-pipe`)
- **Endpoint discovery**: Regex on stderr: `"DevTools listening on (ws://...)"`
- **Session model**: SessionId-based multiplexing — root session + child sessions per target
- **Key initial commands**: `Browser.getVersion`, `Target.setAutoAttach`, `Target.createBrowserContext`

### Firefox (Juggler)
- **Transport**: Pipe only (`-juggler-pipe`)
- **Endpoint discovery**: Wait for "Juggler listening to the pipe" in logs
- **Session model**: Root session + child sessions per page
- **Key initial commands**: `Browser.enable` (with firefoxUserPrefs), `Browser.createBrowserContext`

### WebKit (Inspection Protocol)
- **Transport**: Pipe only (`--inspector-pipe`)
- **Session model**: `pageProxyId`-based routing (different from sessionId)
- **Key initial commands**: `Playwright.enable`, `Playwright.createContext`
- **Special**: provisional page target swap for cross-process navigation

## Historical risks (driver-removal era)

These were the original removal risks. Kept for context; they are not a new plan.

| Risk | Mitigation used |
|------|-----------------|
| Pipe transport on .NET (upstream uses fd 3/4) | WebSocket first on Chromium; pipe for Firefox/WebKit |
| Large refactor scope | Phased issues #1–#10 |
| Protocol type generation | Port used methods; generate where practical |
| Concurrency (driver was single-threaded) | ConcurrentDictionary, dedicated dispatch task |
| WebKit complexity (pageProxyId, provisional pages) | Phase 8 |

## How to use this document

1. Treat **Current Architecture** as the tree.
2. Treat **Archived** and **Driver-removal phases** as history.
3. For new work, read the GitHub issue — do not invent a roadmap here.
4. Upstream TypeScript under `packages/playwright-core/src/server/` remains the
   protocol reference.
