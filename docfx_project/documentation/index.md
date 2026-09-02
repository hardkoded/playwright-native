# PlaywrightSharp

PlaywrightSharp is a .NET library to automate Chromium, Firefox, and WebKit. It talks to the browser directly — there is no Node.js driver process to copy or configure.

## Installation

```
dotnet add package PlaywrightSharp
```

Browsers download automatically the first time you launch if they are not already cached.

## Quick start

```cs
using PlaywrightSharp;

await using var browser = await Playwright.LaunchChromiumAsync();
var page = await browser.NewPageAsync();
await page.GoToAsync("https://www.bing.com");
await page.ScreenshotAsync(path: "bing.png");
```

Launch Firefox or WebKit the same way:

```cs
await using var firefox = await Playwright.LaunchFirefoxAsync();
await using var webkit = await Playwright.LaunchWebkitAsync();
```

Pass `BrowserTypeLaunchOptions` when you need a headed window, a specific binary, or logging:

```cs
await using var browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
    ExecutablePath = "/path/to/chromium",
});
```

## Browsers

`LaunchChromiumAsync`, `LaunchFirefoxAsync`, and `LaunchWebkitAsync` download the matching browser through `BrowserFetcher` when `ExecutablePath` is omitted. You can also download ahead of time:

```cs
var fetcher = new BrowserFetcher();
InstalledBrowser installed = await fetcher.DownloadAsync();

await using var browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
{
    ExecutablePath = installed.GetExecutablePath(),
});
```

Override the cache directory with `BrowserFetcherOptions.Path` or the `PLAYWRIGHT_BROWSERS_PATH` environment variable. See [custom browser locations](Playwright.UseCustomLocations.md).

Older samples launched a Node.js driver and copied platform-specific binaries into `bin`. That path is gone.

## Examples

### Evaluate in the page

```cs
using PlaywrightSharp;

await using var browser = await Playwright.LaunchFirefoxAsync();
var page = await browser.NewPageAsync();
await page.GoToAsync("https://www.example.com/");
var dimensions = await page.EvaluateAsync<Dictionary<string, int>>(@"() => ({
    width: document.documentElement.clientWidth,
    height: document.documentElement.clientHeight,
})");
Console.WriteLine($"{dimensions["width"]}x{dimensions["height"]}");
```

### Intercept network requests

```cs
using PlaywrightSharp;

await using var browser = await Playwright.LaunchChromiumAsync();
var page = await browser.NewPageAsync();
await page.RouteAsync("**/*", route =>
{
    Console.WriteLine(route.Request.Url);
    route.ContinueAsync();
});
await page.GoToAsync("https://todomvc.com");
```

### Mobile and geolocation

```cs
using PlaywrightSharp;

await using var browser = await Playwright.LaunchWebkitAsync();

var contextOptions = new BrowserContextOptions(Playwright.Devices["iPhone 11 Pro"])
{
    Locale = "en-US",
    Geolocation = new Geolocation { Longitude = 12.492507f, Latitude = 41.889938f },
    Permissions = new[] { "geolocation" },
};

var context = await browser.NewContextAsync(contextOptions);
var page = await context.NewPageAsync();
await page.GoToAsync("https://maps.google.com");
await page.ClickAsync("text='Your location'");
await page.ScreenshotAsync(path: "colosseum-iphone.png");
```

## Useful links

* [How to take screenshots](Page.ScreenshotAsync.md)
* [How to download a file](Page.Download.md)
* [How to get internal logs](Playwright.Logger.md)
* [Issues](https://github.com/hardkoded/playwright-sharp/issues)
