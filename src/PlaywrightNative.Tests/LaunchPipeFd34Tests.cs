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
using System.IO.Pipes;
using System.Runtime.InteropServices;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.Transport;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Construction-time coverage for <see cref="TransportMode.PipeFd34"/>, including the
    /// Windows STARTUPINFOEX path that must not throw at manager construction.
    /// </summary>
    [TestFixture]
    public class LaunchPipeFd34Tests
    {
        [PlaywrightTest("browsertype-launch.spec.ts", "PipeFd34 constructs on the current OS")]
        [Test]
        public void PipeFd34ShouldConstructOnCurrentOs()
        {
            using AnonymousPipeServerStream reads = new(PipeDirection.Out, HandleInheritability.Inheritable);
            using AnonymousPipeServerStream writes = new(PipeDirection.In, HandleInheritability.Inheritable);
            string exe = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/true";
            using BrowserProcessManager manager = new(
                exe,
                Array.Empty<string>(),
                TransportMode.PipeFd34,
                inheritablePipes: new InheritablePipes(reads, writes));
            Assert.That(manager.Process, Is.Not.Null);
            Assert.That(manager.Process.StartInfo.FileName, Is.Not.Empty);
        }

        [PlaywrightTest("browsertype-launch.spec.ts", "CRT stdio buffer packs count flags and handles")]
        [Test]
        public void CrtStdioBufferShouldPackCountFlagsAndHandles()
        {
            IntPtr[] handles =
            {
                new IntPtr(1),
                new IntPtr(2),
                new IntPtr(3),
                new IntPtr(4),
                new IntPtr(5),
            };
            byte[] flags =
            {
                (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FDev),
                (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe),
                (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe),
                (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe),
                (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe),
            };

            byte[] buffer = WindowsCrtStdioBuffer.Create(handles, flags);
            Assert.That(BitConverter.ToInt32(buffer, 0), Is.EqualTo(5));
            Assert.That(buffer[4], Is.EqualTo(flags[0]));
            Assert.That(buffer[8], Is.EqualTo(flags[4]));

            int handleOffset = sizeof(int) + flags.Length;
            long fd3 = 0;
            for (int i = 0; i < IntPtr.Size; i++)
            {
                fd3 |= (long)buffer[handleOffset + (IntPtr.Size * 3) + i] << (8 * i);
            }

            Assert.That(fd3, Is.EqualTo(4));
        }
    }
}
