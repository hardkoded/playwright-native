# How to get internal logs
_Contributors: [Dario Kondratiuk](https://www.hardkoded.com/)_

## Problem

You need library logs to debug a launch or protocol problem.

## Solution

Pass an `ILoggerFactory` on `BrowserTypeLaunchOptions`. PlaywrightNative uses it while launching and talking to the browser.

```cs
using Microsoft.Extensions.Logging;
using PlaywrightNative;

ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
{
    builder.SetMinimumLevel(LogLevel.Debug);
    builder.AddDebug();
});

await using var browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
{
    LoggerFactory = loggerFactory,
});
```

You can also implement `IPlaywrightLogger` and set `BrowserTypeLaunchOptions.Logger` for official API-call start/success lines (for example `browser.newContext started`).

Older samples passed a logger into a driver-process factory. Use launch options instead.
