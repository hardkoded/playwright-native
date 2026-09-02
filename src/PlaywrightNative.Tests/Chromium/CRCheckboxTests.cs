/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.Chromium;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for <see cref="CRElementHandle.IsCheckedAsync"/>,
    /// <c>CheckAsync</c>, and <c>UncheckAsync</c>.
    /// </summary>
    [TestFixture]
    public class CRCheckboxTests : CRTestBase
    {
        [PlaywrightTest("page-check.spec.ts", "Is checked should return false for unchecked box")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IsCheckedShouldReturnFalseForUncheckedBox()
        {
            await Page.GoToAsync("data:text/html,<input id='c' type='checkbox'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#c").ConfigureAwait(false);
            bool isChecked = await handle.IsCheckedAsync().ConfigureAwait(false);

            Assert.That(isChecked, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "Is checked should return true for checked box")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IsCheckedShouldReturnTrueForCheckedBox()
        {
            await Page.GoToAsync("data:text/html,<input id='c' type='checkbox' checked>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#c").ConfigureAwait(false);
            bool isChecked = await handle.IsCheckedAsync().ConfigureAwait(false);

            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "Check should check an unchecked box")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckShouldCheckAnUncheckedBox()
        {
            await Page.GoToAsync("data:text/html,<input id='c' type='checkbox' style='position:absolute;left:20px;top:20px'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await handle.CheckAsync().ConfigureAwait(false);

            bool isChecked = await Page.EvaluateAsync<bool>("document.querySelector('#c').checked").ConfigureAwait(false);
            Assert.That(isChecked, Is.True);
        }

        [PlaywrightTest("page-check.spec.ts", "Check should be no op for already checked box")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task CheckShouldBeNoOpForAlreadyCheckedBox()
        {
            await Page.GoToAsync(@"data:text/html,<input id='c' type='checkbox' checked style='position:absolute;left:20px;top:20px'>
                <script>
                window.clicks = 0;
                document.getElementById('c').addEventListener('click', () => window.clicks++);
                </script>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await handle.CheckAsync().ConfigureAwait(false);

            int clicks = await Page.EvaluateAsync<int>("window.clicks").ConfigureAwait(false);
            Assert.That(clicks, Is.EqualTo(0));
        }

        [PlaywrightTest("page-check.spec.ts", "Uncheck should uncheck a checked box")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task UncheckShouldUncheckACheckedBox()
        {
            await Page.GoToAsync("data:text/html,<input id='c' type='checkbox' checked style='position:absolute;left:20px;top:20px'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#c").ConfigureAwait(false);
            await handle.UncheckAsync().ConfigureAwait(false);

            bool isChecked = await Page.EvaluateAsync<bool>("document.querySelector('#c').checked").ConfigureAwait(false);
            Assert.That(isChecked, Is.False);
        }

        [PlaywrightTest("page-check.spec.ts", "Uncheck should throw for radio button")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task UncheckShouldThrowForRadioButton()
        {
            await Page.GoToAsync("data:text/html,<input id='r' type='radio' checked>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#r").ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.UncheckAsync());
            Assert.That(ex.Message, Does.Contain("radio").IgnoreCase);
        }

        [PlaywrightTest("page-check.spec.ts", "Is checked should throw for non checkbox")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task IsCheckedShouldThrowForNonCheckbox()
        {
            await Page.GoToAsync("data:text/html,<input id='t' type='text'>").ConfigureAwait(false);

            await using CRElementHandle handle = await Page.QuerySelectorAsync("#t").ConfigureAwait(false);
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => handle.IsCheckedAsync());
            Assert.That(ex.Message, Does.Contain("checkbox").Or.Contain("radio"));
        }
    }
}
