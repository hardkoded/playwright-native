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
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.WebKit;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Injects Playwright screenshot decorations (disabled animations, hidden
    /// caret, caller stylesheet) for the duration of a capture.
    /// </summary>
    internal static class ScreenshotDecorations
    {
        internal const string NavigatingMessage = "Cannot take a screenshot while page is navigating";

        // Attribute/type selectors beat page rules like `div { caret-color: #000 !important; }`.
        // A bare `*` loses that specificity battle and leaves the caret visible.
        // Inject via evaluate (not AddStyleTag) so a navigation race becomes a
        // swallowed evaluate error instead of a raw CDP context-id failure.
        // Blur the focused field (caret cannot paint without focus) and force
        // caret-color transparent. Resolve after two animation frames so WebKit's
        // snapshot sees the post-blur frame.
        // Must be an IIFE: a bare `() => { ... }` expression only returns the
        // function object and never runs (unlike FinishAnimationsJs / SyncAnimationsJs).
        internal const string HideCaretJs = @"(() => {
  const collectRoots = (root, roots) => {
    roots.push(root);
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT);
    do {
      const node = walker.currentNode;
      const shadowRoot = node instanceof Element ? node.shadowRoot : null;
      if (shadowRoot)
        collectRoots(shadowRoot, roots);
    } while (walker.nextNode());
    return roots;
  };
  const roots = collectRoots(document, []);
  const restore = [];
  const styleTags = [];
  for (const root of roots) {
    const styleTag = document.createElement('style');
    styleTag.textContent = 'input, textarea, [contenteditable] { caret-color: transparent !important; }';
    if (root === document)
      document.documentElement.append(styleTag);
    else
      root.append(styleTag);
    styleTags.push(styleTag);
  }
  const active = document.activeElement;
  let refocus = null;
  if (active && active.matches && active.matches('input,textarea,[contenteditable]')) {
    refocus = active;
    active.blur();
  }
  for (const root of roots) {
    root.querySelectorAll('input,textarea,[contenteditable]').forEach(element => {
      restore.push({
        element,
        value: element.style.getPropertyValue('caret-color'),
        priority: element.style.getPropertyPriority('caret-color')
      });
      element.style.setProperty('caret-color', 'transparent', 'important');
    });
  }
  document.documentElement.getBoundingClientRect();
  window.__pwRestoreCaret = () => {
    for (const tag of styleTags)
      tag.remove();
    for (const item of restore)
      item.element.style.setProperty('caret-color', item.value, item.priority);
    if (refocus && typeof refocus.focus === 'function') {
      try { refocus.focus({ preventScroll: true }); } catch (e) {}
    }
    delete window.__pwRestoreCaret;
  };
  return new Promise(resolve => {
    requestAnimationFrame(() => requestAnimationFrame(resolve));
  });
})()";

        internal const string RestoreCaretJs = "window.__pwRestoreCaret && window.__pwRestoreCaret()";

        // Matches upstream screenshotter.inPagePrepareForScreenshots disableAnimations
        // branch: finite endTime → finish(), infinite → cancel() + resume via play().
        // Must run in the same JS world as RestoreAnimationsJs (utility on WebKit).
        internal const string FinishAnimationsJs = @"(() => {
  const collectRoots = (root, roots) => {
    roots.push(root);
    const walker = document.createTreeWalker(root, NodeFilter.SHOW_ELEMENT);
    do {
      const node = walker.currentNode;
      const shadowRoot = node instanceof Element ? node.shadowRoot : null;
      if (shadowRoot)
        collectRoots(shadowRoot, roots);
    } while (walker.nextNode());
    return roots;
  };
  const infiniteAnimationsToResume = new Set();
  const handleAnimations = (root) => {
    if (!root.getAnimations)
      return;
    for (const animation of root.getAnimations()) {
      if (!animation.effect || animation.playbackRate === 0 || infiniteAnimationsToResume.has(animation))
        continue;
      const endTime = animation.effect.getComputedTiming().endTime;
      if (Number.isFinite(endTime)) {
        try {
          animation.finish();
        } catch (e) {
        }
      } else {
        try {
          animation.cancel();
          infiniteAnimationsToResume.add(animation);
        } catch (e) {
        }
      }
    }
  };
  const roots = collectRoots(document, []);
  const cleanupCallbacks = [];
  for (const root of roots) {
    const handleRootAnimations = handleAnimations.bind(null, root);
    handleRootAnimations();
    root.addEventListener('transitionrun', handleRootAnimations);
    root.addEventListener('animationstart', handleRootAnimations);
    cleanupCallbacks.push(() => {
      root.removeEventListener('transitionrun', handleRootAnimations);
      root.removeEventListener('animationstart', handleRootAnimations);
    });
  }
  cleanupCallbacks.push(() => {
    for (const animation of infiniteAnimationsToResume) {
      try {
        animation.play();
      } catch (e) {
      }
    }
  });
  window.__pwRestoreAnimations = () => {
    for (const cleanupCallback of cleanupCallbacks)
      cleanupCallback();
    delete window.__pwRestoreAnimations;
  };
})()";

        internal const string RestoreAnimationsJs = "window.__pwRestoreAnimations && window.__pwRestoreAnimations()";

        // Official WebKit screenshotter toggles an empty stylesheet so pending
        // CSS animations sync before capture (shouldToggleStyleSheetToSyncAnimations).
        internal const string SyncAnimationsJs = @"(() => {
  const style = document.createElement('style');
  style.textContent = 'body {}';
  document.head.appendChild(style);
  document.documentElement.getBoundingClientRect();
  style.remove();
})()";

        private static readonly ConcurrentDictionary<int, SemaphoreSlim> ScreenshotGates = new ConcurrentDictionary<int, SemaphoreSlim>();

        /// <summary>
        /// Returns <see langword="true"/> when <paramref name="animations"/> is <c>disabled</c>.
        /// </summary>
        /// <param name="animations">The screenshot animations option.</param>
        /// <returns>Whether animations should be frozen.</returns>
        internal static bool IsDisabled(string animations)
            => string.Equals(animations, "disabled", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Official screenshotter hides the caret unless <paramref name="caret"/>
        /// is <c>initial</c>.
        /// </summary>
        /// <param name="caret">The screenshot caret option.</param>
        /// <returns>Whether the caret should be hidden.</returns>
        internal static bool IsHideCaret(string caret)
            => !string.Equals(caret, "initial", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Builds the stylesheet injected for the given screenshot options.
        /// Caret hiding uses type/attribute selectors (not <c>*</c>) so page
        /// rules like <c>input { caret-color: red !important }</c> lose to an
        /// equally specific later sheet; <see cref="HideCaretJs"/> still
        /// applies inline styles + blur as a backstop.
        /// Animations are frozen via <see cref="FinishAnimationsJs"/> (matching
        /// upstream), not CSS: forcing <c>animation-duration: 0s</c> here snaps
        /// a running CSS animation to completion and drops it from
        /// <c>getAnimations()</c> before that script can cancel/finish it,
        /// which suppresses the finish/cancel events official tests assert on.
        /// </summary>
        /// <param name="caret">The screenshot caret option.</param>
        /// <param name="style">Optional caller stylesheet.</param>
        /// <returns>The combined CSS, or an empty string.</returns>
        internal static string BuildCss(string caret, string style)
        {
            string caretCss = IsHideCaret(caret)
                ? "input, textarea, [contenteditable] { caret-color: transparent !important; }"
                : string.Empty;
            if (string.IsNullOrEmpty(style))
            {
                return caretCss;
            }

            return string.IsNullOrEmpty(caretCss) ? style : caretCss + style;
        }

        /// <summary>
        /// Injects decorations, runs <paramref name="capture"/>, then removes them.
        /// </summary>
        /// <param name="page">The page being captured.</param>
        /// <param name="animations">The screenshot animations option.</param>
        /// <param name="caret">The screenshot caret option.</param>
        /// <param name="style">Optional caller stylesheet.</param>
        /// <param name="capture">The screenshot capture.</param>
        /// <param name="mask">Locators whose matches are painted over.</param>
        /// <param name="maskColor">Overlay color. Defaults to magenta.</param>
        /// <returns>The screenshot bytes.</returns>
        internal static async Task<byte[]> CaptureAsync(
            IPage page,
            string animations,
            string caret,
            string style,
            Func<Task<byte[]>> capture,
            IEnumerable<ILocator> mask = null,
            string maskColor = null)
        {
            if (page == null)
            {
                throw new ArgumentNullException(nameof(page));
            }

            if (capture == null)
            {
                throw new ArgumentNullException(nameof(capture));
            }

            SemaphoreSlim gate = ScreenshotGates.GetOrAdd(page.GetHashCode(), _ => new SemaphoreSlim(1, 1));
            await gate.WaitAsync().ConfigureAwait(false);
            string css = BuildCss(caret, style);
            List<IElementHandle> tags = new List<IElementHandle>();
            bool hideCaret = IsHideCaret(caret);
            bool disableAnimations = IsDisabled(animations);
            try
            {
                try
                {
                    // WebKit: empty stylesheet toggle forces layout so CSS
                    // animations are synchronized before capture (upstream).
                    if (page is WKPage)
                    {
                        await EvaluateInFramesAsync(page, SyncAnimationsJs).ConfigureAwait(false);
                    }

                    if (css.Length > 0)
                    {
                        await InjectStyleAsync(page, css, tags).ConfigureAwait(false);
                    }

                    if (hideCaret)
                    {
                        await EvaluateInFramesAsync(page, HideCaretJs).ConfigureAwait(false);
                    }

                    if (disableAnimations)
                    {
                        await FinishAnimationsAsync(page).ConfigureAwait(false);
                    }

                    await ScreenshotMask.ApplyAsync(page, mask, maskColor, tags).ConfigureAwait(false);
                    await WaitForFontsAsync(page).ConfigureAwait(false);

                    return await capture().ConfigureAwait(false);
                }
                catch (Exception ex) when (DestroyedContext.IsDestroyedContext(ex))
                {
                    // Official screenshotter surfaces navigation races as this message
                    // rather than raw CDP "Cannot find context with specified id".
                    throw new PlaywrightException(NavigatingMessage);
                }
            }
            finally
            {
                await CleanupDecorationsAsync(page, tags, hideCaret, disableAnimations).ConfigureAwait(false);
                gate.Release();
            }
        }

        private static async Task CleanupDecorationsAsync(
            IPage page,
            List<IElementHandle> tags,
            bool hideCaret,
            bool disableAnimations)
        {
            if (disableAnimations)
            {
                await EvaluateInFramesAsync(page, RestoreAnimationsJs).ConfigureAwait(false);
            }

            if (hideCaret)
            {
                await EvaluateInFramesAsync(page, RestoreCaretJs).ConfigureAwait(false);
            }

            await RemoveStyleAsync(tags).ConfigureAwait(false);
        }

        /// <summary>
        /// Official screenshotter waits for <c>document.fonts.ready</c> and logs
        /// <c>waiting for fonts to load...</c> so a stalled webfont times out
        /// with that text via <see cref="ScreenshotTimeout"/>.
        /// </summary>
        /// <param name="page">The page being captured.</param>
        /// <returns>A task that completes when fonts are ready or the frame is gone.</returns>
        private static async Task WaitForFontsAsync(IPage page)
        {
            try
            {
                // Upstream: frame.nonStallingEvaluateInExistingContext('document.fonts.ready', 'utility')
                // Do not race a short timer — ScreenshotTimeout owns the deadline.
                if (page is WKPage webkit)
                {
                    await EvaluateInWebKitUtilityAsync(webkit, "document.fonts && document.fonts.ready").ConfigureAwait(false);
                }
                else
                {
                    await page.EvaluateAsync("document.fonts && document.fonts.ready").ConfigureAwait(false);
                }
            }
            catch (PlaywrightException)
            {
            }
            catch (TimeoutException)
            {
            }
        }

        private static async Task InjectStyleAsync(IPage page, string css, List<IElementHandle> tags)
        {
            try
            {
                IElementHandle pageTag = await page.AddStyleTagAsync(new() { Content = css }).ConfigureAwait(false);
                if (pageTag != null)
                {
                    tags.Add(pageTag);
                }
            }
            catch (Exception ex) when (DestroyedContext.IsDestroyedContext(ex))
            {
                throw new PlaywrightException(NavigatingMessage);
            }

            IReadOnlyCollection<IFrame> frames = page.Frames;
            if (frames == null)
            {
                return;
            }

            foreach (IFrame frame in frames)
            {
                if (frame == null || frame.ParentFrame == null || frame.IsDetached)
                {
                    continue;
                }

                try
                {
                    IElementHandle tag = await frame.AddStyleTagAsync(new() { Content = css }).ConfigureAwait(false);
                    if (tag != null)
                    {
                        tags.Add(tag);
                    }
                }
                catch (Exception ex) when (DestroyedContext.IsDestroyedContext(ex))
                {
                    throw new PlaywrightException(NavigatingMessage);
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        private static async Task EvaluateInFramesAsync(IPage page, string expression)
        {
            if (page is WKPage webkit)
            {
                await EvaluateInWebKitUtilityAsync(webkit, expression).ConfigureAwait(false);
                return;
            }

            IReadOnlyCollection<IFrame> frames = page.Frames;
            if (frames == null || frames.Count == 0)
            {
                try
                {
                    await page.EvaluateAsync(expression).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }

                return;
            }

            foreach (IFrame frame in frames)
            {
                if (frame == null || frame.IsDetached)
                {
                    continue;
                }

                try
                {
                    await frame.EvaluateAsync(expression).ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        private static async Task EvaluateInWebKitUtilityAsync(WKPage page, string expression)
        {
            IReadOnlyCollection<IFrame> frames = page.Frames;
            List<WebKitFrame> targets = new List<WebKitFrame>();
            if (frames != null)
            {
                foreach (IFrame frame in frames)
                {
                    if (frame is WebKitFrame webkitFrame && !webkitFrame.IsDetached)
                    {
                        targets.Add(webkitFrame);
                    }
                }
            }

            if (targets.Count == 0 && page.MainFrame is WebKitFrame main)
            {
                targets.Add(main);
            }

            foreach (WebKitFrame frame in targets)
            {
                try
                {
                    WKExecutionContext utility = await page.GetUtilityWorldAsync(frame.GetWKFrame()).ConfigureAwait(false);
                    if (utility != null)
                    {
                        await utility.EvaluateAsync(expression).ConfigureAwait(false);
                    }
                    else
                    {
                        await frame.EvaluateAsync(expression).ConfigureAwait(false);
                    }
                }
                catch (PlaywrightException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        private static Task FinishAnimationsAsync(IPage page)
        {
            // Must share EvaluateInFramesAsync with RestoreAnimationsJs so
            // window.__pwRestoreAnimations is visible on resume (WebKit utility).
            return EvaluateInFramesAsync(page, FinishAnimationsJs);
        }

        private static async Task RemoveStyleAsync(List<IElementHandle> tags)
        {
            foreach (IElementHandle tag in tags)
            {
                try
                {
                    await tag.EvaluateAsync("el => { if (el && el.remove) el.remove(); }").ConfigureAwait(false);
                }
                catch (PlaywrightException)
                {
                }
            }
        }
    }
}
