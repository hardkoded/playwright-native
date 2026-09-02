/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System.Threading.Tasks;
using PlaywrightNative.Chromium;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Resumes a debugger-paused renderer when the caller starts
    /// <c>waitForLoadState</c>. Noopener network popups can stay paused
    /// after attach; resuming here (not at attach) keeps the pending
    /// navigation from being dropped.
    /// </summary>
    internal static class LoadStateResume
    {
        /// <summary>
        /// Sends <c>Runtime.runIfWaitingForDebugger</c> if <paramref name="session"/> is set.
        /// Fire-and-forget so the load-state waiter can subscribe first.
        /// </summary>
        /// <param name="session">The Chromium page session, or <see langword="null"/>.</param>
        internal static void TryResume(CRSession session)
        {
            if (session == null)
            {
                return;
            }

            _ = ResumeCoreAsync(session);
        }

        private static async Task ResumeCoreAsync(CRSession session)
        {
            try
            {
                await session.SendAsync("Runtime.runIfWaitingForDebugger").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
                // Session already closed, crashed, or already running.
            }
        }
    }
}
