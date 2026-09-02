// Copyright (c) Microsoft Corporation.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
// http://www.apache.org/licenses/LICENSE-2.0
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;
using NUnit.Framework;
using PlaywrightNative;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    [TestFixture]
    public class ArchiveExtractorTests
    {
        private const int UnixSymlinkMode = 0x0000A1FF; // S_IFLNK | 0o777, ready to shift into upper 16 bits
        private const int UnixRegularExecutableMode = 0x000081ED; // S_IFREG | 0o755

        [PlaywrightTest("browsers-path.spec.ts", "Extract restores symlink entries on unix")]
        [Test]
        public void ExtractRestoresSymlinkEntriesOnUnix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("Symlink restoration only applies on Unix.");
            }

            string zipPath = Path.Combine(Path.GetTempPath(), "pwsharp-archive-test-" + System.Guid.NewGuid() + ".zip");
            string destDir = Path.Combine(Path.GetTempPath(), "pwsharp-archive-out-" + System.Guid.NewGuid());

            try
            {
                CreateZipWithSymlink(zipPath, regularName: "libfoo.so.1.0.7", regularBytes: new byte[] { 1, 2, 3 }, symlinkName: "libfoo.so.1", symlinkTarget: "libfoo.so.1.0.7");

                ArchiveExtractor.Extract(zipPath, destDir);

                string linkPath = Path.Combine(destDir, "libfoo.so.1");
                FileInfo info = new(linkPath);
                Assert.That(info.LinkTarget, Is.EqualTo("libfoo.so.1.0.7"), "Entry should be restored as a real symlink pointing at the target.");
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                if (Directory.Exists(destDir))
                {
                    Directory.Delete(destDir, recursive: true);
                }
            }
        }

        [PlaywrightTest("browsers-path.spec.ts", "Extract applies execute bits on unix")]
        [Test]
        public void ExtractAppliesExecuteBitsOnUnix()
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Assert.Ignore("Unix mode bits only apply on Unix.");
            }

            string zipPath = Path.Combine(Path.GetTempPath(), "pwsharp-archive-test-" + System.Guid.NewGuid() + ".zip");
            string destDir = Path.Combine(Path.GetTempPath(), "pwsharp-archive-out-" + System.Guid.NewGuid());

            try
            {
                using (FileStream stream = File.Create(zipPath))
                using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
                {
                    ZipArchiveEntry entry = archive.CreateEntry("run.sh");
                    entry.ExternalAttributes = UnixRegularExecutableMode << 16;
                    using Stream entryStream = entry.Open();
                    byte[] payload = Encoding.UTF8.GetBytes("#!/bin/sh\necho hi\n");
                    entryStream.Write(payload, 0, payload.Length);
                }

                ArchiveExtractor.Extract(zipPath, destDir);

                string scriptPath = Path.Combine(destDir, "run.sh");
                Assert.That(File.Exists(scriptPath), Is.True);

#pragma warning disable CA1416 // guarded above by IsOSPlatform(Windows) Assert.Ignore
                UnixFileMode mode = File.GetUnixFileMode(scriptPath);
#pragma warning restore CA1416
                Assert.That(mode.HasFlag(UnixFileMode.UserExecute), Is.True);
            }
            finally
            {
                if (File.Exists(zipPath))
                {
                    File.Delete(zipPath);
                }

                if (Directory.Exists(destDir))
                {
                    Directory.Delete(destDir, recursive: true);
                }
            }
        }

        private static void CreateZipWithSymlink(string zipPath, string regularName, byte[] regularBytes, string symlinkName, string symlinkTarget)
        {
            using FileStream stream = File.Create(zipPath);
            using ZipArchive archive = new(stream, ZipArchiveMode.Create);

            ZipArchiveEntry regular = archive.CreateEntry(regularName);
            using (Stream regularStream = regular.Open())
            {
                regularStream.Write(regularBytes, 0, regularBytes.Length);
            }

            ZipArchiveEntry symlink = archive.CreateEntry(symlinkName);
            symlink.ExternalAttributes = UnixSymlinkMode << 16;
            using (Stream symlinkStream = symlink.Open())
            {
                byte[] targetBytes = Encoding.UTF8.GetBytes(symlinkTarget);
                symlinkStream.Write(targetBytes, 0, targetBytes.Length);
            }
        }
    }
}
