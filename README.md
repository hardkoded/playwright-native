# PlaywrightSharp

[![NuGet version](https://buildstats.info/nuget/PlaywrightSharp)](https://www.nuget.org/packages/PlaywrightSharp)
[![Join Slack](https://img.shields.io/badge/join-slack-infomational)](https://join.slack.com/t/playwright/shared_invite/enQtOTEyMTUxMzgxMjIwLThjMDUxZmIyNTRiMTJjNjIyMzdmZDA3MTQxZWUwZTFjZjQwNGYxZGM5MzRmNzZlMWI5ZWUyOTkzMjE5Njg1NDg)
[![Backers](https://opencollective.com/hardkoded-projects/backers/badge.svg)](https://opencollective.com/hardkoded-projects)

PlaywrightSharp is a .NET library to automate [Chromium](https://www.chromium.org/Home), Firefox, and WebKit with a **pure .NET implementation** — no Node.js, no driver process, no PowerShell scripts.

Chromium is the primary, fully supported browser. Firefox and WebKit launch today through the same API; Firefox is not in CI yet.

## Why PlaywrightSharp instead of playwright-dotnet?

**PlaywrightSharp is a community-first project, created by the community, for the community.**

[Microsoft's playwright-dotnet](https://github.com/microsoft/playwright-dotnet) is a thin auto-generated wrapper around a bundled Node.js process. After years of accumulated community frustration — [89% of open issues unanswered](tasks/playwright-dotnet-pain-points-report.md), top feature requests open for 4+ years, and a widening gap with the Node.js version — we decided to build something better.

### No Node.js. Pure .NET.

The playwright-dotnet library ships an entire **Node.js runtime** inside your NuGet package. This means bloated CI/CD pipelines (teams have reported [300 GB/month in build artifacts](https://github.com/microsoft/playwright-dotnet/issues/1850) just from Node.js binaries), incompatibility with AOT compilation and trimmed publish, and a multi-step installation ritual involving PowerShell scripts buried inside `bin/` folders.

PlaywrightSharp talks to the browser directly (CDP for Chromium, Juggler for Firefox, WebKit Inspection Protocol for WebKit). No Node.js. No driver process. No PowerShell scripts. Just `dotnet add package` and you're ready to go.

### All the good parts of Playwright

Our goal is full feature parity with Playwright — including the features the .NET community has been asking for and never got:

- Visual snapshot testing
- Built-in reporting
- Agentic tools and AI integration
- Soft assertions, Expect.Poll, custom expect messages
- Screenshot, video, and tracing on test failure
- Test retries that actually work
- Synchronous API option
- Programmatic configuration (not just `.runsettings` XML)

### Simple installation

```
dotnet add package PlaywrightSharp
```

That's it. No PowerShell scripts. No `playwright.ps1` hidden in your build output. No `dotnet build` before you can install browsers. `BrowserFetcher` downloads browsers automatically when you launch.

### Open to the community

PlaywrightSharp welcomes contributions. We review PRs, we respond to issues, and we build what the community needs. This isn't a publish-only project with a locked-down contribution model — it's a project that grows with its users.

## Installation

```
dotnet add package PlaywrightSharp
```

## Quick Start

```cs
await using IBrowser browser = await Playwright.LaunchChromiumAsync();
await using IBrowserContext context = await browser.NewContextAsync();
IPage page = await context.NewPageAsync();
await page.GotoAsync("https://www.bing.com");
byte[] png = await page.ScreenshotAsync();
```

Firefox and WebKit use the same pattern:

```cs
await using IBrowser firefox = await Playwright.LaunchFirefoxAsync();
await using IBrowser webkit = await Playwright.LaunchWebkitAsync();
```

Pass a `BrowserTypeLaunchOptions` bag to set `ExecutablePath`, `Headless`, and other launch options. When `ExecutablePath` is omitted, `BrowserFetcher` downloads and caches the pinned browser build.

## Examples

### Evaluate in browser context

```cs
await using IBrowser browser = await Playwright.LaunchChromiumAsync();
await using IBrowserContext context = await browser.NewContextAsync();
IPage page = await context.NewPageAsync();
await page.GotoAsync("https://www.example.com/");
Dictionary<string, int> dimensions = await page.EvaluateAsync<Dictionary<string, int>>(@"() => ({
    width: document.documentElement.clientWidth,
    height: document.documentElement.clientHeight,
})");
Console.WriteLine($"{dimensions["width"]}x{dimensions["height"]}");
```

### Intercept network requests

```cs
await using IBrowser browser = await Playwright.LaunchChromiumAsync();
await using IBrowserContext context = await browser.NewContextAsync();
IPage page = await context.NewPageAsync();
await page.RouteAsync("**/*", route =>
{
    Console.WriteLine(route.Request.Url);
    route.ContinueAsync();
});
await page.GotoAsync("https://todomvc.com");
```

## Useful Links

* [Documentation](https://hardkoded.github.io/playwright-sharp)
* [StackOverflow](https://stackoverflow.com/search?q=playwright-sharp)
* [Issues](https://github.com/hardkoded/playwright-sharp/issues?utf8=%E2%9C%93&q=is%3Aissue)

## Contributing

We welcome contributions! Check out the [open issues](https://github.com/hardkoded/playwright-sharp/issues) to get started, or open a new one if you have an idea. PRs are reviewed and merged — not ignored.
