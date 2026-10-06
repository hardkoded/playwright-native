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
using System.Threading.Tasks;
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Official <c>frame.content()</c> serialization and the navigation-race error
    /// from <c>packages/playwright-core/src/server/frames.ts</c>.
    /// </summary>
    internal static class PageContent
    {
        /// <summary>
        /// JavaScript that returns doctype plus <c>document.documentElement.outerHTML</c>,
        /// matching official <c>page.content()</c>.
        /// </summary>
        internal const string EvaluateExpression =
            @"(() => {
                let retVal = '';
                if (document.doctype) {
                    retVal = new XMLSerializer().serializeToString(document.doctype);
                }
                if (document.documentElement) {
                    retVal += document.documentElement.outerHTML;
                }
                return retVal;
            })()";

        /// <summary>
        /// JavaScript for official <c>page.content({ includeShadow: true })</c>
        /// (<c>injectedScript.documentContent</c>): open shadow roots are serialized as
        /// declarative shadow DOM via <c>getHTML({ shadowRoots })</c>.
        /// </summary>
        internal const string EvaluateWithShadowExpression =
            @"(() => {
                let content = '';
                if (document.doctype) {
                    content = new XMLSerializer().serializeToString(document.doctype);
                }
                const root = document.documentElement;
                if (!root) {
                    return content;
                }
                const shadowRoots = [];
                const collectShadowRoots = node => {
                    for (const element of node.querySelectorAll('*')) {
                        if (element.shadowRoot) {
                            shadowRoots.push(element.shadowRoot);
                            collectShadowRoots(element.shadowRoot);
                        }
                    }
                };
                collectShadowRoots(document);
                // getHTML() serializes children only, wrap them with the root element tags.
                const emptyRoot = root.cloneNode(false).outerHTML;
                const endTagIndex = emptyRoot.lastIndexOf('</');
                return content + emptyRoot.slice(0, endTagIndex) + root.getHTML({ shadowRoots }) + emptyRoot.slice(endTagIndex);
            })()";

        /// <summary>
        /// Official message when <c>content()</c> is evaluated while the document is
        /// being replaced by a navigation.
        /// </summary>
        internal const string NavigationError =
            "Unable to retrieve content because the page is navigating and changing the content.";

        /// <summary>
        /// Picks the content expression for <paramref name="includeShadow"/>.
        /// </summary>
        /// <param name="includeShadow">Whether to serialize open shadow roots.</param>
        /// <returns>The JavaScript expression.</returns>
        internal static string Expression(bool includeShadow)
            => includeShadow ? EvaluateWithShadowExpression : EvaluateExpression;

        /// <summary>
        /// Runs <paramref name="evaluateAsync"/> and rewrites retriable evaluation
        /// failures (destroyed context, mid-navigation) to
        /// <see cref="NavigationError"/>.
        /// </summary>
        /// <param name="evaluateAsync">The browser evaluate that returns HTML.</param>
        /// <returns>The serialized document HTML.</returns>
        internal static async Task<string> ReadAsync(Func<Task<string>> evaluateAsync)
        {
            if (evaluateAsync == null)
            {
                throw new ArgumentNullException(nameof(evaluateAsync));
            }

            try
            {
                return await evaluateAsync().ConfigureAwait(false);
            }
            catch (TimeoutException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new PlaywrightException(NavigationError, ex);
            }
        }
    }
}
