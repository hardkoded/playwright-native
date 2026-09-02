# CLAUDE.md

## Goal

PlaywrightNative is a .NET port of Microsoft Playwright for automating Chromium, Firefox, and WebKit browsers.
Inspired by the original Playwright library, adapted to C# idioms and .NET practices.

Public launch entry points are `Playwright.LaunchChromiumAsync`, `LaunchFirefoxAsync`,
and `LaunchWebkitAsync`. There is no Node.js driver.

## Compatibility campaigns

Leftover options ended at Wave 500 (`tasks/leftover-campaign.md`).
Locator core ended at Wave 523 (`tasks/locator-campaign.md`).
Expect matchers ended at Wave 549 (`tasks/expect-campaign.md`).
Screenshot, pause, client certificates, HAR update, Firefox launcher,
expect options, Firefox smoke, tracing chunks, and UnrouteBehavior
are on `main` (through Wave 586).
The active track is IgnoreDefaultArgs list: follow
`tasks/ignore-default-args-campaign.md` and `tasks/todo.md` Current Phase.
Campaign order and the paste-ready automation prompt: `tasks/campaign-chain.md`.
After IgnoreDefaultArgs: Firefox persistent.
Then stop.

## Quick Reference

```bash
# Build
dotnet build ./src/PlaywrightNative.sln

# Chromium — matches CI (ubuntu/windows, headless + headful)
PRODUCT=CHROMIUM dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj -f net10.0 -s src/PlaywrightNative.Tests/test.runsettings

# WebKit — matches CI (macos-14 + ubuntu, headless)
PRODUCT=WEBKIT dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj -f net10.0 -s src/PlaywrightNative.Tests/test.runsettings

# Firefox — launches today; not in the CI matrix
PRODUCT=FIREFOX dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj -f net10.0 -s src/PlaywrightNative.Tests/test.runsettings

# Run a single test class
PRODUCT=CHROMIUM dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj --filter "ClassName=PlaywrightNative.Tests.PlaywrightTests" -f net10.0

# Run a single test method
PRODUCT=CHROMIUM dotnet test ./src/PlaywrightNative.Tests/PlaywrightNative.Tests.csproj --filter "FullyQualifiedName~ShouldCreateNewPage" -f net10.0

# Lint (run before pushing — CI enforces this)
dotnet format whitespace ./src/PlaywrightNative.sln --verify-no-changes && dotnet format style ./src/PlaywrightNative.sln --verify-no-changes
```

**Environment variables**: `PRODUCT` (CHROMIUM/FIREFOX/WEBKIT), `HEADLESS` (true/false), `SLOW_MO` (ms delay).
Always set `PRODUCT` explicitly when running tests.

**HTTPS tests** require: `dotnet dev-certs https -ep src/PlaywrightNative.TestServer/testCert.cer`

## C# Code Style (Error-Level Rules)

These rules cause build failures. Violating them will break CI:

| Rule | Requirement |
|------|-------------|
| `TreatWarningsAsErrors` | All warnings are errors in library project (`PlaywrightNative.csproj`) |
| `AnalysisMode=AllEnabledByDefault` | All code analysis rules active by default (StyleCop 1.2, Roslynator 4.12, VSTHRD analyzers) |
| `SX1309` (error) | Private fields must use `_camelCase` prefix |
| `CA2007` (error) | `ConfigureAwait(false)` required on every `await` |
| `VSTHRD200` (error) | Async methods must have `Async` suffix |
| `CA1725` (error) | Parameter names must match base declaration |
| `CA1823` (error) | No unused private fields |
| `csharp_style_var_for_built_in_types` (error) | **Never use `var` for built-in types** (`int`, `string`, `bool`, etc.) — use explicit types |
| `dotnet_style_readonly_field` (error) | Fields that are never reassigned must be `readonly` |
| `SA1204/SA1202` | Static members before instance members. Never place a static method after instance methods |
| XML docs | Required on all public APIs (CS1591) |
| No "puppeteer" | CI script checks that no `.cs` file references "puppeteer" |
| Line endings | CRLF required on all `.cs` files (enforced by `.editorconfig`) |

