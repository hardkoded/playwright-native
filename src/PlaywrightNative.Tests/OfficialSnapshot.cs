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
using System.IO;
using NUnit.Framework;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Playwright .NET <c>toMatchSnapshot</c> compare for official screenshot titles.
    /// </summary>
    internal static class OfficialSnapshot
    {
        private const int PixelThreshold = 10;
        private const decimal TotalTolerance = 0.05m;

        internal static void ToMatchSnapshot(string expectedImageName, string actualPath)
            => ToMatchSnapshot(expectedImageName, File.ReadAllBytes(actualPath));

        internal static void ToMatchSnapshot(string expectedImageName, byte[] actual)
        {
            string goldenDir = Path.Combine(
                TestUtils.FindParentDirectory("PlaywrightNative.Tests"),
                "Screenshots",
                BrowserFolder());
            Directory.CreateDirectory(goldenDir);
            string expectedPath = Path.Combine(goldenDir, expectedImageName);

            if (Environment.GetEnvironmentVariable("UPDATE_SNAPSHOTS") == "1")
            {
                File.WriteAllBytes(expectedPath, actual);
                return;
            }

            Assert.That(File.Exists(expectedPath), Is.True, "Missing snapshot " + expectedPath + ". Set UPDATE_SNAPSHOTS=1.");

            using Image<Rgb24> expectedImage = Image.Load<Rgb24>(expectedPath);
            using Image<Rgb24> actualImage = Image.Load<Rgb24>(actual);

            if (expectedImage.Width != actualImage.Width || expectedImage.Height != actualImage.Height)
            {
                Assert.Fail(
                    "Expected image dimensions do not match actual image dimensions.\n" +
                    "Expected: " + expectedImage.Width + "x" + expectedImage.Height + "\n" +
                    "Actual: " + actualImage.Width + "x" + actualImage.Height);
                return;
            }

            int invalidPixels = 0;
            for (int y = 0; y < expectedImage.Height; y++)
            {
                for (int x = 0; x < expectedImage.Width; x++)
                {
                    Rgb24 pixelA = expectedImage[x, y];
                    Rgb24 pixelB = actualImage[x, y];
                    if (Math.Abs(pixelA.R - pixelB.R) > PixelThreshold ||
                        Math.Abs(pixelA.G - pixelB.G) > PixelThreshold ||
                        Math.Abs(pixelA.B - pixelB.B) > PixelThreshold)
                    {
                        invalidPixels++;
                    }
                }
            }

            decimal ratio = expectedImage.Height * expectedImage.Width == 0
                ? 0
                : (decimal)invalidPixels / (expectedImage.Height * expectedImage.Width);
            if (ratio > TotalTolerance)
            {
                Assert.Fail(
                    "Expected image to match snapshot but it did not. " +
                    invalidPixels + " pixels do not match.\n" +
                    "Set UPDATE_SNAPSHOTS=1 to update the snapshot.");
            }
        }

        private static string BrowserFolder()
        {
            if (TestConstants.IsWebKit)
            {
                return "webkit";
            }

            if (TestConstants.IsFirefox)
            {
                return "firefox";
            }

            return "chromium";
        }
    }
}
