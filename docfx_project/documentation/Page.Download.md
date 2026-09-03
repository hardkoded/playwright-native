# How to get download files
_Contributors: [Dario Kondratiuk](https://www.hardkoded.com/)_

## Problem

You want to download and process a file.

## Solution

Enable downloads on the context (or page), then wait for `IDownload`.

```cs
var context = await browser.NewContextAsync(new BrowserContextOptions { AcceptDownloads = true });
```

Or:

```cs
var page = await browser.NewPageAsync(acceptDownloads: true);
```

Once downloads are accepted, handle the `Download` event:

```cs
page.Download += (_, download) => Console.WriteLine(download.SuggestedFilename);
```

You can also wait for the next download:

```cs
var downloadTask = page.WaitForDownloadAsync();
```

`IDownload` lets you read the file, delete it, or save it somewhere else.

```cs
using PlaywrightNative;

using var playwright = await Playwright.CreateAsync();
await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
{
    Headless = false,
});
var page = await browser.NewPageAsync(acceptDownloads: true);
await page.GoToAsync("https://github.com/hardkoded/playwright-native/releases");

var downloadTask = page.WaitForDownloadAsync();

await page.ClickAsync("text=Source Code");

IDownload download = await downloadTask;
string filePath = await download.PathAsync();
Console.WriteLine($"Original path: {filePath}");

await download.SaveAsAsync("version.zip");
Console.WriteLine($"New file exists: {new FileInfo("version.zip").Exists}");
```
