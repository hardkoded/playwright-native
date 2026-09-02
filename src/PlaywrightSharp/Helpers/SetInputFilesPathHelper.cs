/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PlaywrightSharp.Helpers
{
    /// <summary>
    /// Resolves filesystem paths for <c>setInputFiles</c>: missing-path ENOENT,
    /// directory-vs-file validation, and payloads for directory uploads.
    /// </summary>
    internal static class SetInputFilesPathHelper
    {
        /// <summary>
        /// Official error when more than one directory path is passed.
        /// </summary>
        internal const string MultipleDirectoriesMessage = "Multiple directories are not supported";

        /// <summary>
        /// Official error when directory paths are mixed with file paths.
        /// </summary>
        internal const string MixMessage = "File paths must be all files or a single directory";

        /// <summary>
        /// Official error when a directory is passed to a normal file input.
        /// </summary>
        internal const string FolderOnFileInputMessage = "File input does not support directories, pass individual files instead";

        /// <summary>
        /// Official error when a file is passed to a <c>webkitdirectory</c> input.
        /// </summary>
        internal const string FileOnDirectoryInputMessage = "[webkitdirectory] input requires passing a path to a directory";

        /// <summary>
        /// Page function that reports whether the element is a directory upload input.
        /// </summary>
        internal const string IsDirectoryInputFunction = "el => !!(el.webkitdirectory || el.hasAttribute('webkitdirectory'))";

        /// <summary>
        /// Classifies <paramref name="paths"/> as regular files or a single directory.
        /// </summary>
        /// <param name="paths">Filesystem paths from the public API.</param>
        /// <returns>Resolved absolute file paths and in-page payloads.</returns>
        internal static ResolvedInputFilePaths Resolve(IEnumerable<string> paths)
        {
            List<string> files = new List<string>();
            List<string> dirs = new List<string>();
            if (paths != null)
            {
                foreach (string raw in paths)
                {
                    if (string.IsNullOrEmpty(raw))
                    {
                        throw new FileNotFoundException(FormatEnoent(raw ?? string.Empty), raw);
                    }

                    if (Directory.Exists(raw))
                    {
                        dirs.Add(raw);
                    }
                    else if (File.Exists(raw))
                    {
                        files.Add(raw);
                    }
                    else
                    {
                        throw new FileNotFoundException(FormatEnoent(raw), raw);
                    }
                }
            }

            if (dirs.Count > 1)
            {
                throw new PlaywrightSharpException(MultipleDirectoriesMessage);
            }

            if (dirs.Count == 1 && files.Count > 0)
            {
                throw new PlaywrightSharpException(MixMessage);
            }

            if (dirs.Count == 1)
            {
                return ResolveDirectory(dirs[0]);
            }

            string[] absolute = new string[files.Count];
            PlaywrightFilePayload[] payloads = new PlaywrightFilePayload[files.Count];
            for (int i = 0; i < files.Count; i++)
            {
                string path = files[i];
                absolute[i] = Path.GetFullPath(path);
                payloads[i] = FilePayloadHelper.FromPath(path);
            }

            return new ResolvedInputFilePaths(isDirectory: false, absolute, payloads);
        }

        /// <summary>
        /// Follows a <c>&lt;label&gt;</c> to its associated file input.
        /// </summary>
        /// <param name="element">The queried element (may be a label).</param>
        /// <returns>The file input to assign.</returns>
        internal static async Task<IElementHandle> FollowLabelControlAsync(IElementHandle element)
        {
            if (element == null)
            {
                return null;
            }

            IJSHandle retargeted = await element.EvaluateHandleAsync(ElementStateScript.RetargetFollowLabelFunction).ConfigureAwait(false);
            IElementHandle asElement = retargeted?.AsElement();
            return asElement ?? element;
        }

        /// <summary>
        /// Throws when the resolved paths do not match the input's directory mode.
        /// </summary>
        /// <param name="element">The file input.</param>
        /// <param name="resolved">Resolved paths.</param>
        /// <returns>A task that completes after the element is inspected.</returns>
        internal static async Task ValidateAgainstInputAsync(IElementHandle element, ResolvedInputFilePaths resolved)
        {
            if (element == null)
            {
                throw new PlaywrightSharpException("Element is not an <input type=\"file\">");
            }

            bool isDirectoryInput = await element.EvaluateAsync<bool>(IsDirectoryInputFunction).ConfigureAwait(false);
            if (resolved.IsDirectory && !isDirectoryInput)
            {
                throw new PlaywrightSharpException(FolderOnFileInputMessage);
            }

            if (!resolved.IsDirectory && isDirectoryInput)
            {
                throw new PlaywrightSharpException(FileOnDirectoryInputMessage);
            }
        }

        /// <summary>
        /// Formats the official Node <c>ENOENT</c> <c>fs.stat</c> message.
        /// </summary>
        /// <param name="path">The missing path, as passed by the caller.</param>
        /// <returns>The exception message.</returns>
        internal static string FormatEnoent(string path)
            => "ENOENT: no such file or directory, stat '" + path + "'";

        private static ResolvedInputFilePaths ResolveDirectory(string dir)
        {
            string fullDir = Path.GetFullPath(dir).TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
            string dirName = Path.GetFileName(fullDir);
            string[] found = Directory.GetFiles(fullDir, "*", SearchOption.AllDirectories);
            Array.Sort(found, StringComparer.Ordinal);
            string[] absolute = new string[found.Length];
            PlaywrightFilePayload[] payloads = new PlaywrightFilePayload[found.Length];
            for (int i = 0; i < found.Length; i++)
            {
                string file = found[i];
                string fullFile = Path.GetFullPath(file);
                absolute[i] = fullFile;
                string relative = Path.GetRelativePath(fullDir, fullFile).Replace('\\', '/');
                PlaywrightFilePayload payload = FilePayloadHelper.FromPath(file);
                payload.WebkitRelativePath = dirName + "/" + relative;
                payloads[i] = payload;
            }

            return new ResolvedInputFilePaths(isDirectory: true, absolute, payloads);
        }
    }
}
