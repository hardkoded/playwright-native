/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Text;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// Official <c>utils.isLosslessWebp</c> / <c>utils.decodeWebp</c>.
    /// </summary>
    internal static class OfficialWebp
    {
        internal static bool IsLossless(byte[] data)
        {
            if (data == null || data.Length < 16)
            {
                return false;
            }

            if (Encoding.ASCII.GetString(data, 0, 4) != "RIFF"
                || Encoding.ASCII.GetString(data, 8, 4) != "WEBP")
            {
                return false;
            }

            return IndexOf(data, Encoding.ASCII.GetBytes("VP8L")) >= 0;
        }

        internal static (int Width, int Height, byte[] Data) Decode(byte[] data)
        {
            using Image<Rgba32> image = Image.Load<Rgba32>(data);
            byte[] pixels = new byte[image.Width * image.Height * 4];
            int i = 0;
            for (int y = 0; y < image.Height; y++)
            {
                for (int x = 0; x < image.Width; x++)
                {
                    Rgba32 pixel = image[x, y];
                    pixels[i++] = pixel.R;
                    pixels[i++] = pixel.G;
                    pixels[i++] = pixel.B;
                    pixels[i++] = pixel.A;
                }
            }

            return (image.Width, image.Height, pixels);
        }

        internal static int[] Pixel(byte[] data, int width, int x, int y)
        {
            int offset = ((y * width) + x) * 4;
            return new int[] { data[offset], data[offset + 1], data[offset + 2], data[offset + 3] };
        }

        private static int IndexOf(byte[] haystack, byte[] needle)
        {
            for (int i = 0; i + needle.Length <= haystack.Length; i++)
            {
                bool match = true;
                for (int j = 0; j < needle.Length; j++)
                {
                    if (haystack[i + j] != needle[j])
                    {
                        match = false;
                        break;
                    }
                }

                if (match)
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
