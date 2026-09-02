/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Upstream <c>page-dialog.spec.ts</c> parity, including official <c>dialogclosed</c>.
    /// </summary>
    [TestFixture]
    public class PageDialogParityTests : PageTestEx
    {
        private static async Task PollAsync(Func<bool> condition)
        {
            DateTime deadline = DateTime.UtcNow.AddSeconds(5);
            while (!condition() && DateTime.UtcNow < deadline)
            {
                await Task.Delay(50).ConfigureAwait(false);
            }

            Assert.That(condition(), Is.True);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should fire")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFire()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) =>
            {
                Assert.That(dialog.Type, Is.EqualTo(DialogType.Alert));
                Assert.That(dialog.DefaultValue, Is.EqualTo(string.Empty));
                Assert.That(dialog.Message, Is.EqualTo("yo"));
                _ = dialog.AcceptAsync();
            };

            await page.EvaluateAsync("alert('yo')").ConfigureAwait(false);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should fire dialogclosed when dialog is accepted")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireDialogClosedWhenDialogIsAccepted()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            List<IDialog> closed = new List<IDialog>();
            _ = page.DialogClosed() + ((_, dialog) => { closed.Add(dialog); });
            IDialog opened = null;
            page.Dialog += (_, dialog) =>
            {
                opened = dialog;
                _ = dialog.AcceptAsync();
            };

            await page.EvaluateAsync("alert('yo')").ConfigureAwait(false);
            await PollAsync(() => closed.Count == 1).ConfigureAwait(false);
            Assert.That(closed, Has.Count.EqualTo(1));
            Assert.That(closed[0], Is.SameAs(opened));

            // Perform some roundtrips to ensure the event does not fire twice.
            await page.EvaluateAsync("1").ConfigureAwait(false);
            await page.EvaluateAsync("1").ConfigureAwait(false);
            Assert.That(closed, Has.Count.EqualTo(1));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should fire dialogclosed when dialog is dismissed")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireDialogClosedWhenDialogIsDismissed()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IDialog> closedPromise = page.WaitForEventAsync(PageEvent.DialogClosed);
            page.Dialog += (_, dialog) => _ = dialog.DismissAsync();
            await page.EvaluateAsync("confirm('boolean?')").ConfigureAwait(false);
            IDialog dialog = await closedPromise.ConfigureAwait(false);
            Assert.That(dialog.Type, Is.EqualTo(DialogType.Confirm));
            Assert.That(dialog.Message, Is.EqualTo("boolean?"));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should fire dialogclosed for auto-dismissed dialogs")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireDialogClosedForAutoDismissedDialogs()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IDialog> closedPromise = page.WaitForDialogClosedAsync();
            await page.EvaluateAsync("alert('yo')").ConfigureAwait(false);
            IDialog dialog = await closedPromise.ConfigureAwait(false);
            Assert.That(dialog.Message, Is.EqualTo("yo"));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should allow accepting prompts")]
        [PlaywrightTest("page-dialog.spec.ts", "should allow accepting prompts @smoke")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAllowAcceptingPrompts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) =>
            {
                Assert.That(dialog.Type, Is.EqualTo(DialogType.Prompt));
                Assert.That(dialog.DefaultValue, Is.EqualTo("yes."));
                Assert.That(dialog.Message, Is.EqualTo("question?"));
                _ = dialog.AcceptAsync("answer!");
            };

            string result = await page.EvaluateAsync<string>("prompt('question?', 'yes.')").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("answer!"));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should dismiss the prompt")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDismissThePrompt()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) => _ = dialog.DismissAsync();
            string result = await page.EvaluateAsync<string>("prompt('question?')").ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should accept the confirm prompt")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAcceptTheConfirmPrompt()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) => _ = dialog.AcceptAsync();
            bool result = await page.EvaluateAsync<bool>("confirm('boolean?')").ConfigureAwait(false);
            Assert.That(result, Is.True);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should dismiss the confirm prompt")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldDismissTheConfirmPrompt()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) => _ = dialog.DismissAsync();
            bool result = await page.EvaluateAsync<bool>("confirm('boolean?')").ConfigureAwait(false);
            Assert.That(result, Is.False);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should be able to close context with open alert")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldBeAbleToCloseContextWithOpenAlert()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            Task<IDialog> alertPromise = page.WaitForDialogAsync();
            await page.EvaluateAsync("setTimeout(() => alert('hello'), 0)").ConfigureAwait(false);
            await alertPromise.ConfigureAwait(false);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should handle multiple alerts")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleMultipleAlerts()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) =>
            {
                _ = dialog.AcceptAsync().ContinueWith(_ => { }, TaskScheduler.Default);
            };

            await page.SetContentAsync(@"
    <p>Hello World</p>
    <script>
      alert('Please dismiss this dialog');
      alert('Please dismiss this dialog');
      alert('Please dismiss this dialog');
    </script>
  ").ConfigureAwait(false);
            string text = await page.TextContentAsync("p").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("Hello World"));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should handle multiple confirms")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldHandleMultipleConfirms()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            page.Dialog += (_, dialog) =>
            {
                _ = dialog.AcceptAsync().ContinueWith(_ => { }, TaskScheduler.Default);
            };

            await page.SetContentAsync(@"
    <p>Hello World</p>
    <script>
      confirm('Please confirm me?');
      confirm('Please confirm me?');
      confirm('Please confirm me?');
    </script>
  ").ConfigureAwait(false);
            string text = await page.TextContentAsync("p").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("Hello World"));
        }

        [PlaywrightTest("page-dialog.spec.ts", "should auto-dismiss the prompt without listeners")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDismissThePromptWithoutListeners()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            string result = await page.EvaluateAsync<string>("prompt('question?')").ConfigureAwait(false);
            Assert.That(result, Is.Null);
        }

        [PlaywrightTest("page-dialog.spec.ts", "should auto-dismiss the alert without listeners")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldAutoDismissTheAlertWithoutListeners()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await page.SetContentAsync("<div onclick=\"window.alert(123); window._clicked=true\">Click me</div>").ConfigureAwait(false);
            await page.ClickAsync("div").ConfigureAwait(false);
            bool clicked = await page.EvaluateAsync<bool>("window._clicked").ConfigureAwait(false);
            Assert.That(clicked, Is.True);
        }
    }
}
