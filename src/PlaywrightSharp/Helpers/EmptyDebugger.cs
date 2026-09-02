/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// No-op <see cref="IDebugger"/> until browser-context debugging is wired.
    /// </summary>
    internal sealed class EmptyDebugger : IDebugger
    {
        /// <inheritdoc/>
        public event EventHandler PausedStateChanged;

        /// <inheritdoc/>
        public Microsoft.Playwright.DebuggerPausedDetails PausedDetails => null;

        /// <inheritdoc/>
        public Task RequestPauseAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task ResumeAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task NextAsync() => Task.CompletedTask;

        /// <inheritdoc/>
        public Task RunToAsync(Location location) => Task.CompletedTask;
    }
}
