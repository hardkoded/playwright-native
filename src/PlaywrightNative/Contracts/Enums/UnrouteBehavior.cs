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
namespace PlaywrightNative
{
    /// <summary>
    /// Legacy unroute behavior including an explicit <see cref="Undefined"/> sentinel.
    /// </summary>
    public enum UnrouteBehavior
    {
        /// <summary>Unset behavior (defaults to <see cref="Default"/>).</summary>
        Undefined = 0,

        /// <summary>Wait for in-flight handlers to finish.</summary>
        Wait = 1,

        /// <summary>Stop routing and ignore handler errors.</summary>
        IgnoreErrors = 2,

        /// <summary>Stop routing without waiting.</summary>
        Default = 3,
    }
}
