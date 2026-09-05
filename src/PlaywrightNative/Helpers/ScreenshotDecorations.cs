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
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Injects Playwright screenshot decorations (disabled animations, hidden
    /// caret, caller stylesheet) for the duration of a capture.
    /// </summary>
    internal static class ScreenshotDecorations
    {
        internal const string DisableAnimationsCss = @"*, *::before, *::after {
  animation-delay: 0s !important;
  animation-duration: 0s !important;
  animation-play-state: paused !important;
  transition-duration: 0s !important;
  transition-delay: 0s !important;
}";

        internal const string HideCaretCss = "* { caret-color: transparent !important; }";

        internal const string HideCaretJs = @"(function() {
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
  window.__pwRestoreCaret = () => {
    for (const item of restore)
      item.element.style.setProperty('caret-color', item.value, item.priority);
    delete window.__pwRestoreCaret;
  };
})()";

        internal const string RestoreCaretJs = "window.__pwRestoreCaret && window.__pwRestoreCaret()";

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
  const infinite = [];
  const handle = (root) => {
    if (!root.getAnimations)
      return;
    for (const animation of root.getAnimations()) {
      try {
        if (!animation.effect || animation.playbackRate === 0)
          continue;
        const timing = animation.effect && animation.effect.getComputedTiming
          ? animation.effect.getComputedTiming()
          : null;
        if (timing && (timing.iterations === Infinity || timing.duration === Infinity)) {
          animation.cancel();
          infinite.push(animation);
        } else {
          animation.finish();
        }
      } catch (e) {
        try { animation.cancel(); } catch (e2) {}
      }
    }
  };
  for (const root of collectRoots(document, []))
    handle(root);
  window.__pwRestoreAnimations = () => {
    for (const animation of infinite) {
      try { animation.play(); } catch (e) {}
    }
    delete window.__pwRestoreAnimations;
  };
})()";

        internal const string RestoreAnimationsJs = "window.__pwRestoreAnimations && window.__pwRestoreAnimations()";

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
        /// </summary>
        /// <param name="animations">The screenshot animations option.</param>
        /// <param name="caret">The screenshot caret option.</param>
        /// <param name="style">Optional caller stylesheet.</param>
        /// <returns>The combined CSS, or an empty string.</returns>
        internal static string BuildCss(string animations, string caret, string style)
        {
            StringBuilder builder = new StringBuilder();
            if (IsDisabled(animations))
            {
                builder.Append(DisableAnimationsCss);
            }

            if (IsHideCaret(caret))
            {
                builder.Append(HideCaretCss);
            }

            if (!string.IsNullOrEmpty(style))
            {
                builder.Append(style);
            }

            return builder.ToString();
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
            string css = BuildCss(animations, caret, style);
            List<IElementHandle> tags = new List<IElementHandle>();
            try
            {
                if (css.Length > 0)
                {
                    await InjectStyleAsync(page, css, tags).ConfigureAwait(false);
                }

                if (IsHideCaret(caret))
                {
                    await EvaluateInFramesAsync(page, HideCaretJs).ConfigureAwait(false);
                }

                if (IsDisabled(animations))
                {
                    await FinishAnimationsAsync(page).ConfigureAwait(false);
                }

                await ScreenshotMask.ApplyAsync(page, mask, maskColor, tags).ConfigureAwait(false);
                await WaitForFontsAsync(page).ConfigureAwait(false);

                return await capture().ConfigureAwait(false);
            }
            finally
            {
                if (IsDisabled(animations))
                {
                    await EvaluateInFramesAsync(page, RestoreAnimationsJs).ConfigureAwait(false);
                }

                if (IsHideCaret(caret))
                {
                    await EvaluateInFramesAsync(page, RestoreCaretJs).ConfigureAwait(false);
                }

                await RemoveStyleAsync(tags).ConfigureAwait(false);
                gate.Release();
            }
        }

        /// <summary>
        /// Official screenshotter waits for <c>document.fonts.ready</c> and logs
        /// <c>waiting for fonts to load...</c> so a stalled webfont times out
        /// with that text.
        /// </summary>
        /// <param name="page">The page being captured.</param>
        /// <returns>A task that completes when fonts are ready or the frame is gone.</returns>
        private static async Task WaitForFontsAsync(IPage page)
        {
            try
            {
                await page.EvaluateAsync<object>(@"async () => {
  if (!document.fonts || document.fonts.status !== 'loading')
    return true;
  await Promise.race([
    document.fonts.ready,
    new Promise((_, reject) => window.addEventListener('pagehide', () => reject(new Error('navigating')), { once: true })),
    new Promise((_, reject) => setTimeout(() => reject(new Error('navigating')), 250))
  ]);
  return true;
}").ConfigureAwait(false);
            }
            catch (PlaywrightNativeException)
            {
            }
        }

        private static async Task InjectStyleAsync(IPage page, string css, List<IElementHandle> tags)
        {
            IElementHandle pageTag = await page.AddStyleTagAsync(new() { Content = css }).ConfigureAwait(false);
            if (pageTag != null)
            {
                tags.Add(pageTag);
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
                catch (PlaywrightNativeException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        private static async Task EvaluateInFramesAsync(IPage page, string expression)
        {
            IReadOnlyCollection<IFrame> frames = page.Frames;
            if (frames == null || frames.Count == 0)
            {
                try
                {
                    await page.EvaluateAsync(expression).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
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
                catch (PlaywrightNativeException)
                {
                }
                catch (TimeoutException)
                {
                }
            }
        }

        private static async Task FinishAnimationsAsync(IPage page)
        {
            IReadOnlyCollection<IFrame> frames = page.Frames;
            if (frames == null || frames.Count == 0)
            {
                await page.EvaluateAsync(FinishAnimationsJs).ConfigureAwait(false);
                return;
            }

            foreach (IFrame frame in frames)
            {
                try
                {
                    await frame.EvaluateAsync(FinishAnimationsJs).ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }

        private static async Task RemoveStyleAsync(List<IElementHandle> tags)
        {
            foreach (IElementHandle tag in tags)
            {
                try
                {
                    await tag.EvaluateAsync("el => { if (el && el.remove) el.remove(); }").ConfigureAwait(false);
                }
                catch (PlaywrightNativeException)
                {
                }
            }
        }
    }
}