### Style Preferences (Not Error-Level)

- `var` elsewhere: allowed by suggestion, but prefer explicit types when type isn't apparent.
- 4-space indentation, Allman braces, `using` directives outside namespace.
- `system` usings sorted first.
- Expression-bodied members preferred as refactoring suggestion.
- Avoid `this.` qualification.

### LangVersion

`Directory.Build.props` sets `<LangVersion>latest</LangVersion>`. Modern C# features are available.

## Architecture

### Project Structure

```
PlaywrightNative/                    ← Main library (netstandard2.1 + net10.0)
  ├── Chromium/                     ← Direct CDP (CRPage, CRNetworkManager, ...)
  ├── Firefox/                      ← Direct Juggler (FFPage, FFConnection, ...)
  ├── WebKit/                       ← Direct WIP (WKPage, WKConnection, ...)
  ├── Transport/                    ← Browser pipe / WebSocket I/O (not a driver)
  ├── Helpers/                      ← Internal utilities
  └── Contracts/                    ← Interface definitions

PlaywrightNative.NUnit/              ← NUnit fixtures + PlaywrightTestAttribute
  ├── PageTest.cs / ContextTest.cs / BrowserTest.cs / PlaywrightTest.cs
  ├── WorkerAwareTest.cs / SkipAttribute.cs
  └── TestExpectations/             ← JSON skip/fail expectations per browser/platform

PlaywrightNative.Tests/              ← Test suite (NUnit 4.1, net10.0)
  ├── BaseTests/                    ← BrowserLauncher, BrowserExecutableFixture, PageTestEx
  ├── Chromium/                     ← CRTestBase protocol-level Chromium tests
  └── *.cs                          ← Public-API tests plus fetcher / installer unit tests

PlaywrightNative.TestServer/         ← HTTP/HTTPS server for test fixtures
```

### Key Design Patterns

- **Direct browser connections**: `Playwright.LaunchChromiumAsync` / `LaunchFirefoxAsync` /
  `LaunchWebkitAsync` spawn the browser and speak its native protocol. `Transport/` is
  `PipeTransport`, `WebSocketTransport`, and `BrowserProcessManager` — browser I/O, not
  leftover Node-driver plumbing.
- **InternalsVisibleTo**: The main library exposes internals to tests via strong-name signed assemblies (keys in `src/keys/`).
- **ConfigureAwait(false) everywhere**: All async code uses `.ConfigureAwait(false)` to avoid deadlocks in sync-over-async scenarios.
- **Typed protocol messages**: Do not poke at `JsonElement` with `TryGetProperty` for protocol messages with a known shape. Create a proper type for the message (a class/record with `[JsonPropertyName]` for the wire names) and deserialize into it. Reserve raw `JsonElement` for genuinely dynamic/unknown-shape payloads (e.g. by-value evaluation results).

### Test Structure

- **Framework**: NUnit 4.1 with `Microsoft.NET.Test.Sdk 17.12`.
- **NUnit fixtures**: `PlaywrightTest` → `BrowserTest` → `ContextTest` → `PageTest`
  in `PlaywrightNative.NUnit`. Tests wrap those as `PlaywrightTestEx` / `BrowserTestEx` /
  `ContextTestEx` / `PageTestEx` (adds the shared HTTP/HTTPS servers). The old
  `PlaywrightNativeBaseTest` hierarchy is gone.
- **Chromium protocol tests**: `Chromium/CRTestBase` launches via `ChromiumBrowserType` and
  exercises `CRPage` / `CRBrowser` directly.
