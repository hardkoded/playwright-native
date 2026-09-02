using System;
using System.IO;
using System.Threading.Tasks;
using PlaywrightSharp;

namespace ScreenshotsDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            BrowserFetcher fetcher = new BrowserFetcher();
            InstalledBrowser installed = await fetcher.DownloadAsync();

            await using var browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = installed.GetExecutablePath(),
                Headless = true,
            });

            Console.WriteLine("Navigating microsoft");
            var page = await browser.NewPageAsync();
            await page.GoToAsync("http://www.microsoft.com");

            Console.WriteLine("Taking Screenshot");
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "microsoft.png");
            await File.WriteAllBytesAsync(outputPath, await page.ScreenshotAsync());

            Console.WriteLine("Export completed");
        }
    }
}
