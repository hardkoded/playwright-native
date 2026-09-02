# How to take screenshots
_Contributors: [Meir Blachman](https://www.github.com/meir017)_

## Problem

You need to take a screenshot of a page.

## Solution

Use `Page.ScreenshotAsync`, passing a file path when you want the image written to disk.

```cs
using PlaywrightNative;

string url = "https://www.somepage.com";
string file = "somepage.png";

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
});
var page = await browser.NewPageAsync();

await page.GoToAsync(url);
await page.ScreenshotAsync(path: file);
```

`ScreenshotAsync` also returns the image bytes when you omit `path`.
