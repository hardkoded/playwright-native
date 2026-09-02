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
using System.Threading.Tasks;
using Microsoft.Playwright.NUnit;

namespace PlaywrightNative.NUnit;

/// <summary>
/// Per-worker service pool and lifecycle. Extends
/// <see cref="Microsoft.Playwright.NUnit.WorkerAwareTest"/>.
/// </summary>
public class WorkerAwareTest : Microsoft.Playwright.NUnit.WorkerAwareTest
{
}

/// <summary>
/// Worker-scoped service registered via <see cref="Microsoft.Playwright.NUnit.WorkerAwareTest.RegisterService{T}"/>.
/// </summary>
public interface IWorkerService : Microsoft.Playwright.NUnit.IWorkerService
{
}
