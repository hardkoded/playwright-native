# microsoft/playwright-dotnet: Community Pain Points Report

**Date:** April 7, 2026
**Source:** GitHub Issues analysis (open + closed) from microsoft/playwright-dotnet
**Total open issues analyzed:** 73 | **Closed issues sampled:** 100+ (by reactions)

---

## Executive Summary

The microsoft/playwright-dotnet repository shows **severe community neglect**:

- **89% of open issues** have received zero maintainer response
- **92% of open bugs** have no maintainer response
- The top 2 issues (70 and 53 reactions) have been open for **3.5+ and 4+ years** respectively
- Feature parity with Node.js Playwright is widening, not narrowing
- Users are openly asking "Is this repository maintained?" (Feb 2026, no response)
- The `P3-collecting-feedback` label is used on 43/73 open issues as a de facto "will not act" designation

---

## 1. Installation & Developer Experience (The #1 Pain Point)

**Combined reactions: ~96 | Issues: 5+ | Oldest: 3.5 years**

This is the single largest source of frustration. The current workflow — install NuGet, build, find `playwright.ps1` buried in `bin/`, run PowerShell — is completely alien to .NET developers.

| Issue | Reactions | Age | Maintainer Response |
|-------|-----------|-----|---------------------|
| [#2286](https://github.com/microsoft/playwright-dotnet/issues/2286) - More intuitive setup/installation | **70** | 3.5 yr | Yes (day 1 only, then silence) |
| [#2239](https://github.com/microsoft/playwright-dotnet/issues/2239) - InstallWhenNeeded option | **13** | 3.8 yr | None |
| [#2317](https://github.com/microsoft/playwright-dotnet/issues/2317) - Custom driver path (security) | **10** | 3.5 yr | None |
| [#3281](https://github.com/microsoft/playwright-dotnet/issues/3281) - Install fails with self-signed cert | recent | recent | None |
| [#3272](https://github.com/microsoft/playwright-dotnet/issues/3272) - Project-less installation | recent | recent | None |

**Key quote** from Microsoft's own `timheuer` (MEMBER): *"In a $100 survey for Playwright for .NET I'd use $70 of my budget for this."*

**Root cause:** The installation process was designed around the Node.js ecosystem's conventions (postinstall scripts, global caches) and never adapted to .NET conventions (NuGet restore, MSBuild targets, dotnet tools).

---

## 2. Node.js Dependency (The Architectural Root Cause)

**Combined reactions: ~65 | Issues: 4+ | Oldest: 4.3 years**

The bundled Node.js runtime is the root cause of multiple top issues. It causes CI/CD bloat, blocks AOT compilation, creates security audit failures, and makes the library feel un-.NET.

| Issue | Reactions | Age | Maintainer Response |
|-------|-----------|-----|---------------------|
| [#1850](https://github.com/microsoft/playwright-dotnet/issues/1850) - Remove Node.js from package | **53** | 4.3 yr | Yes (day 5 only) |
| [#2714](https://github.com/microsoft/playwright-dotnet/issues/2714) - AOT compilation support | **8** | 2.5 yr | Yes (after 6 months) |
| [#3271](https://github.com/microsoft/playwright-dotnet/issues/3271) - Connect to remote without Node | **4** | 3 mo | None |
| [#3260](https://github.com/microsoft/playwright-dotnet/issues/3260) - Trimmed .NET 10 publish broken | recent | recent | None |

**Impact examples from community:**
- One team reported **300GB/month of build artifacts** primarily from Node.js binaries being copied across project references
- Enterprise environments hit EPERM errors because Node.js writes to locked-down file paths
- AOT, trimming, and single-file publish are all impossible due to Node.js dependency

---

## 3. Feature Parity with Node.js Playwright (Widening Gap)

**Combined reactions: ~130+ | Issues: 10+ | Oldest: 4.3 years**

The .NET version is missing major features that have existed in Node.js Playwright for years. The community has described this as *"the whole Playwright Test section is missing."*

### Testing Features (Missing in .NET)

| Issue | Feature | Reactions | Age | Response |
|-------|---------|-----------|-----|----------|
| [#1854](https://github.com/microsoft/playwright-dotnet/issues/1854) | Visual snapshot testing (`toHaveScreenshot`) | **33** | 4.3 yr | 1 reply, day 2 |
| [#2214](https://github.com/microsoft/playwright-dotnet/issues/2214) | Screenshot/video/tracing on-failure | **24** | 3.7 yr | None |
| [#2316](https://github.com/microsoft/playwright-dotnet/issues/2316) | Test retries (implemented then reverted) | **23** | 3.5 yr | After 2.8 years |
| [#2328](https://github.com/microsoft/playwright-dotnet/issues/2328) | Soft assertions | **18** | 3.5 yr | None |
| [#2161](https://github.com/microsoft/playwright-dotnet/issues/2161) | Custom expect messages | **13** | 3.9 yr | None |
| [#2314](https://github.com/microsoft/playwright-dotnet/issues/2314) | Expect.Poll | **12** | 3.5 yr | None |
| [#2351](https://github.com/microsoft/playwright-dotnet/issues/2351) | CSS/JS code coverage API | **7** | 3.4 yr | None |
| [#2444](https://github.com/microsoft/playwright-dotnet/issues/2444) | Default action timeout in runsettings | **7** | 3.2 yr | None |

### Platform Features (Missing in .NET)

| Issue | Feature | Reactions | Age | Response |
|-------|---------|-----------|-----|----------|
| [#1178](https://github.com/microsoft/playwright-dotnet/issues/1178) | Android support | **20** | **5.1 yr** | None |
| [#3263](https://github.com/microsoft/playwright-dotnet/issues/3263) | Playwright Agents (AI/MCP) | **11** | 4 mo | None |
| [#2179](https://github.com/microsoft/playwright-dotnet/issues/2179) | Electron support | **9** | 3.8 yr | None |

### .NET-Idiomatic API Gaps

| Issue | Feature | Reactions | Age | Response |
|-------|---------|-----------|-----|----------|
| [#2715](https://github.com/microsoft/playwright-dotnet/issues/2715) | Synchronous API | **19** | 2.5 yr | None |
| [#3081](https://github.com/microsoft/playwright-dotnet/issues/3081) | Programmatic settings (not XML) | **10** | 1.3 yr | None |
| [#3237](https://github.com/microsoft/playwright-dotnet/issues/3237) | xUnit codegen support | **6** | 7 mo | None |

---

## 4. .NET 10 Compatibility (Emerging Crisis)

**Combined reactions: ~24+ | Issues: 5+ | All from last 6 months**

Multiple issues report that Playwright is broken or incompatible with .NET 10, the current LTS release:

| Issue | Problem |
|-------|---------|
| [#3255](https://github.com/microsoft/playwright-dotnet/issues/3255) | Docker images don't include .NET 10 SDK (24 reactions) |
| [#3268](https://github.com/microsoft/playwright-dotnet/issues/3268) | `playwright.ps1` broken on .NET 10 + ARM macOS |
| [#3291](https://github.com/microsoft/playwright-dotnet/issues/3291) | Deprecation errors on .NET 10 Windows |
| [#3269](https://github.com/microsoft/playwright-dotnet/issues/3269) | netcore8.0 compatibility issues |
| [#3260](https://github.com/microsoft/playwright-dotnet/issues/3260) | Trimmed publish fails (reflection-based serialization) |

None of these have received maintainer responses.

---

## 5. Bugs Closed Without Resolution

Multiple production-impacting bugs were closed as `NOT_PLANNED` without fixes:

| Issue | Bug | Reactions | Comments | Resolution |
|-------|-----|-----------|----------|------------|
| [#2641](https://github.com/microsoft/playwright-dotnet/issues/2641) | "Process exited" crashes in production | -- | 7 (users sharing same issue) | Closed, no fix |
| [#2672](https://github.com/microsoft/playwright-dotnet/issues/2672) | Memory leak with request/response events | -- | -- | "Re-file with reproduction" |
| [#2661](https://github.com/microsoft/playwright-dotnet/issues/2661) | GPU memory leak | -- | -- | "Recreate context after each test" |
| [#2629](https://github.com/microsoft/playwright-dotnet/issues/2629) | SetInputFilesAsync extreme memory allocation | -- | -- | "Closing, waiting for more reports" |
| [#2962](https://github.com/microsoft/playwright-dotnet/issues/2962) | Memory leak in .NET hosted services | 3 | 13 community comments | Zero maintainer response |

**Pattern:** Bug reports without a perfect minimal reproduction are closed. Even when multiple users confirm the same issue, it's dismissed.

---

## 6. Community Contributions Rejected

The project systematically rejects unsolicited community contributions:

| PR/Issue | Contribution | Outcome |
|----------|-------------|---------|
| #2595 | String constants extraction | Rejected |
| #2592 | Rewrite stream wrappers (found real bugs) | Rejected |
| #2557 | Update NuGet dependencies | Rejected — "We upgrade as we have to" |
| #2681 | Community member offered to build HTML reporting | Closed in 1 day |

Community PRs for features (like Windows ARM64 NuGet packages, #3288/#3289) face delays due to near-zero maintainer engagement.

---

## 7. Dismissed Feature Requests (Closed as NOT_PLANNED)

**60+ issues** were closed as `NOT_PLANNED`. Notable patterns:

### .NET-Idiomatic Requests Rejected
- **#2221** - `IDisposable` alongside `IAsyncDisposable` (5 reactions) — Microsoft changed their own docs instead of the code
- **#3151** - `Uri` overloads for URL members — Closed without action
- **#2669** - Avoid async delegates (VSTHRD101 conflicts) — "More of a linter problem"

### Architectural Requests Rejected
- **#3089** - Custom HttpClient integration — "Against what Playwright stands for"
- **#2686** - WebView2 support — "Out of scope"
- **#2733** - Stealth/community plugins — "Not in scope"

### Bug Reports That Took 1-2 Years to Fix

| Issue | Bug | Days Open | Reactions |
|-------|-----|-----------|-----------|
| [#2255](https://github.com/microsoft/playwright-dotnet/issues/2255) | Single-file publish broken | **652 days** | 11 |
| [#2739](https://github.com/microsoft/playwright-dotnet/issues/2739) | Docker image without SDK | **644 days** | 4 |
| [#2122](https://github.com/microsoft/playwright-dotnet/issues/2122) | Native macOS ARM driver | **578 days** | 1 |
| [#2259](https://github.com/microsoft/playwright-dotnet/issues/2259) | Expose Expect timeout API | **533 days** | 9 |
| [#1652](https://github.com/microsoft/playwright-dotnet/issues/1652) | CancellationToken support | **~350 days** | 18 |

---

## 8. Maintenance Health Indicators

| Signal | Assessment |
|--------|-----------|
| Maintainer engagement on issues | **1 MEMBER response** in 6 months of new issues |
| Open bug response rate | **8%** (92% unanswered) |
| Backport cadence | Falling behind — 1.58/1.59 backports still open |
| .NET 10 readiness | Not addressed despite multiple reports |
| Community sentiment | Users asking "Is this maintained?" (no response) |
| Feature parity trend | **Widening gap** — AI Agents, visual testing, etc. |
| P3-collecting-feedback issues | 43 issues, 81% with zero maintainer comment |
| Releases | Versions track upstream Playwright but lag on .NET-specific issues |

### Maintainer Response Timeline
- **Late 2021 – Early 2022:** Active engagement from `pavelfeldman`, `timheuer`, `mxschmitt`
- **2022 – 2024:** Sporadic, declining responses
- **July 2025:** Brief triage pass by `Youssef1313` on 2 old issues
- **2025 – 2026:** Near-zero community engagement

---

## 9. Top 20 Pain Points Ranked by Community Demand

| Rank | Issue | Pain Point | Reactions | Age |
|------|-------|-----------|-----------|-----|
| 1 | #2286 | Installation is unintuitive | **70** | 3.5 yr |
| 2 | #1850 | Node.js dependency bloat | **53** | 4.3 yr |
| 3 | #1854 | No visual snapshot testing | **33** | 4.3 yr |
| 4 | #2214 | No auto screenshot/video on failure | **24** | 3.7 yr |
| 5 | #3255 | No .NET 10 Docker images | **24** | 5 mo |
| 6 | #2316 | Test retries broken/reverted | **23** | 3.5 yr |
| 7 | #1178 | No Android support | **20** | 5.1 yr |
| 8 | #2715 | No synchronous API | **19** | 2.5 yr |
| 9 | #2328 | No soft assertions | **18** | 3.5 yr |
| 10 | #2161 | No custom expect messages | **13** | 3.9 yr |
| 11 | #2239 | No auto-install browsers | **13** | 3.8 yr |
| 12 | #2314 | No Expect.Poll | **12** | 3.5 yr |
| 13 | #3263 | No Playwright Agents (AI) | **11** | 4 mo |
| 14 | #2317 | No custom driver path | **10** | 3.5 yr |
| 15 | #3081 | No programmatic settings | **10** | 1.3 yr |
| 16 | #2179 | No Electron support | **9** | 3.8 yr |
| 17 | #2714 | No AOT support | **8** | 2.5 yr |
| 18 | #2351 | No CSS/JS coverage API | **7** | 3.4 yr |
| 19 | #2444 | No default action timeout config | **7** | 3.2 yr |
| 20 | #3237 | No xUnit codegen | **6** | 7 mo |

---

## 10. Conclusions

### What .NET Developers Want (That They're Not Getting)

1. **A library that feels like a .NET library** — NuGet install that just works, no PowerShell scripts, no Node.js, supports AOT/trimming/single-file publish
2. **Feature parity with Node.js Playwright** — Visual testing, soft assertions, retries, on-failure artifacts, coverage API
3. **Active maintenance** — Timely responses to bugs, a visible roadmap, community PRs reviewed
4. **Modern .NET support** — .NET 10 compatibility, Docker images, AOT
5. **Test runner integration** — Programmatic config, better xUnit support, built-in retry logic

### The Fundamental Problem

The .NET port is treated as a **thin, auto-generated wrapper** around the Node.js Playwright core. Feature requests that would require .NET-specific engineering effort (removing Node.js, visual testing, sync API, test runner features) are systematically deferred or rejected. The project publishes version bumps that track upstream Playwright releases, but invests minimal effort in .NET-specific quality, features, or community engagement.

### The Opportunity for PlaywrightNative

The community's top demands align precisely with PlaywrightNative's architectural direction:

| Community Demand | PlaywrightNative Advantage |
|-----------------|--------------------------|
| Remove Node.js dependency | Direct driver communication — no Node.js |
| Simpler installation | Pure .NET NuGet package |
| AOT / trimming support | No Node.js = feasible |
| Custom driver paths | Controllable architecture |
| Active maintenance | Community-driven, responsive |
| .NET-idiomatic API | Designed for C# from the ground up |
