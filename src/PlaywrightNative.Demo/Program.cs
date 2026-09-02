using System;
using System.Threading.Tasks;
using PlaywrightNative;

namespace PlaywrightNative.Demo
{
    class Program
    {
        static async Task Main()
        {
            string chromiumPath = Environment.GetEnvironmentVariable("CHROMIUM_PATH")
                ?? throw new InvalidOperationException("Set CHROMIUM_PATH to the Chromium executable.");
            var browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions { ExecutablePath = chromiumPath, Headless = false });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GoToAsync("https://example.com");
            Console.ReadLine();
        }
    }
}
