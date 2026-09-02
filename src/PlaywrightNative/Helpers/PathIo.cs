/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.IO;
using System.Text;

namespace PlaywrightNative.Helpers
{
    /// <summary>
    /// Reads and writes local files for path overloads of page/context APIs.
    /// </summary>
    internal static class PathIo
    {
        /// <summary>
        /// Reads a UTF-8 text file.
        /// </summary>
        /// <param name="path">A filesystem path.</param>
        /// <returns>The file contents.</returns>
        internal static string ReadText(string path)
        {
            EnsurePath(path);
            return File.ReadAllText(path);
        }

        /// <summary>
        /// Reads a file as bytes.
        /// </summary>
        /// <param name="path">A filesystem path.</param>
        /// <returns>The file contents.</returns>
        internal static byte[] ReadBytes(string path)
        {
            EnsurePath(path);
            return File.ReadAllBytes(path);
        }

        /// <summary>
        /// Writes <paramref name="bytes"/> to <paramref name="path"/>, creating parent
        /// directories when needed.
        /// </summary>
        /// <param name="path">A filesystem path.</param>
        /// <param name="bytes">The bytes to write. Null is treated as empty.</param>
        internal static void WriteBytes(string path, byte[] bytes)
        {
            EnsurePath(path);
            string directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.WriteAllBytes(path, bytes ?? Array.Empty<byte>());
        }

        /// <summary>
        /// Writes UTF-8 text to <paramref name="path"/>, creating parent directories when needed.
        /// </summary>
        /// <param name="path">A filesystem path.</param>
        /// <param name="text">The text to write. Null is treated as empty.</param>
        internal static void WriteText(string path, string text)
            => WriteBytes(path, Encoding.UTF8.GetBytes(text ?? string.Empty));

        private static void EnsurePath(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                throw new ArgumentException("Path must not be null or empty.", nameof(path));
            }
        }
    }
}
