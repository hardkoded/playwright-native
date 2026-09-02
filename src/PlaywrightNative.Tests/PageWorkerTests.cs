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
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Direct-connection tests for <see cref="IPage.Worker"/> and <see cref="IPage.Workers"/>.
    /// </summary>
    [TestFixture]
    public class PageWorkerTests : PageTestEx
    {
        [PlaywrightTest("workers.spec.ts", "Worker event and evaluate")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportWorkerAndEvaluate()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> waitTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(@"
                window.__w = new Worker(URL.createObjectURL(new Blob(['// wave78'], { type: 'application/javascript' })));
            ").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            Assert.That(worker, Is.Not.Null);
            Assert.That(page.Workers, Has.Exactly(1).Items);
            Assert.That(page.Workers, Does.Contain(worker));

            int sum = await worker.EvaluateAsync<int>("1 + 1").ConfigureAwait(false);
            Assert.That(sum, Is.EqualTo(2));

            IJSHandle handle = await worker.EvaluateHandleAsync("({ a: 7 })").ConfigureAwait(false);
            Assert.That(handle, Is.Not.Null);
            int value = await handle.EvaluateAsync<int>("obj => obj.a").ConfigureAwait(false);
            Assert.That(value, Is.EqualTo(7));
        }

        [PlaywrightTest("workers.spec.ts", "Workers is empty before a worker is created")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldStartWithNoWorkers()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Assert.That(page.Workers, Is.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "Close fires when the worker terminates")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldFireCloseWhenWorkerTerminates()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> waitTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(@"
                window.__w = new Worker(URL.createObjectURL(new Blob(['// wave78-close'], { type: 'application/javascript' })));
            ").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            Task<IWorker> closeTask = worker.WaitForCloseAsync();
            await page.EvaluateAsync<object>("window.__w.terminate()").ConfigureAwait(false);
            IWorker closed = await closeTask.ConfigureAwait(false);

            Assert.That(closed, Is.SameAs(worker));
            Assert.That(page.Workers, Is.Empty);
        }

        [PlaywrightTest("workers.spec.ts", "Worker Console reports console.log")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldReportWorkerConsoleLog()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> waitTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(@"
                window.__w = new Worker(URL.createObjectURL(new Blob(['// wave338'], { type: 'application/javascript' })));
            ").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            TaskCompletionSource<IConsoleMessage> consoleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            worker.Console += (_, message) => consoleTcs.TrySetResult(message);

            await worker.EvaluateAsync<object>("console.log('hello-worker')").ConfigureAwait(false);
            IConsoleMessage message = await consoleTcs.Task.ConfigureAwait(false);

            Assert.That(message, Is.Not.Null);
            Assert.That(message.Text, Does.Contain("hello-worker"));
            Assert.That(message.Type, Is.EqualTo("log"));
        }

        [PlaywrightTest("workers.spec.ts", "Console message exposes Worker")]
        [Test]
        [Timeout(30_000)]
        public async Task ShouldExposeWorkerOnConsoleMessage()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync("about:blank").ConfigureAwait(false);

            Task<IWorker> waitTask = page.WaitForEventAsync(PageEvent.Worker);
            await page.EvaluateAsync<object>(@"
                window.__w = new Worker(URL.createObjectURL(new Blob(['// wave339'], { type: 'application/javascript' })));
            ").ConfigureAwait(false);
            IWorker worker = await waitTask.ConfigureAwait(false);

            TaskCompletionSource<IConsoleMessage> consoleTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            worker.Console += (_, message) => consoleTcs.TrySetResult(message);

            await worker.EvaluateAsync<object>("console.log('worker-owner')").ConfigureAwait(false);
            IConsoleMessage message = await consoleTcs.Task.ConfigureAwait(false);

            Assert.That(message, Is.Not.Null);
            Assert.That(message.Worker, Is.SameAs(worker));
        }
    }
}
