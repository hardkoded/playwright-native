/*
 * MIT License
 *
 * Copyright (c) 2020 Darío Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 *
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
 */
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace PlaywrightNative.Transport
{
    /// <summary>
    /// Starts a process on Windows so the child CRT sees inherited fds 3 (read) and 4
    /// (write). Matches Node/libuv: <c>STARTUPINFOEX</c> +
    /// <c>PROC_THREAD_ATTRIBUTE_HANDLE_LIST</c> plus the MSVC <c>lpReserved2</c> stdio
    /// buffer that <c>_get_osfhandle(3/4)</c> consumes (Playwright WebKit
    /// <c>RemoteInspectorPipe</c>).
    /// </summary>
    [SupportedOSPlatform("windows")]
    internal static class WindowsFd34ProcessLauncher
    {
        private const int StartfUsestdhandles = 0x00000100;
        private const int StartfUseshowwindow = 0x00000001;
        private const int SwHide = 0;
        private const int CreateUnicodeEnvironment = 0x00000400;
        private const int ExtendedStartupinfoPresent = 0x00080000;
        private const int CreateNoWindow = 0x08000000;
        private const int ProcThreadAttributeHandleList = 0x00020002;
        private const int HandleFlagInherit = 0x00000001;
        private const uint GenericRead = 0x80000000;
        private const uint GenericWrite = 0x40000000;
        private const uint FileShareRead = 0x00000001;
        private const uint FileShareWrite = 0x00000002;
        private const uint OpenExisting = 3;

        /// <summary>
        /// Launches <paramref name="process"/> with fds 3/4 inherited from
        /// <paramref name="pipes"/> and binds the resulting OS process onto the existing
        /// <see cref="Process"/> instance so <see cref="Process.BeginErrorReadLine"/> and
        /// <see cref="Process.Exited"/> keep working.
        /// </summary>
        /// <param name="process">The preconfigured <see cref="Process"/> (not yet started).</param>
        /// <param name="pipes">Client handles to expose as CRT fds 3 and 4.</param>
        internal static void Start(Process process, InheritablePipes pipes)
        {
            if (process == null)
            {
                throw new ArgumentNullException(nameof(process));
            }

            if (pipes == null)
            {
                throw new ArgumentNullException(nameof(pipes));
            }

            ProcessStartInfo startInfo = process.StartInfo;
            IntPtr stdin = NativeMethods.InvalidHandleValue;
            IntPtr stdoutChild = NativeMethods.InvalidHandleValue;
            IntPtr stdoutParent = IntPtr.Zero;
            IntPtr stderrChild = NativeMethods.InvalidHandleValue;
            IntPtr stderrParent = IntPtr.Zero;
            IntPtr attrList = IntPtr.Zero;
            IntPtr environmentBlock = IntPtr.Zero;
            GCHandle crtPin = default;
            GCHandle handlePin = default;
            bool created = false;
            bool attrInitialized = false;
            NativeMethods.ProcessInformation processInfo = default;

            try
            {
                stdin = OpenNul(GenericRead);
                if (startInfo.RedirectStandardOutput)
                {
                    CreateInheritablePipe(out stdoutParent, out stdoutChild, parentReads: true);
                }
                else
                {
                    stdoutChild = OpenNul(GenericWrite);
                }

                if (startInfo.RedirectStandardError)
                {
                    CreateInheritablePipe(out stderrParent, out stderrChild, parentReads: true);
                }
                else
                {
                    stderrChild = OpenNul(GenericWrite);
                }

                IntPtr fd3 = pipes.ChildReads.ClientSafePipeHandle.DangerousGetHandle();
                IntPtr fd4 = pipes.ChildWrites.ClientSafePipeHandle.DangerousGetHandle();
                EnsureInheritable(fd3);
                EnsureInheritable(fd4);

                IntPtr[] childHandles =
                {
                    stdin,
                    stdoutChild,
                    stderrChild,
                    fd3,
                    fd4,
                };
                byte[] flags =
                {
                    (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FDev),
                    startInfo.RedirectStandardOutput
                        ? (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe)
                        : (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FDev),
                    startInfo.RedirectStandardError
                        ? (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe)
                        : (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FDev),
                    (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe),
                    (byte)(WindowsCrtStdioBuffer.FOpen | WindowsCrtStdioBuffer.FPipe),
                };
                byte[] crtBuffer = WindowsCrtStdioBuffer.Create(childHandles, flags);
                crtPin = GCHandle.Alloc(crtBuffer, GCHandleType.Pinned);

                IntPtr size = IntPtr.Zero;
                NativeMethods.InitializeProcThreadAttributeList(IntPtr.Zero, 1, 0, ref size);
                attrList = Marshal.AllocHGlobal(size);
                if (!NativeMethods.InitializeProcThreadAttributeList(attrList, 1, 0, ref size))
                {
                    throw NewWin32Exception("InitializeProcThreadAttributeList");
                }

                attrInitialized = true;

                handlePin = GCHandle.Alloc(childHandles, GCHandleType.Pinned);
                if (!NativeMethods.UpdateProcThreadAttribute(
                        attrList,
                        0,
                        (IntPtr)ProcThreadAttributeHandleList,
                        handlePin.AddrOfPinnedObject(),
                        (IntPtr)(childHandles.Length * IntPtr.Size),
                        IntPtr.Zero,
                        IntPtr.Zero))
                {
                    throw NewWin32Exception("UpdateProcThreadAttribute");
                }

                NativeMethods.StartupInfoExW startup = default;
                startup.StartupInfo.Cb = Marshal.SizeOf<NativeMethods.StartupInfoExW>();
                startup.StartupInfo.DwFlags = StartfUsestdhandles | StartfUseshowwindow;
                startup.StartupInfo.WShowWindow = SwHide;
                startup.StartupInfo.HStdInput = stdin;
                startup.StartupInfo.HStdOutput = stdoutChild;
                startup.StartupInfo.HStdError = stderrChild;
                startup.StartupInfo.CbReserved2 = (short)crtBuffer.Length;
                startup.StartupInfo.LpReserved2 = crtPin.AddrOfPinnedObject();
                startup.LpAttributeList = attrList;

                environmentBlock = BuildEnvironmentBlock(startInfo.Environment);
                string commandLine = BuildCommandLine(startInfo);
                char[] commandLineBuffer = (commandLine + "\0").ToCharArray();
                string applicationName = Path.IsPathRooted(startInfo.FileName) ? startInfo.FileName : null;
                string workingDirectory = string.IsNullOrEmpty(startInfo.WorkingDirectory)
                    ? null
                    : startInfo.WorkingDirectory;
                int flagsCreate = CreateUnicodeEnvironment | ExtendedStartupinfoPresent | CreateNoWindow;

                if (!NativeMethods.CreateProcessW(
                        applicationName,
                        commandLineBuffer,
                        IntPtr.Zero,
                        IntPtr.Zero,
                        true,
                        flagsCreate,
                        environmentBlock,
                        workingDirectory,
                        ref startup,
                        out processInfo))
                {
                    throw NewWin32Exception("CreateProcessW");
                }

                created = true;
                NativeMethods.CloseHandle(processInfo.HThread);
                processInfo.HThread = IntPtr.Zero;

                BindProcess(process, processInfo.HProcess, processInfo.DwProcessId, stdoutParent, stderrParent, startInfo);
                processInfo.HProcess = IntPtr.Zero;
                stdoutParent = IntPtr.Zero;
                stderrParent = IntPtr.Zero;
            }
            catch
            {
                if (created && processInfo.HProcess != IntPtr.Zero)
                {
                    NativeMethods.TerminateProcess(processInfo.HProcess, 1);
                }

                throw;
            }
            finally
            {
                if (handlePin.IsAllocated)
                {
                    handlePin.Free();
                }

                if (crtPin.IsAllocated)
                {
                    crtPin.Free();
                }

                if (attrList != IntPtr.Zero)
                {
                    if (attrInitialized)
                    {
                        NativeMethods.DeleteProcThreadAttributeList(attrList);
                    }

                    Marshal.FreeHGlobal(attrList);
                }

                if (environmentBlock != IntPtr.Zero)
                {
                    Marshal.FreeHGlobal(environmentBlock);
                }

                CloseIfValid(stdin);
                CloseIfValid(stdoutChild);
                CloseIfValid(stderrChild);
                if (processInfo.HThread != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(processInfo.HThread);
                }

                if (processInfo.HProcess != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(processInfo.HProcess);
                }

                if (stdoutParent != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(stdoutParent);
                }

                if (stderrParent != IntPtr.Zero)
                {
                    NativeMethods.CloseHandle(stderrParent);
                }
            }
        }

        private static void BindProcess(
            Process process,
            IntPtr processHandle,
            int processId,
            IntPtr stdoutParent,
            IntPtr stderrParent,
            ProcessStartInfo startInfo)
        {
            MethodInfo setProcessHandle = typeof(Process).GetMethod(
                "SetProcessHandle",
                BindingFlags.Instance | BindingFlags.NonPublic);
            MethodInfo setProcessId = typeof(Process).GetMethod(
                "SetProcessId",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo standardErrorField = typeof(Process).GetField(
                "_standardError",
                BindingFlags.Instance | BindingFlags.NonPublic);
            FieldInfo standardOutputField = typeof(Process).GetField(
                "_standardOutput",
                BindingFlags.Instance | BindingFlags.NonPublic);

            if (setProcessHandle == null || setProcessId == null)
            {
                throw new PlaywrightNativeException(
                    "Unable to attach the Windows fd-3/4 process to System.Diagnostics.Process; .NET internals changed.");
            }

            SafeProcessHandle safeHandle = new SafeProcessHandle(processHandle, ownsHandle: true);
            setProcessHandle.Invoke(process, new object[] { safeHandle });
            setProcessId.Invoke(process, new object[] { processId });

            if (startInfo.RedirectStandardError && stderrParent != IntPtr.Zero)
            {
                if (standardErrorField == null)
                {
                    throw new PlaywrightNativeException(
                        "Unable to attach redirected stderr for the Windows fd-3/4 process; .NET internals changed.");
                }

                standardErrorField.SetValue(process, CreateReader(stderrParent));
            }

            if (startInfo.RedirectStandardOutput && stdoutParent != IntPtr.Zero)
            {
                if (standardOutputField == null)
                {
                    throw new PlaywrightNativeException(
                        "Unable to attach redirected stdout for the Windows fd-3/4 process; .NET internals changed.");
                }

                standardOutputField.SetValue(process, CreateReader(stdoutParent));
            }
        }

        private static StreamReader CreateReader(IntPtr handle)
        {
            FileStream stream = new FileStream(new SafeFileHandle(handle, ownsHandle: true), FileAccess.Read);
            return new StreamReader(stream, Encoding.UTF8);
        }

        private static string BuildCommandLine(ProcessStartInfo startInfo)
        {
            List<string> tokens = new List<string> { QuoteArgument(startInfo.FileName) };
            if (startInfo.ArgumentList.Count > 0)
            {
                foreach (string arg in startInfo.ArgumentList)
                {
                    tokens.Add(QuoteArgument(arg));
                }
            }
            else if (!string.IsNullOrEmpty(startInfo.Arguments))
            {
                tokens.Add(startInfo.Arguments);
            }

            return string.Join(" ", tokens);
        }

        private static string QuoteArgument(string arg)
        {
            if (string.IsNullOrEmpty(arg))
            {
                return "\"\"";
            }

            bool needsQuotes = false;
            for (int i = 0; i < arg.Length; i++)
            {
                char c = arg[i];
                if (c == ' ' || c == '\t' || c == '"')
                {
                    needsQuotes = true;
                    break;
                }
            }

            if (!needsQuotes)
            {
                return arg;
            }

            return "\"" + arg.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal) + "\"";
        }

        private static IntPtr BuildEnvironmentBlock(IDictionary<string, string> environment)
        {
            StringBuilder builder = new StringBuilder();
            foreach (KeyValuePair<string, string> pair in environment)
            {
                if (string.IsNullOrEmpty(pair.Key))
                {
                    continue;
                }

                builder.Append(pair.Key);
                builder.Append('=');
                builder.Append(pair.Value ?? string.Empty);
                builder.Append('\0');
            }

            builder.Append('\0');
            byte[] bytes = Encoding.Unicode.GetBytes(builder.ToString());
            IntPtr block = Marshal.AllocHGlobal(bytes.Length);
            Marshal.Copy(bytes, 0, block, bytes.Length);
            return block;
        }

        private static void CreateInheritablePipe(out IntPtr parent, out IntPtr child, bool parentReads)
        {
            NativeMethods.SecurityAttributes security = new NativeMethods.SecurityAttributes
            {
                NLength = Marshal.SizeOf<NativeMethods.SecurityAttributes>(),
                BInheritHandle = 1,
            };

            if (!NativeMethods.CreatePipe(out IntPtr read, out IntPtr write, ref security, 0))
            {
                throw NewWin32Exception("CreatePipe");
            }

            if (parentReads)
            {
                parent = read;
                child = write;
                EnsureNotInheritable(parent);
            }
            else
            {
                parent = write;
                child = read;
                EnsureNotInheritable(parent);
            }
        }

        private static IntPtr OpenNul(uint access)
        {
            NativeMethods.SecurityAttributes security = new NativeMethods.SecurityAttributes
            {
                NLength = Marshal.SizeOf<NativeMethods.SecurityAttributes>(),
                BInheritHandle = 1,
            };

            IntPtr handle = NativeMethods.CreateFileW(
                "NUL",
                access,
                FileShareRead | FileShareWrite,
                ref security,
                OpenExisting,
                0,
                IntPtr.Zero);
            if (handle == NativeMethods.InvalidHandleValue)
            {
                throw NewWin32Exception("CreateFileW(NUL)");
            }

            return handle;
        }

        private static void EnsureInheritable(IntPtr handle)
        {
            if (!NativeMethods.SetHandleInformation(handle, HandleFlagInherit, HandleFlagInherit))
            {
                throw NewWin32Exception("SetHandleInformation(inherit)");
            }
        }

        private static void EnsureNotInheritable(IntPtr handle)
        {
            if (!NativeMethods.SetHandleInformation(handle, HandleFlagInherit, 0))
            {
                throw NewWin32Exception("SetHandleInformation(no inherit)");
            }
        }

        private static void CloseIfValid(IntPtr handle)
        {
            if (handle != IntPtr.Zero && handle != NativeMethods.InvalidHandleValue)
            {
                NativeMethods.CloseHandle(handle);
            }
        }

        private static PlaywrightNativeException NewWin32Exception(string api)
        {
            int error = Marshal.GetLastWin32Error();
            return new PlaywrightNativeException(
                $"Windows fd-3/4 launch failed at {api}: {new Win32Exception(error).Message} ({error}).",
                new Win32Exception(error));
        }

        private static class NativeMethods
        {
            internal static readonly IntPtr InvalidHandleValue = new IntPtr(-1);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CreatePipe(
                out IntPtr hReadPipe,
                out IntPtr hWritePipe,
                ref SecurityAttributes lpPipeAttributes,
                int nSize);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern IntPtr CreateFileW(
                string lpFileName,
                uint dwDesiredAccess,
                uint dwShareMode,
                ref SecurityAttributes lpSecurityAttributes,
                uint dwCreationDisposition,
                uint dwFlagsAndAttributes,
                IntPtr hTemplateFile);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool SetHandleInformation(IntPtr hObject, int dwMask, int dwFlags);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CloseHandle(IntPtr hObject);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool TerminateProcess(IntPtr hProcess, uint uExitCode);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool InitializeProcThreadAttributeList(
                IntPtr lpAttributeList,
                int dwAttributeCount,
                int dwFlags,
                ref IntPtr lpSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool UpdateProcThreadAttribute(
                IntPtr lpAttributeList,
                uint dwFlags,
                IntPtr attribute,
                IntPtr lpValue,
                IntPtr cbSize,
                IntPtr lpPreviousValue,
                IntPtr lpReturnSize);

            [DllImport("kernel32.dll", SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            internal static extern void DeleteProcThreadAttributeList(IntPtr lpAttributeList);

            [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
            [DefaultDllImportSearchPaths(DllImportSearchPath.System32)]
            [return: MarshalAs(UnmanagedType.Bool)]
            internal static extern bool CreateProcessW(
                string lpApplicationName,
                char[] lpCommandLine,
                IntPtr lpProcessAttributes,
                IntPtr lpThreadAttributes,
                [MarshalAs(UnmanagedType.Bool)] bool bInheritHandles,
                int dwCreationFlags,
                IntPtr lpEnvironment,
                string lpCurrentDirectory,
                ref StartupInfoExW lpStartupInfo,
                out ProcessInformation lpProcessInformation);

            [StructLayout(LayoutKind.Sequential)]
            internal struct SecurityAttributes
            {
                internal int NLength;
                internal IntPtr LpSecurityDescriptor;
                internal int BInheritHandle;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct StartupInfoW
            {
                internal int Cb;
                internal IntPtr LpReserved;
                internal IntPtr LpDesktop;
                internal IntPtr LpTitle;
                internal int DwX;
                internal int DwY;
                internal int DwXSize;
                internal int DwYSize;
                internal int DwXCountChars;
                internal int DwYCountChars;
                internal int DwFillAttribute;
                internal int DwFlags;
                internal short WShowWindow;
                internal short CbReserved2;
                internal IntPtr LpReserved2;
                internal IntPtr HStdInput;
                internal IntPtr HStdOutput;
                internal IntPtr HStdError;
            }

            [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
            internal struct StartupInfoExW
            {
                internal StartupInfoW StartupInfo;
                internal IntPtr LpAttributeList;
            }

            [StructLayout(LayoutKind.Sequential)]
            internal struct ProcessInformation
            {
                internal IntPtr HProcess;
                internal IntPtr HThread;
                internal int DwProcessId;
                internal int DwThreadId;
            }
        }
    }
}
