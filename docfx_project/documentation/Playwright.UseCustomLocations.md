# How to use a custom browser cache
_Contributors: [Scott Huang](https://github.com/ScottHuangZL), [Dario Kondratiuk](https://www.hardkoded.com/)_

## Problem

You want browsers downloaded to a specific directory instead of the default Playwright cache.

## Solution

Pass a cache path to `BrowserFetcher`, then launch with that executable:

```cs
using PlaywrightNative;

var fetcher = new BrowserFetcher(new BrowserFetcherOptions
{
    Browser = SupportedBrowser.Chromium,
    Path = "customBrowserPath",
});
InstalledBrowser installed = await fetcher.DownloadAsync();

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    ExecutablePath = installed.GetExecutablePath(),
});
```

You can also set the `PLAYWRIGHT_BROWSERS_PATH` environment variable so every default `BrowserFetcher` (including the one used by `playwright.Chromium.LaunchAsync` when `ExecutablePath` is omitted) writes to that directory.

To use a browser you already installed, skip the fetcher and set `ExecutablePath` on `BrowserTypeLaunchOptions`.

Older samples also accepted a driver executable path. PlaywrightNative no longer ships or copies a Node.js driver.
