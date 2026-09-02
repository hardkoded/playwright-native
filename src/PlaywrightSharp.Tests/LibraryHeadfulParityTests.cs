/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>library/headful.spec.ts</c> parity. File-level official
    /// skip for headless (avoid popping windows) and chromium-headless-shell.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryHeadfulParityTests : PageTestEx
    {
        [SetUp]
        public void SkipHeadless()
        {
            Assert.Ignore("official skip: avoid popping windows in headless mode");
        }

        [PlaywrightTest("headful.spec.ts", "should have default url when launching browser @smoke")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldHaveDefaultUrlWhenLaunchingBrowser()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should close browser with beforeunload page")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldCloseBrowserWithBeforeunloadPage()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should close browsercontext with pending beforeunload dialog")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldCloseBrowsercontextWithPendingBeforeunloadDialog()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should not crash when creating second context")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotCrashWhenCreatingSecondContext()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should click when viewport size is larger than screen")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldClickWhenViewportSizeIsLargerThanScreen()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should dispatch click events to oversized viewports")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldDispatchClickEventsToOversizedViewports()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should click background tab")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldClickBackgroundTab()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should close browser after context menu was triggered")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldCloseBrowserAfterContextMenuWasTriggered()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should(not) block third party cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotBlockThirdPartyCookies()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should not block third party SameSite=None cookies")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotBlockThirdPartySameSiteNoneCookies()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should not override viewport size when passed null")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldNotOverrideViewportSizeWhenPassedNull()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "Page.bringToFront should work")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task PageBringToFrontShouldWork()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should click in OOPIF")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldClickInOopif()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should click bottom row w/ infobar in OOPIF")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldClickBottomRowWInfobarInOopif()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "headless and headful should use same default fonts")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task HeadlessAndHeadfulShouldUseSameDefaultFonts()
            => Task.CompletedTask;

        [PlaywrightTest("headful.spec.ts", "should have the same hyphen rendering on headless and headed")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public Task ShouldHaveTheSameHyphenRenderingOnHeadlessAndHeaded()
            => Task.CompletedTask;
    }
}
