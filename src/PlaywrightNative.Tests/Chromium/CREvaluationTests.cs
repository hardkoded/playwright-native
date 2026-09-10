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

namespace PlaywrightNative.Tests.Chromium
{
    /// <summary>
    /// Integration tests for JavaScript evaluation via the direct Chromium CDP layer.
    /// Tests expression evaluation, function evaluation with arguments,
    /// and evaluation across navigations.
    /// </summary>
    [TestFixture]
    public class CREvaluationTests : CRTestBase
    {
        [PlaywrightTest("page-evaluate.spec.ts", "should accept a string")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateExpression()
        {
            int result = await Page.EvaluateAsync<int>("1 + 2").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(3));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateStringExpression()
        {
            string result = await Page.EvaluateAsync<string>("'hello' + ' world'").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("hello world"));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateArrowFunctionWithArguments()
        {
            int result = await Page.EvaluateFunctionAsync<int>("(a, b) => a + b", 3, 4).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(7));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithStringArgument()
        {
            string result = await Page.EvaluateFunctionAsync<string>("(s) => s.toUpperCase()", "hello").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("HELLO"));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithMixedArguments()
        {
            string result = await Page.EvaluateFunctionAsync<string>(
                "(name, age) => `${name} is ${age}`", "Alice", 30).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Alice is 30"));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithBooleanArgument()
        {
            bool result = await Page.EvaluateFunctionAsync<bool>("(v) => !v", true).ConfigureAwait(false);
            Assert.That(result, Is.False);
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithNoArguments()
        {
            int result = await Page.EvaluateFunctionAsync<int>("() => 42").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(42));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should await promise")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitPromise()
        {
            int result = await Page.EvaluateAsync<int>("Promise.resolve(8)").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(8));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitPromiseFromFunction()
        {
            int result = await Page.EvaluateFunctionAsync<int>(
                "async (x) => { return x * 2; }", 5).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(10));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnEvaluationError()
        {
            PlaywrightNativeException ex = Assert.ThrowsAsync<PlaywrightNativeException>(
                () => Page.EvaluateAsync<object>("throw new Error('test error')"));
            Assert.That(ex.Message, Does.Contain("test error"));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateAfterNavigation()
        {
            await Page.GoToAsync("data:text/html,<div>test</div>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("test"));
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithNullArgument()
        {
            bool result = await Page.EvaluateFunctionAsync<bool>("(v) => v === null", (object)null).ConfigureAwait(false);
            Assert.That(result, Is.True);
        }

        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithDoubleArgument()
        {
            double result = await Page.EvaluateFunctionAsync<double>("(a, b) => a + b", 1.5, 2.5).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(4.0));
        }
    }
}
