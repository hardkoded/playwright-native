/*
 * MIT License
 *
 * Copyright (c) Microsoft Corporation.
 * Modifications copyright (c) Dario Kondratiuk.
 */
using System.Threading.Tasks;
using Microsoft.Playwright.NUnit;

namespace PlaywrightSharp.NUnit;

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
