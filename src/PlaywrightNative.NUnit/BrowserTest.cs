/*
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
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
using Microsoft.Playwright;
using NUnit.Framework;

namespace PlaywrightNative.NUnit;

/// <summary>
/// Shared <see cref="IBrowser"/> per NUnit worker. Mirrors
/// <see cref="Microsoft.Playwright.NUnit.BrowserTest"/> but launches through
/// PlaywrightNative instead of the Node driver.
/// </summary>
public class BrowserTest : PlaywrightTest
{
    private readonly List<IBrowserContext> _contexts = new();

    /// <summary>
    /// Gets the worker-scoped browser instance.
    /// </summary>
    public IBrowser Browser { get; private set; } = null!;

    /// <summary>
    /// Creates a new context and tracks it for tear-down.
    /// </summary>
    /// <param name="options">Optional context options.</param>
    /// <returns>The new browser context.</returns>
    public async Task<IBrowserContext> NewContext(BrowserContextOptions options = null)
    {
        IBrowserContext context = await Browser.NewContextAsync(options).ConfigureAwait(false);
        _contexts.Add(context);
        return context;
    }

    /// <summary>
    /// Registers or reuses the worker-scoped browser service.
    /// </summary>
    [SetUp]
    public async Task BrowserSetup()
    {
        BrowserService service = await BrowserService.Register(
            this,
            BrowserName,
            await LaunchOptionsAsync().ConfigureAwait(false)).ConfigureAwait(false);
        Browser = service.Browser;
    }

    /// <summary>
    /// Closes contexts created via <see cref="NewContext"/>.
    /// Always closes — even on failure — so contexts do not linger on a reused
    /// browser, and so failed tests do not rely solely on browser dispose for FD cleanup.
    /// </summary>
    [TearDown]
    public async Task BrowserTearDown()
    {
        // Snapshot first: CloseAsync can fire events that create/track more contexts
        // and would otherwise throw Collection was modified during enumeration.
        IBrowserContext[] contexts = _contexts.ToArray();
        _contexts.Clear();
        foreach (IBrowserContext context in contexts)
        {
            try
            {
                await context.CloseAsync().ConfigureAwait(false);
            }
            catch (Exception)
            {
                // Best-effort cleanup during teardown.
            }
        }

        Browser = null!;
    }

    /// <summary>
    /// Launch options. Default implementation installs/resolves the browser via
    /// <see cref="BrowserExecutable"/> and sets <c>ExecutablePath</c>.
    /// Override to customize (headed mode, extra args, etc.); call
    /// <c>await base.LaunchOptionsAsync()</c> when you still want the package install path.
    /// </summary>
    /// <returns>Launch options for the current <see cref="PlaywrightTest.BrowserName"/>.</returns>
    public virtual Task<BrowserTypeLaunchOptions> LaunchOptionsAsync()
        => BrowserExecutable.CreateLaunchOptionsAsync(BrowserName);
}
