/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
namespace PlaywrightSharp
{
    /// <summary>
    /// Official <c>tracing.start</c> options, plus PlaywrightSharp screen/aria snapshot flags.
    /// </summary>
    public class TracingStartOptions : Microsoft.Playwright.TracingStartOptions
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="TracingStartOptions"/> class.
        /// </summary>
        public TracingStartOptions()
        {
        }

        /// <summary>
        /// Initializes a new instance of the <see cref="TracingStartOptions"/> class.
        /// </summary>
        /// <param name="clone">Options to copy.</param>
        public TracingStartOptions(TracingStartOptions clone)
            : base(clone)
        {
            if (clone == null)
            {
                return;
            }

            ScreenSnapshots = clone.ScreenSnapshots;
            AriaSnapshots = clone.AriaSnapshots;
        }

        /// <summary>
        /// Official <c>snapshots: { screen: true }</c>. Captures PNG action
        /// screenshots with before/action/after phases.
        /// </summary>
        public bool? ScreenSnapshots { get; set; }

        /// <summary>
        /// Official <c>snapshots: { aria: true }</c>. Captures aria snapshots
        /// with before/action/after phases.
        /// </summary>
        public bool? AriaSnapshots { get; set; }
    }
}
