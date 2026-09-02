/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Compares screenshot bytes for <c>ToHaveScreenshot</c>.
    /// </summary>
    internal static class ScreenshotComparer
    {
        private static readonly byte[] PngSignature = new byte[] { 137, 80, 78, 71, 13, 10, 26, 10 };

        /// <summary>
        /// Captures <paramref name="page"/> and compares it to <paramref name="expected"/>.
        /// </summary>
        /// <param name="page">The page to capture.</param>
        /// <param name="expected">Expected image bytes.</param>
        /// <param name="maxDiffPixels">Maximum differing pixels, or <see langword="null"/>.</param>
        /// <param name="maxDiffPixelRatio">Maximum differing pixel fraction, or <see langword="null"/>.</param>
        /// <param name="threshold">YIQ threshold, or <see langword="null"/>.</param>
        /// <param name="animations">Screenshot animations option.</param>
        /// <param name="caret">Screenshot caret option.</param>
        /// <param name="omitBackground">Hide the default background.</param>
        /// <param name="mask">Locators painted over for the capture.</param>
        /// <param name="maskColor">Overlay color for <paramref name="mask"/>.</param>
        /// <returns><see langword="true"/> when the screenshots match.</returns>
        internal static async Task<bool> MatchesAsync(
            IPage page,
            byte[] expected,
            int? maxDiffPixels = null,
            float? maxDiffPixelRatio = null,
            float? threshold = null,
            string animations = null,
            string caret = null,
            bool? omitBackground = null,
            IEnumerable<ILocator> mask = null,
            string maskColor = null)
        {
            try
            {
                byte[] actual = await page.ScreenshotAsync(
                    omitBackground: omitBackground,
                    timeout: 2000,
                    animations: animations,
                    caret: caret,
                    mask: mask,
                    maskColor: maskColor).ConfigureAwait(false);
                return Matches(actual, expected, maxDiffPixels, maxDiffPixelRatio, threshold);
            }
            catch (PlaywrightSharpException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// Captures <paramref name="handle"/> and compares it to <paramref name="expected"/>.
        /// </summary>
        /// <param name="handle">The element to capture.</param>
        /// <param name="expected">Expected image bytes.</param>
        /// <param name="maxDiffPixels">Maximum differing pixels, or <see langword="null"/>.</param>
        /// <param name="maxDiffPixelRatio">Maximum differing pixel fraction, or <see langword="null"/>.</param>
        /// <param name="threshold">YIQ threshold, or <see langword="null"/>.</param>
        /// <param name="animations">Screenshot animations option.</param>
        /// <param name="caret">Screenshot caret option.</param>
        /// <param name="omitBackground">Hide the default background.</param>
        /// <param name="mask">Locators painted over for the capture.</param>
        /// <param name="maskColor">Overlay color for <paramref name="mask"/>.</param>
        /// <returns><see langword="true"/> when the screenshots match.</returns>
        internal static async Task<bool> MatchesAsync(
            IElementHandle handle,
            byte[] expected,
            int? maxDiffPixels = null,
            float? maxDiffPixelRatio = null,
            float? threshold = null,
            string animations = null,
            string caret = null,
            bool? omitBackground = null,
            IEnumerable<ILocator> mask = null,
            string maskColor = null)
        {
            try
            {
                byte[] actual = await handle.ScreenshotAsync(
                    omitBackground: omitBackground,
                    timeout: 2000,
                    animations: animations,
                    caret: caret,
                    mask: mask,
                    maskColor: maskColor).ConfigureAwait(false);
                return Matches(actual, expected, maxDiffPixels, maxDiffPixelRatio, threshold);
            }
            catch (PlaywrightSharpException)
            {
                return false;
            }
            catch (TimeoutException)
            {
                return false;
            }
        }

        /// <summary>
        /// Throws when a screenshot-expect tolerance is out of range.
        /// </summary>
        /// <param name="maxDiffPixels">Maximum differing pixels.</param>
        /// <param name="maxDiffPixelRatio">Maximum differing pixel fraction.</param>
        /// <param name="threshold">YIQ threshold.</param>
        internal static void ValidateTolerance(int? maxDiffPixels, float? maxDiffPixelRatio, float? threshold)
        {
            if (maxDiffPixels.HasValue && maxDiffPixels.Value < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(maxDiffPixels), "maxDiffPixels must not be negative.");
            }

            if (maxDiffPixelRatio.HasValue && (maxDiffPixelRatio.Value < 0f || maxDiffPixelRatio.Value > 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(maxDiffPixelRatio), "maxDiffPixelRatio must be between 0 and 1.");
            }

            if (threshold.HasValue && (threshold.Value < 0f || threshold.Value > 1f))
            {
                throw new ArgumentOutOfRangeException(nameof(threshold), "threshold must be between 0 and 1.");
            }
        }

        /// <summary>
        /// Returns whether <paramref name="actual"/> matches <paramref name="expected"/>.
        /// Byte-identical buffers match; otherwise PNG pixels are compared.
        /// </summary>
        /// <param name="actual">Captured image bytes.</param>
        /// <param name="expected">Expected image bytes.</param>
        /// <param name="maxDiffPixels">Maximum differing pixels, or <see langword="null"/>.</param>
        /// <param name="maxDiffPixelRatio">Maximum differing pixel fraction, or <see langword="null"/>.</param>
        /// <param name="threshold">YIQ threshold, or <see langword="null"/>.</param>
        /// <returns><see langword="true"/> when the images match.</returns>
        internal static bool Matches(
            byte[] actual,
            byte[] expected,
            int? maxDiffPixels = null,
            float? maxDiffPixelRatio = null,
            float? threshold = null)
        {
            if (actual == null || expected == null)
            {
                return false;
            }

            if (actual.AsSpan().SequenceEqual(expected))
            {
                return true;
            }

            if (!TryDecodePng(actual, out int actualWidth, out int actualHeight, out byte[] actualPixels))
            {
                return false;
            }

            if (!TryDecodePng(expected, out int expectedWidth, out int expectedHeight, out byte[] expectedPixels))
            {
                return false;
            }

            if (actualWidth != expectedWidth || actualHeight != expectedHeight)
            {
                return false;
            }

            bool hasTolerance = maxDiffPixels.HasValue || maxDiffPixelRatio.HasValue || threshold.HasValue;
            if (!hasTolerance)
            {
                return actualPixels.AsSpan().SequenceEqual(expectedPixels);
            }

            float yiq = threshold ?? 0.2f;
            int different = CountDifferentPixels(actualPixels, expectedPixels, yiq);
            int total = actualWidth * actualHeight;
            if (maxDiffPixels.HasValue && different > maxDiffPixels.Value)
            {
                return false;
            }

            if (maxDiffPixelRatio.HasValue && total > 0 && (float)different / total > maxDiffPixelRatio.Value)
            {
                return false;
            }

            if (!maxDiffPixels.HasValue && !maxDiffPixelRatio.HasValue)
            {
                return different == 0;
            }

            return true;
        }

        private static int CountDifferentPixels(byte[] actual, byte[] expected, float threshold)
        {
            double maxDelta = 35215.0 * threshold * threshold;
            int different = 0;
            for (int i = 0; i < actual.Length; i += 4)
            {
                if (YiqDelta(actual, expected, i) > maxDelta)
                {
                    different++;
                }
            }

            return different;
        }

        private static double YiqDelta(byte[] actual, byte[] expected, int offset)
        {
            int r = actual[offset] - expected[offset];
            int g = actual[offset + 1] - expected[offset + 1];
            int b = actual[offset + 2] - expected[offset + 2];
            double y = (r * 0.29889531) + (g * 0.58662247) + (b * 0.11448223);
            double i = (r * 0.59597799) - (g * 0.27417610) - (b * 0.32180189);
            double q = (r * 0.21147017) - (g * 0.52261711) + (b * 0.31114694);
            return (0.5053 * y * y) + (0.299 * i * i) + (0.1957 * q * q);
        }

        private static bool TryDecodePng(byte[] png, out int width, out int height, out byte[] pixels)
        {
            width = 0;
            height = 0;
            pixels = null;
            if (png == null || png.Length < 8 || !png.AsSpan(0, 8).SequenceEqual(PngSignature))
            {
                return false;
            }

            int offset = 8;
            int bitDepth = 0;
            int colorType = 0;
            int interlace = 0;
            using MemoryStream idat = new MemoryStream();
            while (offset + 12 <= png.Length)
            {
                int length = BinaryPrimitives.ReadInt32BigEndian(png.AsSpan(offset));
                offset += 4;
                if (length < 0 || offset + 4 + length + 4 > png.Length)
                {
                    return false;
                }

                uint type = BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(offset));
                offset += 4;
                ReadOnlySpan<byte> data = png.AsSpan(offset, length);
                offset += length + 4;

                if (type == 0x49484452)
                {
                    if (length < 13)
                    {
                        return false;
                    }

                    width = BinaryPrimitives.ReadInt32BigEndian(data);
                    height = BinaryPrimitives.ReadInt32BigEndian(data.Slice(4));
                    bitDepth = data[8];
                    colorType = data[9];
                    interlace = data[12];
                    if (width <= 0 || height <= 0 || bitDepth != 8 || interlace != 0)
                    {
                        return false;
                    }

                    if (colorType != 2 && colorType != 6)
                    {
                        return false;
                    }
                }
                else if (type == 0x49444154)
                {
                    idat.Write(data);
                }
                else if (type == 0x49454E44)
                {
                    break;
                }
            }

            if (width == 0 || height == 0 || idat.Length == 0)
            {
                return false;
            }

            byte[] raw;
            try
            {
                raw = InflateZlib(idat.ToArray());
            }
            catch (InvalidDataException)
            {
                return false;
            }

            int bytesPerPixel = colorType == 6 ? 4 : 3;
            int stride = width * bytesPerPixel;
            int expectedLength = height * (stride + 1);
            if (raw.Length < expectedLength)
            {
                return false;
            }

            byte[] rgba = new byte[width * height * 4];
            byte[] previous = new byte[stride];
            byte[] current = new byte[stride];
            int rawOffset = 0;
            int dest = 0;
            for (int y = 0; y < height; y++)
            {
                int filter = raw[rawOffset++];
                Buffer.BlockCopy(raw, rawOffset, current, 0, stride);
                rawOffset += stride;
                if (!Unfilter(filter, current, previous, bytesPerPixel))
                {
                    return false;
                }

                if (colorType == 6)
                {
                    Buffer.BlockCopy(current, 0, rgba, dest, stride);
                    dest += stride;
                }
                else
                {
                    for (int x = 0; x < width; x++)
                    {
                        int src = x * 3;
                        rgba[dest++] = current[src];
                        rgba[dest++] = current[src + 1];
                        rgba[dest++] = current[src + 2];
                        rgba[dest++] = 255;
                    }
                }

                Buffer.BlockCopy(current, 0, previous, 0, stride);
            }

            pixels = rgba;
            return true;
        }

        private static byte[] InflateZlib(byte[] zlib)
        {
            if (zlib.Length < 6)
            {
                throw new InvalidDataException("zlib payload is too short.");
            }

            using MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 6, writable: false);
            using DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress);
            using MemoryStream output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        private static bool Unfilter(int filter, byte[] current, byte[] previous, int bytesPerPixel)
        {
            if (filter == 0)
            {
                return true;
            }

            for (int i = 0; i < current.Length; i++)
            {
                byte left = i >= bytesPerPixel ? current[i - bytesPerPixel] : (byte)0;
                byte up = previous[i];
                byte upLeft = i >= bytesPerPixel ? previous[i - bytesPerPixel] : (byte)0;
                int recon;
                switch (filter)
                {
                    case 1:
                        recon = current[i] + left;
                        break;
                    case 2:
                        recon = current[i] + up;
                        break;
                    case 3:
                        recon = current[i] + ((left + up) / 2);
                        break;
                    case 4:
                        recon = current[i] + Paeth(left, up, upLeft);
                        break;
                    default:
                        return false;
                }

                current[i] = (byte)(recon & 0xFF);
            }

            return true;
        }

        private static byte Paeth(byte left, byte up, byte upLeft)
        {
            int p = left + up - upLeft;
            int pa = Math.Abs(p - left);
            int pb = Math.Abs(p - up);
            int pc = Math.Abs(p - upLeft);
            if (pa <= pb && pa <= pc)
            {
                return left;
            }

            return pb <= pc ? up : upLeft;
        }
    }
}