- **`[PlaywrightTest]` attribute**: Links each test to its upstream spec file. Also implements the test expectations system — checks `TestExpectations.local.json` to skip/fail tests per browser/platform/mode.
- **`[Skip]`**: `PlaywrightNative.NUnit.SkipAttribute` skips by browser/OS flags. Also see
  `BrowserLauncher.SkipUnlessChromium()` for Chromium-only Direct tests.
- **Test pattern**: `[PlaywrightTest("spec-file.ts", "test name")]` + `[Test, Timeout(TestConstants.DefaultTestTimeout)]`.
- **Assertions**: NUnit constraint model — `Assert.That(value, Is.EqualTo(...))`, `Does.Contain(...)`, `Has.Exactly(n).Items`, etc.
- **`TestConstants`**: Provides server URLs (port 8081/8082), browser product detection, default launch/timeout options.
- **Test expectations**: `TestExpectations.local.json` in `PlaywrightNative.NUnit` controls which tests are expected to skip/fail/pass per browser+platform+mode combination. Tests marked `skip`/`fail`/`timeout` are automatically skipped via `PlaywrightTestAttribute`.
- **Golden rule**: Tests must always match upstream. Never modify tests to match local code.

### CI Matrix

CI (`.github/workflows/dotnet.yml`) runs `net10.0` with `test.runsettings` (1-hour
session timeout). Matrix:

- Chromium headless + headful on `ubuntu-latest` and `windows-latest`
  (`PRODUCT=CHROMIUM`, full `PlaywrightNative.Tests` suite)
- WebKit headless on `macos-14` and `ubuntu-latest`
  (`PRODUCT=WEBKIT`, full suite; known gaps live in `TestExpectations.local.json`)

Firefox is not in the CI matrix. Browser binaries come from `BrowserFetcher`,
cached under `PLAYWRIGHT_BROWSERS_PATH`.

## Upstream Porting Conventions

When porting from upstream TypeScript to C#:

| TypeScript | C# |
|------------|-----|
| `camelCase` | `PascalCase` (public), `_camelCase` (private fields) |
| `Promise<T>` | `Task<T>` |
| interfaces | Prefix with `I` |
| events | C# event pattern (`event EventHandler<T>`) |
| getter functions | Property accessors |
| `async function foo()` | `async Task FooAsync()` |

If the upstream file contains "Copyright (c) Microsoft Corporation.", add that copyright to the local file.

### Mandatory rules

- **Every test inspired by the upstream repo must carry a `[PlaywrightTest("<spec-file>.spec.ts", "<original test name>")]` attribute** targeting the original upstream test it is derived from. Name the C# method as the PascalCase form of the upstream test name.
- **Every file generated by reading upstream code must include `* Modifications copyright (c) Microsoft Corporation.` in its header**, even when the new file does not map 1:1 to a single upstream file.

## External Sources

These repositories are used as reference. You may run git commands to update them locally.

- **upstream**: `../../microsoft/playwright` — canonical Playwright Node.js implementation. Upstream code: `packages/playwright-core/src/server/`. Upstream tests: `tests/library/` and `tests/page/`.
- **upstream-dotnet**: `../../microsoft/playwright-dotnet` — Microsoft's official .NET Playwright client. Reference for .NET API conventions.

When porting, check both upstream (canonical logic) and upstream-dotnet (.NET-idiomatic patterns).

## Git Workflow

- Always verify which branch you are on before commits or PRs.
- Never commit to main/master.
- Create a new branch immediately when starting work. Confirm with `git branch --show-current`.

## Testing & Debugging

- Investigate flaky test root causes (race conditions, non-deterministic ordering, frame assumptions). Never skip tests or add them to expected failures as a workaround.
- When running tests locally on macOS, WebKit may need extra native deps. Chromium is the primary target for local development.

## Continuous Improvement

When you encounter a new code style error during builds (SA/CA rule failures), update the "C# Code Style" section above with the new rule and how to comply. This keeps the document accurate as the codebase evolves.
