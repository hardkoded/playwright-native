/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.IO.Compression;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Re-encodes an 8-bit PNG (the only format WebKit <c>Page.snapshotRect</c> emits)
    /// as a baseline JPEG. Avoids adding an image library to the netstandard2.1 surface.
    /// </summary>
    internal static class PngToJpeg
    {
        private static readonly int[] ZigZag =
        {
            0, 1, 8, 16, 9, 2, 3, 10,
            17, 24, 32, 25, 18, 11, 4, 5,
            12, 19, 26, 33, 40, 48, 41, 34,
            27, 20, 13, 6, 7, 14, 21, 28,
            35, 42, 49, 56, 57, 50, 43, 36,
            29, 22, 15, 23, 30, 37, 44, 51,
            58, 59, 52, 45, 38, 31, 39, 46,
            53, 60, 61, 54, 47, 55, 62, 63,
        };

        private static readonly byte[] StdLumQuant =
        {
            16, 11, 10, 16, 24, 40, 51, 61,
            12, 12, 14, 19, 26, 58, 60, 55,
            14, 13, 16, 24, 40, 57, 69, 56,
            14, 17, 22, 29, 51, 87, 80, 62,
            18, 22, 37, 56, 68, 109, 103, 77,
            24, 35, 55, 64, 81, 104, 113, 92,
            49, 64, 78, 87, 103, 121, 120, 101,
            72, 92, 95, 98, 112, 100, 103, 99,
        };

        private static readonly byte[] StdChrQuant =
        {
            17, 18, 24, 47, 99, 99, 99, 99,
            18, 21, 26, 66, 99, 99, 99, 99,
            24, 26, 56, 99, 99, 99, 99, 99,
            47, 66, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
            99, 99, 99, 99, 99, 99, 99, 99,
        };

        /// <summary>
        /// Converts a PNG byte array to a baseline JPEG.
        /// </summary>
        /// <param name="png">PNG bytes from WebKit <c>snapshotRect</c>.</param>
        /// <param name="quality">JPEG quality 0–100. Values outside the range are clamped. Defaults to 80.</param>
        /// <returns>JPEG bytes.</returns>
        internal static byte[] Convert(byte[] png, int? quality)
        {
            if (png == null || png.Length < 8)
            {
                throw new PlaywrightSharpException("PNG screenshot is empty.");
            }

            RgbImage image = DecodePng(png);
            int q = quality ?? 80;
            if (q < 1)
            {
                q = 1;
            }
            else if (q > 100)
            {
                q = 100;
            }

            return EncodeJpeg(image, q);
        }

        private static RgbImage DecodePng(byte[] png)
        {
            if (png[0] != 0x89 || png[1] != 0x50 || png[2] != 0x4E || png[3] != 0x47
                || png[4] != 0x0D || png[5] != 0x0A || png[6] != 0x1A || png[7] != 0x0A)
            {
                throw new PlaywrightSharpException("Screenshot is not a PNG.");
            }

            int width = 0;
            int height = 0;
            int bitDepth = 0;
            int colorType = 0;
            int interlace = 0;
            using MemoryStream idat = new MemoryStream();
            int offset = 8;
            while (offset + 12 <= png.Length)
            {
                int length = ReadInt32Be(png, offset);
                if (length < 0 || offset + 12 + length > png.Length)
                {
                    throw new PlaywrightSharpException("PNG chunk is truncated.");
                }

                string type = System.Text.Encoding.ASCII.GetString(png, offset + 4, 4);
                int dataStart = offset + 8;
                if (type == "IHDR")
                {
                    if (length < 13)
                    {
                        throw new PlaywrightSharpException("PNG IHDR is truncated.");
                    }

                    width = ReadInt32Be(png, dataStart);
                    height = ReadInt32Be(png, dataStart + 4);
                    bitDepth = png[dataStart + 8];
                    colorType = png[dataStart + 9];
                    interlace = png[dataStart + 12];
                }
                else if (type == "IDAT" && length > 0)
                {
                    idat.Write(png, dataStart, length);
                }
                else if (type == "IEND")
                {
                    break;
                }

                offset += 12 + length;
            }

            if (width <= 0 || height <= 0)
            {
                throw new PlaywrightSharpException("PNG IHDR is missing or invalid.");
            }

            if (bitDepth != 8)
            {
                throw new PlaywrightSharpException("Only 8-bit PNG screenshots are supported.");
            }

            if (interlace != 0)
            {
                throw new PlaywrightSharpException("Interlaced PNG screenshots are not supported.");
            }

            int channels = colorType switch
            {
                0 => 1,
                2 => 3,
                4 => 2,
                6 => 4,
                _ => throw new PlaywrightSharpException("Unsupported PNG color type " + colorType + "."),
            };

            byte[] raw = InflateZlib(idat.ToArray());
            byte[] rgb = UnfilterToRgb(raw, width, height, channels);
            return new RgbImage(width, height, rgb);
        }

        private static byte[] InflateZlib(byte[] zlib)
        {
            if (zlib == null || zlib.Length < 6)
            {
                throw new PlaywrightSharpException("PNG IDAT is empty.");
            }

            using MemoryStream input = new MemoryStream(zlib, 2, zlib.Length - 6);
            using DeflateStream deflate = new DeflateStream(input, CompressionMode.Decompress);
            using MemoryStream output = new MemoryStream();
            deflate.CopyTo(output);
            return output.ToArray();
        }

        private static byte[] UnfilterToRgb(byte[] raw, int width, int height, int channels)
        {
            int stride = width * channels;
            int expected = height * (stride + 1);
            if (raw.Length < expected)
            {
                throw new PlaywrightSharpException("PNG pixel data is truncated.");
            }

            byte[] recon = new byte[height * stride];
            for (int y = 0; y < height; y++)
            {
                int filter = raw[y * (stride + 1)];
                int src = (y * (stride + 1)) + 1;
                int dst = y * stride;
                for (int x = 0; x < stride; x++)
                {
                    byte value = raw[src + x];
                    byte left = x >= channels ? recon[dst + x - channels] : (byte)0;
                    byte up = y > 0 ? recon[dst + x - stride] : (byte)0;
                    byte upLeft = y > 0 && x >= channels ? recon[dst + x - stride - channels] : (byte)0;
                    recon[dst + x] = filter switch
                    {
                        0 => value,
                        1 => (byte)(value + left),
                        2 => (byte)(value + up),
                        3 => (byte)(value + ((left + up) / 2)),
                        4 => (byte)(value + Paeth(left, up, upLeft)),
                        _ => throw new PlaywrightSharpException("Unknown PNG filter " + filter + "."),
                    };
                }
            }

            byte[] rgb = new byte[width * height * 3];
            for (int i = 0; i < width * height; i++)
            {
                int s = i * channels;
                int d = i * 3;
                if (channels == 1)
                {
                    rgb[d] = recon[s];
                    rgb[d + 1] = recon[s];
                    rgb[d + 2] = recon[s];
                }
                else if (channels == 2)
                {
                    byte gray = recon[s];
                    byte alpha = recon[s + 1];
                    rgb[d] = BlendWhite(gray, alpha);
                    rgb[d + 1] = rgb[d];
                    rgb[d + 2] = rgb[d];
                }
                else if (channels == 3)
                {
                    rgb[d] = recon[s];
                    rgb[d + 1] = recon[s + 1];
                    rgb[d + 2] = recon[s + 2];
                }
                else
                {
                    byte alpha = recon[s + 3];
                    rgb[d] = BlendWhite(recon[s], alpha);
                    rgb[d + 1] = BlendWhite(recon[s + 1], alpha);
                    rgb[d + 2] = BlendWhite(recon[s + 2], alpha);
                }
            }

            return rgb;
        }

        private static byte BlendWhite(byte channel, byte alpha)
            => (byte)(((channel * alpha) + (255 * (255 - alpha)) + 127) / 255);

        private static byte Paeth(byte a, byte b, byte c)
        {
            int p = a + b - c;
            int pa = Math.Abs(p - a);
            int pb = Math.Abs(p - b);
            int pc = Math.Abs(p - c);
            if (pa <= pb && pa <= pc)
            {
                return a;
            }

            return pb <= pc ? b : c;
        }

        private static int ReadInt32Be(byte[] data, int offset)
            => (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];

        private static byte[] EncodeJpeg(RgbImage image, int quality)
        {
            byte[] lumQuant = ScaleQuant(StdLumQuant, quality);
            byte[] chrQuant = ScaleQuant(StdChrQuant, quality);
            HuffmanTable dcLum = HuffmanTable.DcLuminance();
            HuffmanTable acLum = HuffmanTable.AcLuminance();
            HuffmanTable dcChr = HuffmanTable.DcChrominance();
            HuffmanTable acChr = HuffmanTable.AcChrominance();

            using MemoryStream output = new MemoryStream();
            WriteMarker(output, 0xD8);
            WriteJfif(output);
            WriteDqt(output, 0, lumQuant);
            WriteDqt(output, 1, chrQuant);
            WriteSof(output, image.Width, image.Height);
            WriteDht(output, 0, 0, dcLum);
            WriteDht(output, 0, 1, acLum);
            WriteDht(output, 1, 0, dcChr);
            WriteDht(output, 1, 1, acChr);
            WriteSos(output);

            BitWriter bits = new BitWriter(output);
            int prevY = 0;
            int prevCb = 0;
            int prevCr = 0;
            float[] block = new float[64];
            int[] quantized = new int[64];
            int mcuY;
            for (mcuY = 0; mcuY < image.Height; mcuY += 8)
            {
                int mcuX;
                for (mcuX = 0; mcuX < image.Width; mcuX += 8)
                {
                    FillY(image, mcuX, mcuY, block);
                    prevY = EncodeBlock(bits, block, lumQuant, dcLum, acLum, prevY, quantized);
                    FillCb(image, mcuX, mcuY, block);
                    prevCb = EncodeBlock(bits, block, chrQuant, dcChr, acChr, prevCb, quantized);
                    FillCr(image, mcuX, mcuY, block);
                    prevCr = EncodeBlock(bits, block, chrQuant, dcChr, acChr, prevCr, quantized);
                }
            }

            bits.Flush();
            WriteMarker(output, 0xD9);
            return output.ToArray();
        }

        private static byte[] ScaleQuant(byte[] standard, int quality)
        {
            int scale = quality < 50 ? 5000 / quality : 200 - (quality * 2);
            byte[] table = new byte[64];
            for (int i = 0; i < 64; i++)
            {
                int value = ((standard[i] * scale) + 50) / 100;
                if (value < 1)
                {
                    value = 1;
                }
                else if (value > 255)
                {
                    value = 255;
                }

                table[i] = (byte)value;
            }

            return table;
        }

        private static void FillY(RgbImage image, int left, int top, float[] block)
        {
            for (int y = 0; y < 8; y++)
            {
                int py = Math.Min(top + y, image.Height - 1);
                for (int x = 0; x < 8; x++)
                {
                    int px = Math.Min(left + x, image.Width - 1);
                    int i = ((py * image.Width) + px) * 3;
                    float r = image.Rgb[i];
                    float g = image.Rgb[i + 1];
                    float b = image.Rgb[i + 2];
                    block[(y * 8) + x] = (0.299f * r) + (0.587f * g) + (0.114f * b) - 128f;
                }
            }
        }

        private static void FillCb(RgbImage image, int left, int top, float[] block)
        {
            for (int y = 0; y < 8; y++)
            {
                int py = Math.Min(top + y, image.Height - 1);
                for (int x = 0; x < 8; x++)
                {
                    int px = Math.Min(left + x, image.Width - 1);
                    int i = ((py * image.Width) + px) * 3;
                    float r = image.Rgb[i];
                    float g = image.Rgb[i + 1];
                    float b = image.Rgb[i + 2];
                    block[(y * 8) + x] = (-0.168736f * r) - (0.331264f * g) + (0.5f * b);
                }
            }
        }

        private static void FillCr(RgbImage image, int left, int top, float[] block)
        {
            for (int y = 0; y < 8; y++)
            {
                int py = Math.Min(top + y, image.Height - 1);
                for (int x = 0; x < 8; x++)
                {
                    int px = Math.Min(left + x, image.Width - 1);
                    int i = ((py * image.Width) + px) * 3;
                    float r = image.Rgb[i];
                    float g = image.Rgb[i + 1];
                    float b = image.Rgb[i + 2];
                    block[(y * 8) + x] = (0.5f * r) - (0.418688f * g) - (0.081312f * b);
                }
            }
        }

        private static int EncodeBlock(
            BitWriter bits,
            float[] spatial,
            byte[] quant,
            HuffmanTable dc,
            HuffmanTable ac,
            int prevDc,
            int[] quantized)
        {
            Dct8x8(spatial, quantized, quant);
            int dcCoef = quantized[0];
            WriteCoefficient(bits, dc, dcCoef - prevDc);
            int zeroRun = 0;
            for (int i = 1; i < 64; i++)
            {
                int value = quantized[ZigZag[i]];
                if (value == 0)
                {
                    zeroRun++;
                    continue;
                }

                while (zeroRun >= 16)
                {
                    ac.Write(bits, 0xF0);
                    zeroRun -= 16;
                }

                WriteAc(bits, ac, zeroRun, value);
                zeroRun = 0;
            }

            if (zeroRun > 0)
            {
                ac.Write(bits, 0x00);
            }

            return dcCoef;
        }

        private static void Dct8x8(float[] spatial, int[] quantized, byte[] quant)
        {
            float[] temp = new float[64];
            for (int y = 0; y < 8; y++)
            {
                for (int u = 0; u < 8; u++)
                {
                    float sum = 0;
                    for (int x = 0; x < 8; x++)
                    {
                        sum += spatial[(y * 8) + x] * Cos((2 * x) + 1, u);
                    }

                    temp[(y * 8) + u] = sum * (u == 0 ? 0.35355339f : 0.5f);
                }
            }

            for (int u = 0; u < 8; u++)
            {
                for (int v = 0; v < 8; v++)
                {
                    float sum = 0;
                    for (int y = 0; y < 8; y++)
                    {
                        sum += temp[(y * 8) + u] * Cos((2 * y) + 1, v);
                    }

                    float coeff = sum * (v == 0 ? 0.35355339f : 0.5f);
                    int q = quant[(v * 8) + u];
                    quantized[(v * 8) + u] = (int)Math.Round(coeff / q, MidpointRounding.AwayFromZero);
                }
            }
        }

        private static float Cos(int odd, int freq)
            => (float)Math.Cos((odd * freq) * Math.PI / 16.0);

        private static void WriteCoefficient(BitWriter bits, HuffmanTable table, int value)
        {
            int category = BitCategory(value);
            table.Write(bits, category);
            if (category > 0)
            {
                bits.WriteBits(CoefficientBits(value, category), category);
            }
        }

        private static void WriteAc(BitWriter bits, HuffmanTable table, int zeroRun, int value)
        {
            int category = BitCategory(value);
            table.Write(bits, (zeroRun << 4) | category);
            bits.WriteBits(CoefficientBits(value, category), category);
        }

        private static int BitCategory(int value)
        {
            int abs = value < 0 ? -value : value;
            int category = 0;
            while (abs > 0)
            {
                abs >>= 1;
                category++;
            }

            return category;
        }

        private static int CoefficientBits(int value, int category)
        {
            if (value < 0)
            {
                return value + ((1 << category) - 1);
            }

            return value;
        }

        private static void WriteMarker(Stream output, byte code)
        {
            output.WriteByte(0xFF);
            output.WriteByte(code);
        }

        private static void WriteJfif(Stream output)
        {
            WriteMarker(output, 0xE0);
            WriteUInt16Be(output, 16);
            output.Write(new byte[] { 0x4A, 0x46, 0x49, 0x46, 0x00, 1, 1, 0, 0, 1, 0, 1, 0, 0 }, 0, 14);
        }

        private static void WriteDqt(Stream output, byte destination, byte[] table)
        {
            WriteMarker(output, 0xDB);
            WriteUInt16Be(output, 67);
            output.WriteByte(destination);
            for (int i = 0; i < 64; i++)
            {
                output.WriteByte(table[ZigZag[i]]);
            }
        }

        private static void WriteSof(Stream output, int width, int height)
        {
            WriteMarker(output, 0xC0);
            WriteUInt16Be(output, 17);
            output.WriteByte(8);
            WriteUInt16Be(output, height);
            WriteUInt16Be(output, width);
            output.WriteByte(3);
            output.WriteByte(1);
            output.WriteByte(0x11);
            output.WriteByte(0);
            output.WriteByte(2);
            output.WriteByte(0x11);
            output.WriteByte(1);
            output.WriteByte(3);
            output.WriteByte(0x11);
            output.WriteByte(1);
        }

        private static void WriteDht(Stream output, byte tableClass, byte destination, HuffmanTable table)
        {
            byte[] payload = table.DhtPayload();
            WriteMarker(output, 0xC4);
            WriteUInt16Be(output, 2 + 1 + payload.Length);
            output.WriteByte((byte)((tableClass << 4) | destination));
            output.Write(payload, 0, payload.Length);
        }

        private static void WriteSos(Stream output)
        {
            WriteMarker(output, 0xDA);
            WriteUInt16Be(output, 12);
            output.WriteByte(3);
            output.WriteByte(1);
            output.WriteByte(0x00);
            output.WriteByte(2);
            output.WriteByte(0x11);
            output.WriteByte(3);
            output.WriteByte(0x11);
            output.WriteByte(0);
            output.WriteByte(63);
            output.WriteByte(0);
        }

        private static void WriteUInt16Be(Stream output, int value)
        {
            output.WriteByte((byte)(value >> 8));
            output.WriteByte((byte)value);
        }

        private readonly struct RgbImage
        {
            internal RgbImage(int width, int height, byte[] rgb)
            {
                Width = width;
                Height = height;
                Rgb = rgb;
            }

            internal int Width { get; }

            internal int Height { get; }

            internal byte[] Rgb { get; }
        }

        private sealed class HuffmanTable
        {
            private readonly int[] _codes = new int[256];
            private readonly int[] _lengths = new int[256];
            private readonly byte[] _bits;
            private readonly byte[] _values;

            private HuffmanTable(byte[] bits, byte[] values)
            {
                _bits = bits;
                _values = values;
                int code = 0;
                int index = 0;
                for (int length = 1; length <= 16; length++)
                {
                    int count = bits[length - 1];
                    for (int i = 0; i < count; i++)
                    {
                        byte symbol = values[index++];
                        _codes[symbol] = code;
                        _lengths[symbol] = length;
                        code++;
                    }

                    code <<= 1;
                }
            }

            internal static HuffmanTable DcLuminance()
                => new HuffmanTable(
                    new byte[] { 0, 1, 5, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0, 0, 0 },
                    new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 });

            internal static HuffmanTable AcLuminance()
                => new HuffmanTable(
                    new byte[] { 0, 2, 1, 3, 3, 2, 4, 3, 5, 5, 4, 4, 0, 0, 1, 0x7D },
                    new byte[]
                    {
                        0x01, 0x02, 0x03, 0x00, 0x04, 0x11, 0x05, 0x12, 0x21, 0x31, 0x41, 0x06, 0x13, 0x51, 0x61, 0x07,
                        0x22, 0x71, 0x14, 0x32, 0x81, 0x91, 0xA1, 0x08, 0x23, 0x42, 0xB1, 0xC1, 0x15, 0x52, 0xD1, 0xF0,
                        0x24, 0x33, 0x62, 0x72, 0x82, 0x09, 0x0A, 0x16, 0x17, 0x18, 0x19, 0x1A, 0x25, 0x26, 0x27, 0x28,
                        0x29, 0x2A, 0x34, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48, 0x49,
                        0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68, 0x69,
                        0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x83, 0x84, 0x85, 0x86, 0x87, 0x88, 0x89,
                        0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5, 0xA6, 0xA7,
                        0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3, 0xC4, 0xC5,
                        0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA, 0xE1, 0xE2,
                        0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF1, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
                        0xF9, 0xFA,
                    });

            internal static HuffmanTable DcChrominance()
                => new HuffmanTable(
                    new byte[] { 0, 3, 1, 1, 1, 1, 1, 1, 1, 1, 1, 0, 0, 0, 0, 0 },
                    new byte[] { 0, 1, 2, 3, 4, 5, 6, 7, 8, 9, 10, 11 });

            internal static HuffmanTable AcChrominance()
                => new HuffmanTable(
                    new byte[] { 0, 2, 1, 2, 4, 4, 3, 4, 7, 5, 4, 4, 0, 1, 2, 0x77 },
                    new byte[]
                    {
                        0x00, 0x01, 0x02, 0x03, 0x11, 0x04, 0x05, 0x21, 0x31, 0x06, 0x12, 0x41, 0x51, 0x07, 0x61, 0x71,
                        0x13, 0x22, 0x32, 0x81, 0x08, 0x14, 0x42, 0x91, 0xA1, 0xB1, 0xC1, 0x09, 0x23, 0x33, 0x52, 0xF0,
                        0x15, 0x62, 0x72, 0xD1, 0x0A, 0x16, 0x24, 0x34, 0xE1, 0x25, 0xF1, 0x17, 0x18, 0x19, 0x1A, 0x26,
                        0x27, 0x28, 0x29, 0x2A, 0x35, 0x36, 0x37, 0x38, 0x39, 0x3A, 0x43, 0x44, 0x45, 0x46, 0x47, 0x48,
                        0x49, 0x4A, 0x53, 0x54, 0x55, 0x56, 0x57, 0x58, 0x59, 0x5A, 0x63, 0x64, 0x65, 0x66, 0x67, 0x68,
                        0x69, 0x6A, 0x73, 0x74, 0x75, 0x76, 0x77, 0x78, 0x79, 0x7A, 0x82, 0x83, 0x84, 0x85, 0x86, 0x87,
                        0x88, 0x89, 0x8A, 0x92, 0x93, 0x94, 0x95, 0x96, 0x97, 0x98, 0x99, 0x9A, 0xA2, 0xA3, 0xA4, 0xA5,
                        0xA6, 0xA7, 0xA8, 0xA9, 0xAA, 0xB2, 0xB3, 0xB4, 0xB5, 0xB6, 0xB7, 0xB8, 0xB9, 0xBA, 0xC2, 0xC3,
                        0xC4, 0xC5, 0xC6, 0xC7, 0xC8, 0xC9, 0xCA, 0xD2, 0xD3, 0xD4, 0xD5, 0xD6, 0xD7, 0xD8, 0xD9, 0xDA,
                        0xE2, 0xE3, 0xE4, 0xE5, 0xE6, 0xE7, 0xE8, 0xE9, 0xEA, 0xF2, 0xF3, 0xF4, 0xF5, 0xF6, 0xF7, 0xF8,
                        0xF9, 0xFA,
                    });

            internal void Write(BitWriter bits, int symbol)
            {
                int length = _lengths[symbol];
                if (length == 0)
                {
                    throw new PlaywrightSharpException("JPEG Huffman symbol " + symbol + " is not in the table.");
                }

                bits.WriteBits(_codes[symbol], length);
            }

            internal byte[] DhtPayload()
            {
                byte[] payload = new byte[16 + _values.Length];
                Array.Copy(_bits, 0, payload, 0, 16);
                Array.Copy(_values, 0, payload, 16, _values.Length);
                return payload;
            }
        }

        private sealed class BitWriter
        {
            private readonly Stream _output;
            private int _buffer;
            private int _bits;

            internal BitWriter(Stream output)
            {
                _output = output;
            }

            internal void WriteBits(int value, int length)
            {
                _buffer = (_buffer << length) | (value & ((1 << length) - 1));
                _bits += length;
                while (_bits >= 8)
                {
                    _bits -= 8;
                    int b = (_buffer >> _bits) & 0xFF;
                    _output.WriteByte((byte)b);
                    if (b == 0xFF)
                    {
                        _output.WriteByte(0x00);
                    }
                }
            }

            internal void Flush()
            {
                if (_bits > 0)
                {
                    WriteBits(0x7F, 8 - _bits);
                }
            }
        }
    }
}
