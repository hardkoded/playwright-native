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
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Finds a frame by URL matcher or name. Shared by Chromium and WebKit page implementations.
    /// </summary>
    internal static class FrameLookup
    {
        /// <summary>
        /// Returns the first frame whose URL matches the first non-null matcher.
        /// </summary>
        /// <param name="frames">Frames to search.</param>
        /// <param name="urlString">A glob pattern or exact URL.</param>
        /// <param name="urlRegex">A regular expression.</param>
        /// <param name="urlFunc">A predicate receiving the frame URL.</param>
        /// <returns>The matching frame, or <see langword="null"/>.</returns>
        internal static IFrame ByUrl(IEnumerable<IFrame> frames, string urlString, Regex urlRegex, Func<string, bool> urlFunc)
        {
            if (frames == null)
            {
                return null;
            }

            foreach (IFrame frame in frames)
            {
                if (frame != null && UrlMatcher.Matches(frame.Url, urlString, urlRegex, urlFunc))
                {
                    return frame;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns the first frame whose <see cref="IFrame.Name"/> equals <paramref name="name"/>.
        /// </summary>
        /// <param name="frames">Frames to search.</param>
        /// <param name="name">The frame name.</param>
        /// <returns>The matching frame, or <see langword="null"/>.</returns>
        internal static IFrame ByName(IEnumerable<IFrame> frames, string name)
        {
            if (frames == null || name == null)
            {
                return null;
            }

            foreach (IFrame frame in frames)
            {
                if (frame != null && string.Equals(frame.Name, name, StringComparison.Ordinal))
                {
                    return frame;
                }
            }

            return null;
        }

        /// <summary>
        /// Returns frames with the main frame first, then the remaining frames.
        /// Matches official Playwright <c>page.frames()</c> order.
        /// </summary>
        /// <param name="main">The main frame.</param>
        /// <param name="frames">All frames, in any order.</param>
        /// <returns>An ordered frame list.</returns>
        internal static IReadOnlyCollection<IFrame> MainFirst(IFrame main, IEnumerable<IFrame> frames)
        {
            List<IFrame> ordered = new List<IFrame>();
            if (main != null)
            {
                ordered.Add(main);
            }

            if (frames == null)
            {
                return ordered;
            }

            foreach (IFrame frame in frames)
            {
                if (frame != null && !ReferenceEquals(frame, main))
                {
                    ordered.Add(frame);
                }
            }

            return ordered;
        }

        /// <summary>
        /// Official Playwright <c>page.frames()</c> order: depth-first walk of
        /// <see cref="IFrame.ChildFrames"/> starting at the main frame.
        /// </summary>
        /// <param name="main">The main frame.</param>
        /// <returns>The frame tree in document-attach order.</returns>
        internal static IReadOnlyList<IFrame> DepthFirst(IFrame main)
        {
            List<IFrame> ordered = new List<IFrame>();
            CollectDepthFirst(main, ordered);
            return ordered;
        }

        private static void CollectDepthFirst(IFrame frame, List<IFrame> ordered)
        {
            if (frame == null)
            {
                return;
            }

            ordered.Add(frame);
            foreach (IFrame child in frame.ChildFrames)
            {
                CollectDepthFirst(child, ordered);
            }
        }
    }
}
