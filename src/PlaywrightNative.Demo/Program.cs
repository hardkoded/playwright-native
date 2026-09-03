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
            using var playwright = await Playwright.CreateAsync();
            var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { ExecutablePath = chromiumPath, Headless = false });
            var context = await browser.NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GoToAsync("https://example.com");
            Console.ReadLine();
        }
    }
}
