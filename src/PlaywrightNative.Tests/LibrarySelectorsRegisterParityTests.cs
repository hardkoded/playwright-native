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
using System.IO;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/selectors-register.spec.ts</c> parity. Do not edit
    /// leftover <c>SelectorsRegisterTests</c> or page
    /// <c>SelectorsRegisterParityTests</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibrarySelectorsRegisterParityTests : PageTestEx
    {
        private const string CreateTagSelector = @"() => ({
  query(root, selector) {
    return root.querySelector(selector);
  },
  queryAll(root, selector) {
    return Array.from(root.querySelectorAll(selector));
  }
})";

        [PlaywrightTest("selectors-register.spec.ts", "should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWork()
        {
            await Playwright.Selectors.RegisterAsync("tag", "(" + CreateTagSelector + ")()").ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            await Playwright.Selectors.RegisterAsync("tag2", "(" + CreateTagSelector + ")()").ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><span></span></div><div></div>").ConfigureAwait(false);

            Assert.That(await page.EvalOnSelectorAsync<string>("tag=DIV", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("DIV"));
            Assert.That(await page.EvalOnSelectorAsync<string>("tag=SPAN", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("tag=DIV", "es => es.length").ConfigureAwait(false), Is.EqualTo(2));

            Assert.That(await page.EvalOnSelectorAsync<string>("tag2=DIV", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("DIV"));
            Assert.That(await page.EvalOnSelectorAsync<string>("tag2=SPAN", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            Assert.That(await page.EvalOnSelectorAllAsync<int>("tag2=DIV", "es => es.length").ConfigureAwait(false), Is.EqualTo(2));

            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("tAG=DIV"));
            Assert.That(error.Message, Does.Contain("Unknown engine \"tAG\" while parsing selector tAG=DIV"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "should work when registered on global")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void ShouldWorkWhenRegisteredOnGlobal()
        {
            Assert.Ignore("Official it.skip(mode === 'driver'); Node require('@playwright/test').");
        }

        [PlaywrightTest("selectors-register.spec.ts", "should work with path")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkWithPath()
        {
            string path = Path.Combine(Path.GetTempPath(), "sectionselectorengine-" + Guid.NewGuid().ToString("N") + ".js");
            await File.WriteAllTextAsync(
                path,
                @"({
  create(root, target) {
  },
  query(root, selector) {
    return root.querySelector('section');
  },
  queryAll(root, selector) {
    return Array.from(root.querySelectorAll('section'));
  }
})").ConfigureAwait(false);
            try
            {
                await Playwright.Selectors.RegisterAsync("foo", new() { Path = path }).ConfigureAwait(false);
                await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
                IPage page = await browser.NewPageAsync().ConfigureAwait(false);
                await page.SetContentAsync("<section></section>").ConfigureAwait(false);
                Assert.That(await page.EvalOnSelectorAsync<string>("foo=whatever", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SECTION"));
                await page.CloseAsync().ConfigureAwait(false);
            }
            finally
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException)
                {
                }
            }
        }

        [PlaywrightTest("selectors-register.spec.ts", "should work in main and isolated world")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldWorkInMainAndIsolatedWorld()
        {
            const string createDummySelector = @"() => ({
    query(root, selector) {
      return window['__answer'];
    },
    queryAll(root, selector) {
      return window['__answer'] ? [window['__answer'], document.body, document.documentElement] : [];
    }
  })";
            await Playwright.Selectors.RegisterAsync("main", createDummySelector).ConfigureAwait(false);
            await Playwright.Selectors.RegisterAsync("isolated", createDummySelector, contentScript: true).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<div><span><section></section></span></div>").ConfigureAwait(false);
            await page.EvaluateAsync("() => window['__answer'] = document.querySelector('span')").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("main=ignored", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            Assert.That(await page.EvalOnSelectorAsync<string>("css=div >> main=ignored", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            Assert.That(await page.EvalOnSelectorAllAsync<bool>("main=ignored", "es => window['__answer'] !== undefined").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("main=ignored", "es => es.filter(e => e).length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.QuerySelectorAsync("isolated=ignored").ConfigureAwait(false), Is.Null);
            Assert.That(await page.QuerySelectorAsync("css=div >> isolated=ignored").ConfigureAwait(false), Is.Null);
            Assert.That(await page.EvalOnSelectorAllAsync<bool>("isolated=ignored", "es => window['__answer'] !== undefined").ConfigureAwait(false), Is.True);
            Assert.That(await page.EvalOnSelectorAllAsync<int>("isolated=ignored", "es => es.filter(e => e).length").ConfigureAwait(false), Is.EqualTo(3));
            Assert.That(await page.EvalOnSelectorAsync<string>("main=ignored >> isolated=ignored", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            Assert.That(await page.EvalOnSelectorAsync<string>("isolated=ignored >> main=ignored", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SPAN"));
            Assert.That(await page.EvalOnSelectorAsync<string>("main=ignored >> css=section", "e => e.nodeName").ConfigureAwait(false), Is.EqualTo("SECTION"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-register.spec.ts", "should handle errors")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldHandleErrors()
        {
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("neverregister=ignored"));
            Assert.That(error.Message, Does.Contain("Unknown engine \"neverregister\" while parsing selector neverregister=ignored"));

            const string createDummySelector = @"() => ({
    query(root, selector) {
      return root.querySelector('dummy');
    },
    queryAll(root, selector) {
      return Array.from(root.querySelectorAll('dummy'));
    }
  })";

            error = Assert.CatchAsync<PlaywrightNativeException>(() => Playwright.Selectors.RegisterAsync("$", createDummySelector));
            Assert.That(error.Message, Is.EqualTo("selectors.register: Selector engine name may only contain [a-zA-Z0-9_] characters"));

            await Playwright.Selectors.RegisterAsync("dummy", createDummySelector).ConfigureAwait(false);
            await Playwright.Selectors.RegisterAsync("duMMy", createDummySelector).ConfigureAwait(false);

            error = Assert.CatchAsync<PlaywrightNativeException>(() => Playwright.Selectors.RegisterAsync("dummy", createDummySelector));
            Assert.That(error.Message, Is.EqualTo("selectors.register: \"dummy\" selector engine has been already registered"));

            error = Assert.CatchAsync<PlaywrightNativeException>(() => Playwright.Selectors.RegisterAsync("css", createDummySelector));
            Assert.That(error.Message, Is.EqualTo("selectors.register: \"css\" is a predefined selector engine"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-register.spec.ts", "should throw \"already registered\" error when registering")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowAlreadyRegisteredErrorWhenRegistering()
        {
            await Playwright.Selectors.RegisterAsync("alreadyRegistered", CreateTagSelector).ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(
                () => Playwright.Selectors.RegisterAsync("alreadyRegistered", CreateTagSelector));
            Assert.That(error.Message, Is.EqualTo("selectors.register: \"alreadyRegistered\" selector engine has been already registered"));
        }

        [PlaywrightTest("selectors-register.spec.ts", "should not rely on engines working from the root")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldNotRelyOnEnginesWorkingFromTheRoot()
        {
            const string createValueEngine = @"() => ({
    query(root, selector) {
      return root && root.value.includes(selector) ? root : undefined;
    },
    queryAll(root, selector) {
      return root && root.value.includes(selector) ? [root] : [];
    },
  })";
            await Playwright.Selectors.RegisterAsync("__value", createValueEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            await page.SetContentAsync("<input id=input1 value=value1><input id=input2 value=value2>").ConfigureAwait(false);
            Assert.That(await page.EvalOnSelectorAsync<string>("input >> __value=value2", "e => e.id").ConfigureAwait(false), Is.EqualTo("input2"));
            await page.CloseAsync().ConfigureAwait(false);
        }

        [PlaywrightTest("selectors-register.spec.ts", "should throw a nice error if the selector returns a bad value")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowANiceErrorIfTheSelectorReturnsABadValue()
        {
            const string createFakeEngine = @"() => ({
    query(root, selector) {
      return [document.body];
    },
    queryAll(root, selector) {
      return [[document.body]];
    },
  })";
            await Playwright.Selectors.RegisterAsync("__fake", createFakeEngine).ConfigureAwait(false);
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IPage page = await browser.NewPageAsync().ConfigureAwait(false);
            PlaywrightNativeException error = Assert.CatchAsync<PlaywrightNativeException>(() => page.QuerySelectorAsync("__fake=value2"));
            Assert.That(error.Message, Does.Contain("Expected a Node but got [object Array]"));
            await page.CloseAsync().ConfigureAwait(false);
        }
    }
}
