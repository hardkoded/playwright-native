using System;
using System.IO;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using PlaywrightNative;

namespace PdfDemo
{
    class Program
    {
        static async Task Main(string[] args)
        {
            ILoggerFactory loggerFactory = LoggerFactory.Create(builder =>
            {
                builder.SetMinimumLevel(LogLevel.Debug);
                builder.AddDebug();
            });

            BrowserFetcher fetcher = new BrowserFetcher();
            InstalledBrowser installed = await fetcher.DownloadAsync();

            using var playwright = await Playwright.CreateAsync();
            await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = installed.GetExecutablePath(),
                Headless = true,
                LoggerFactory = loggerFactory,
            });

            var page = await browser.NewPageAsync();
            Console.WriteLine("Navigating google");
            await page.GoToAsync("http://www.google.com");

            Console.WriteLine("Generating PDF");
            string outputPath = Path.Combine(Directory.GetCurrentDirectory(), "google.pdf");
            byte[] pdf = await page.PdfAsync();
            await File.WriteAllBytesAsync(outputPath, pdf);

            Console.WriteLine("Export completed");
        }
    }
}
