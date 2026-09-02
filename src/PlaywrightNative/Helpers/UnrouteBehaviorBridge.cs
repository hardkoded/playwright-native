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
using MicrosoftPlaywright = Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>Maps legacy <see cref="UnrouteBehavior"/> to official values.</summary>
    internal static class UnrouteBehaviorBridge
    {
        internal static MicrosoftPlaywright.UnrouteBehavior ToOfficial(UnrouteBehavior behavior)
            => behavior switch
            {
                UnrouteBehavior.Wait => MicrosoftPlaywright.UnrouteBehavior.Wait,
                UnrouteBehavior.IgnoreErrors => MicrosoftPlaywright.UnrouteBehavior.IgnoreErrors,
                UnrouteBehavior.Default => MicrosoftPlaywright.UnrouteBehavior.Default,
                _ => MicrosoftPlaywright.UnrouteBehavior.Default,
            };
    }
}
