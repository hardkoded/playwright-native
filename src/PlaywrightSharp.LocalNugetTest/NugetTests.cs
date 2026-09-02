using System;
using System.Threading.Tasks;
using Xunit;

namespace PlaywrightSharp.LocalNugetTest
{
    public class NugetTests
    {
        [Fact]
        public async Task ShouldWork()
        {
            BrowserFetcher fetcher = new BrowserFetcher();
            InstalledBrowser installed = await fetcher.DownloadAsync();

            await using var browser = await Playwright.LaunchChromiumAsync(new BrowserTypeLaunchOptions
            {
                ExecutablePath = installed.GetExecutablePath(),
            });

            var page = await browser.NewPageAsync();
            Console.WriteLine("Navigating google");
            await page.GoToAsync("http://www.google.com");

            Assert.Contains("Google", await page.TitleAsync());
        }
    }
}
