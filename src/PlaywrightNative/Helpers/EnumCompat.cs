/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using Microsoft.Playwright;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Legacy enum sentinels that official <c>Microsoft.Playwright</c> enums omit.
    /// </summary>
    internal static class EnumCompat
    {
        /// <summary>Legacy unset color-scheme sentinel (<c>Null</c> in official API).</summary>
        internal const ColorScheme UndefinedColorScheme = ColorScheme.Null;

        /// <summary>Legacy unset media sentinel.</summary>
        internal const Media UndefinedMedia = Media.Null;

        /// <summary>Legacy unset reduced-motion sentinel.</summary>
        internal const ReducedMotion UndefinedReducedMotion = ReducedMotion.Null;

        /// <summary>Legacy unset forced-colors sentinel.</summary>
        internal const ForcedColors UndefinedForcedColors = ForcedColors.Null;

        /// <summary>Legacy unset contrast sentinel.</summary>
        internal const Contrast UndefinedContrast = Contrast.Null;

        /// <summary>Legacy unset wait-for-selector state (defaults to visible).</summary>
        internal const WaitForSelectorState UndefinedWaitForSelectorState = (WaitForSelectorState)(-1);

        /// <summary>Legacy unset HAR content policy.</summary>
        internal const HarContentPolicy UndefinedHarContentPolicy = (HarContentPolicy)(-1);

        /// <summary>Legacy unset screenshot type.</summary>
        internal const ScreenshotType UndefinedScreenshotType = (ScreenshotType)(-1);

        /// <summary>Legacy unset element state.</summary>
        internal const ElementState UndefinedElementState = (ElementState)(-1);

        /// <summary>Legacy unset annotate position.</summary>
        internal const AnnotatePosition UndefinedAnnotatePosition = (AnnotatePosition)(-1);

        /// <summary>Legacy unset screencast cursor.</summary>
        internal const ScreencastCursor UndefinedScreencastCursor = (ScreencastCursor)(-1);

        /// <summary>Legacy unset ARIA role sentinel.</summary>
        internal const AriaRole UndefinedAriaRole = (AriaRole)(-1);

        /// <summary>
        /// Legacy <c>less</c> contrast value (not yet on official <see cref="Contrast"/>).
        /// </summary>
        internal const Contrast LessContrast = (Contrast)3;

        /// <summary>Legacy unset same-site sentinel.</summary>
        internal const Microsoft.Playwright.SameSiteAttribute UndefinedSameSite =
            (Microsoft.Playwright.SameSiteAttribute)(-1);
    }
}
