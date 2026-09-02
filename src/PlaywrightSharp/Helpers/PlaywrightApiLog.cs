using System;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Official client <c>_wrapApiCall</c> logger from
    /// <c>library/logger.spec.ts</c>: <c>{api} started</c> /
    /// <c>{api} succeeded</c> at info.
    /// </summary>
    internal static class PlaywrightApiLog
    {
        /// <summary>
        /// Runs <paramref name="action"/> and logs start / success / failure.
        /// </summary>
        /// <typeparam name="T">The action result.</typeparam>
        /// <param name="logger">Official logger, or <see langword="null"/>.</param>
        /// <param name="apiName">Official API name, for example <c>browser.newContext</c>.</param>
        /// <param name="action">The API body.</param>
        /// <returns>The action result.</returns>
        internal static async Task<T> RunAsync<T>(IPlaywrightLogger logger, string apiName, Func<Task<T>> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            Write(logger, apiName, " started");
            try
            {
                T result = await action().ConfigureAwait(false);
                Write(logger, apiName, " succeeded");
                return result;
            }
            catch
            {
                Write(logger, apiName, " failed");
                throw;
            }
        }

        /// <summary>
        /// Runs <paramref name="action"/> and logs start / success / failure.
        /// </summary>
        /// <param name="logger">Official logger, or <see langword="null"/>.</param>
        /// <param name="apiName">Official API name.</param>
        /// <param name="action">The API body.</param>
        /// <returns>A task that completes when the action finishes.</returns>
        internal static Task RunAsync(IPlaywrightLogger logger, string apiName, Func<Task> action)
        {
            if (action == null)
            {
                throw new ArgumentNullException(nameof(action));
            }

            return RunAsync(logger, apiName, async () =>
            {
                await action().ConfigureAwait(false);
                return true;
            });
        }

        private static void Write(IPlaywrightLogger logger, string apiName, string suffix)
        {
            if (logger == null || !logger.IsEnabled("api", PlaywrightLogSeverity.Info))
            {
                return;
            }

            logger.Log("api", PlaywrightLogSeverity.Info, apiName + suffix);
        }
    }
}
