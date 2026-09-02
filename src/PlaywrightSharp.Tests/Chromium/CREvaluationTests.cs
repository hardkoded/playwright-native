/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.NUnit;

namespace PlaywrightSharp.Tests.Chromium
{
    /// <summary>
    /// Integration tests for JavaScript evaluation via the direct Chromium CDP layer.
    /// Tests expression evaluation, function evaluation with arguments,
    /// and evaluation across navigations.
    /// </summary>
    [TestFixture]
    public class CREvaluationTests : CRTestBase
    {
        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate expression")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateExpression()
        {
            int result = await Page.EvaluateAsync<int>("1 + 2").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(3));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate string expression")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateStringExpression()
        {
            string result = await Page.EvaluateAsync<string>("'hello' + ' world'").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("hello world"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate arrow function with arguments")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateArrowFunctionWithArguments()
        {
            int result = await Page.EvaluateFunctionAsync<int>("(a, b) => a + b", 3, 4).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(7));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate function with string argument")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithStringArgument()
        {
            string result = await Page.EvaluateFunctionAsync<string>("(s) => s.toUpperCase()", "hello").ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("HELLO"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate function with mixed arguments")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithMixedArguments()
        {
            string result = await Page.EvaluateFunctionAsync<string>(
                "(name, age) => `${name} is ${age}`", "Alice", 30).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo("Alice is 30"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate function with boolean argument")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithBooleanArgument()
        {
            bool result = await Page.EvaluateFunctionAsync<bool>("(v) => !v", true).ConfigureAwait(false);
            Assert.That(result, Is.False);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate function with no arguments")]
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

        [PlaywrightTest("page-evaluate.spec.ts", "should await promise from function")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldAwaitPromiseFromFunction()
        {
            int result = await Page.EvaluateFunctionAsync<int>(
                "async (x) => { return x * 2; }", 5).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(10));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should throw on evaluation error")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldThrowOnEvaluationError()
        {
            PlaywrightSharpException ex = Assert.ThrowsAsync<PlaywrightSharpException>(
                () => Page.EvaluateAsync<object>("throw new Error('test error')"));
            Assert.That(ex.Message, Does.Contain("test error"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate after navigation")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateAfterNavigation()
        {
            await Page.GoToAsync("data:text/html,<div>test</div>").ConfigureAwait(false);

            string text = await Page.EvaluateAsync<string>("document.querySelector('div').textContent").ConfigureAwait(false);
            Assert.That(text, Is.EqualTo("test"));
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate function with null argument")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithNullArgument()
        {
            bool result = await Page.EvaluateFunctionAsync<bool>("(v) => v === null", (object)null).ConfigureAwait(false);
            Assert.That(result, Is.True);
        }

        [PlaywrightTest("page-evaluate.spec.ts", "should evaluate function with double argument")]
        [Test, Timeout(TestConstants.DefaultTestTimeout)]
        public async Task ShouldEvaluateFunctionWithDoubleArgument()
        {
            double result = await Page.EvaluateFunctionAsync<double>("(a, b) => a + b", 1.5, 2.5).ConfigureAwait(false);
            Assert.That(result, Is.EqualTo(4.0));
        }
    }
}
