// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;

namespace PlaywrightNative
{
    /// <summary>
    /// Extracts a downloaded browser zip into its installation directory and
    /// applies execute bits on Unix systems.
    /// </summary>
    internal static class ArchiveExtractor
    {
        /// <summary>
        /// Extracts <paramref name="zipPath"/> into <paramref name="destinationDir"/>.
        /// Creates the destination directory if needed. Applies execute bits on Unix.
        /// </summary>
        /// <param name="zipPath">Path to the zip archive on disk.</param>
        /// <param name="destinationDir">Directory to extract into.</param>
        /// <returns><c>true</c> when permission fix-up ran (Unix); <c>false</c> on Windows.</returns>
        internal static bool Extract(string zipPath, string destinationDir)
        {
            Directory.CreateDirectory(destinationDir);

            ZipFile.ExtractToDirectory(zipPath, destinationDir, overwriteFiles: true);

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                return false;
            }

#if NET6_0_OR_GREATER
#pragma warning disable CA1416 // already guarded by IsOSPlatform(Windows) check above
            ApplyUnixAttributes(zipPath, destinationDir);
#pragma warning restore CA1416
#endif
            return true;
        }

#if NET6_0_OR_GREATER
        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void ApplyUnixAttributes(string zipPath, string destinationDir)
        {
            // Upper 16 bits of ExternalAttributes hold the Unix file mode for entries
            // produced by Unix zip implementations.
            const int UnixModeShift = 16;
            const int FileTypeMask = 0xF000;
            const int SymlinkType = 0xA000; // S_IFLNK
            const int ExecuteAnyMask = 0x49; // 0o111

            using ZipArchive archive = ZipFile.OpenRead(zipPath);

            foreach (ZipArchiveEntry entry in archive.Entries)
            {
                if (string.IsNullOrEmpty(entry.Name))
                {
                    continue; // directory entry
                }

                int unixMode = (int)(entry.ExternalAttributes >> UnixModeShift);
                string fullPath = Path.Combine(destinationDir, entry.FullName);

                if ((unixMode & FileTypeMask) == SymlinkType)
                {
                    RestoreSymlink(entry, fullPath);
                    continue;
                }

                if ((unixMode & ExecuteAnyMask) == 0)
                {
                    continue;
                }

                if (!File.Exists(fullPath))
                {
                    continue;
                }

                UnixFileMode current = File.GetUnixFileMode(fullPath);
                UnixFileMode withExecute = current
                    | UnixFileMode.UserExecute
                    | UnixFileMode.GroupExecute
                    | UnixFileMode.OtherExecute;
                if (withExecute != current)
                {
                    File.SetUnixFileMode(fullPath, withExecute);
                }
            }
        }

        [SupportedOSPlatform("linux")]
        [SupportedOSPlatform("macos")]
        private static void RestoreSymlink(ZipArchiveEntry entry, string linkPath)
        {
            // ZipFile.ExtractToDirectory does not preserve symlinks: it writes the
            // entry payload (the link target text, e.g. "libWPEWebKit-2.0.so.1.20.0")
            // as a regular ~30-byte file. The dynamic loader then opens that file and
            // reports "file too short". Read the payload, delete the placeholder, and
            // recreate the entry as a real symlink.
            string target;
            using (Stream stream = entry.Open())
            using (StreamReader reader = new(stream, Encoding.UTF8))
            {
                target = reader.ReadToEnd();
            }

            target = target.TrimEnd('\0', '\n', '\r');
            if (string.IsNullOrEmpty(target))
            {
                return;
            }

            if (File.Exists(linkPath))
            {
                File.Delete(linkPath);
            }

            File.CreateSymbolicLink(linkPath, target);
        }
#endif
    }
}
