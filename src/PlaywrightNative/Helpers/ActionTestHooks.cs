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
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Test-only pointer action hooks. Mirrors the upstream
    /// <c>__testHookBeforeStable</c>, <c>__testHookBeforeHitTarget</c>,
    /// <c>__testHookBeforePointerAction</c> and
    /// <c>__testHookAfterPointerAction</c> click options. Hooks flow with the
    /// async call that installed them through <see cref="Use"/>.
    /// </summary>
    internal sealed class ActionTestHooks
    {
        private static readonly AsyncLocal<ActionTestHooks> CurrentHooks = new AsyncLocal<ActionTestHooks>();

        /// <summary>Gets the hooks installed for the current async flow, if any.</summary>
        internal static ActionTestHooks Current => CurrentHooks.Value;

        /// <summary>Gets the hook that runs before the element stability wait.</summary>
        internal Func<Task> BeforeStable { get; init; }

        /// <summary>Gets the hook that runs after the click point is computed, before hit testing.</summary>
        internal Func<Task> BeforeHitTarget { get; init; }

        /// <summary>Gets the hook that runs right before the mouse press.</summary>
        internal Func<Task> BeforePointerAction { get; init; }

        /// <summary>Gets the hook that runs after the pointer action, before the navigation wait.</summary>
        internal Func<Task> AfterPointerAction { get; init; }

        /// <summary>
        /// Installs <paramref name="hooks"/> for the current async flow.
        /// </summary>
        /// <param name="hooks">The hooks to install.</param>
        /// <returns>A scope that restores the previous hooks when disposed.</returns>
        internal static IDisposable Use(ActionTestHooks hooks)
        {
            ActionTestHooks previous = CurrentHooks.Value;
            CurrentHooks.Value = hooks;
            return new Scope(previous);
        }

        private sealed class Scope : IDisposable
        {
            private readonly ActionTestHooks _previous;

            internal Scope(ActionTestHooks previous) => _previous = previous;

            public void Dispose() => CurrentHooks.Value = _previous;
        }
    }
}
