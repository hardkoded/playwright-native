/*
 * Copyright (c) 2020 Darío Kondratiuk
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

namespace PlaywrightNative.Transport
{
    /// <summary>
    /// MSVC CRT startup info passed through <c>STARTUPINFO.lpReserved2</c>. Playwright's
    /// WebKit embedder maps fd 3/4 with <c>_get_osfhandle</c>; that only works when the
    /// parent fills this buffer (same layout as libuv <c>uv__stdio_create</c>).
    /// </summary>
    internal static class WindowsCrtStdioBuffer
    {
        /// <summary>CRT <c>FOPEN</c> — descriptor is open.</summary>
        internal const byte FOpen = 0x01;

        /// <summary>CRT <c>FPIPE</c> — descriptor is a pipe.</summary>
        internal const byte FPipe = 0x08;

        /// <summary>CRT <c>FDEV</c> — descriptor is a character device (NUL).</summary>
        internal const byte FDev = 0x40;

        /// <summary>
        /// Builds the packed CRT stdio buffer:
        /// <c>int count</c>, <c>byte flags[count]</c>, <c>HANDLE handles[count]</c>.
        /// Handles are packed immediately after the flags (no alignment padding).
        /// </summary>
        /// <param name="handles">OS handles for fd 0..N-1.</param>
        /// <param name="flags">CRT <c>_osfile</c> flags for each fd.</param>
        /// <returns>The buffer to pin as <c>lpReserved2</c>.</returns>
        internal static byte[] Create(IntPtr[] handles, byte[] flags)
        {
            if (handles == null)
            {
                throw new ArgumentNullException(nameof(handles));
            }

            if (flags == null)
            {
                throw new ArgumentNullException(nameof(flags));
            }

            if (handles.Length != flags.Length)
            {
                throw new ArgumentException("Handle and flag counts must match.");
            }

            if (handles.Length == 0 || handles.Length > 255)
            {
                throw new ArgumentOutOfRangeException(nameof(handles), "CRT stdio count must be between 1 and 255.");
            }

            int count = handles.Length;
            int handleOffset = sizeof(int) + count;
            byte[] buffer = new byte[handleOffset + (IntPtr.Size * count)];
            WriteInt32(buffer, 0, count);
            for (int i = 0; i < count; i++)
            {
                buffer[sizeof(int) + i] = flags[i];
            }

            for (int i = 0; i < count; i++)
            {
                WriteHandle(buffer, handleOffset + (IntPtr.Size * i), handles[i]);
            }

            return buffer;
        }

        private static void WriteInt32(byte[] buffer, int offset, int value)
        {
            buffer[offset] = (byte)value;
            buffer[offset + 1] = (byte)(value >> 8);
            buffer[offset + 2] = (byte)(value >> 16);
            buffer[offset + 3] = (byte)(value >> 24);
        }

        private static void WriteHandle(byte[] buffer, int offset, IntPtr handle)
        {
            long value = handle.ToInt64();
            for (int i = 0; i < IntPtr.Size; i++)
            {
                buffer[offset + i] = (byte)(value >> (8 * i));
            }
        }
    }
}
