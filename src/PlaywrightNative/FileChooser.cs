using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Playwright;
using PlaywrightNative.Helpers;

namespace PlaywrightNative
{
    /// <summary>
    /// FileChooser arguments.
    /// </summary>
    public partial class FileChooser : IFileChooser
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="FileChooser"/> class.
        /// </summary>
        /// <param name="page">The page this file chooser belongs to.</param>
        /// <param name="element">The input element.</param>
        /// <param name="multiple">The multiple option.</param>
        public FileChooser(IPage page, IElementHandle element, bool multiple)
        {
            Page = page;
            Element = element;
            IsMultiple = multiple;
        }

        /// <inheritdoc />
        public IPage Page { get; set; }

        /// <inheritdoc />
        public IElementHandle Element { get; set; }

        /// <inheritdoc />
        public bool IsMultiple { get; set; }

        /// <inheritdoc />
        public Task SetFilesAsync(string files, bool? noWaitAfter, float? timeout)
            => SetFilesAsync(string.IsNullOrEmpty(files) ? Array.Empty<string>() : new[] { files }, noWaitAfter, timeout);

        /// <inheritdoc />
        public Task SetFilesAsync(IEnumerable<string> files, bool? noWaitAfter, float? timeout)
            => FileChooserSetFilesHelper.SetFromPathsAsync(Element, files);

        /// <inheritdoc />
        public Task SetFilesAsync(FilePayload files, bool? noWaitAfter, float? timeout)
            => SetFilesAsync(files == null ? Array.Empty<FilePayload>() : new[] { files }, noWaitAfter, timeout);

        /// <inheritdoc />
        public Task SetFilesAsync(IEnumerable<FilePayload> files, bool? noWaitAfter, float? timeout)
            => Element.SetInputFilesAsync(files, noWaitAfter, timeout, force: true);

#pragma warning disable SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
        Task IFileChooser.SetFilesAsync(string files, FileChooserSetFilesOptions options)
            => SetFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IFileChooser.SetFilesAsync(IEnumerable<string> files, FileChooserSetFilesOptions options)
            => SetFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IFileChooser.SetFilesAsync(FilePayload files, FileChooserSetFilesOptions options)
            => SetFilesAsync(files, options?.NoWaitAfter, options?.Timeout);

        Task IFileChooser.SetFilesAsync(IEnumerable<FilePayload> files, FileChooserSetFilesOptions options)
            => SetFilesAsync(files, options?.NoWaitAfter, options?.Timeout);
#pragma warning restore SA1137, SA1201, SA1202, SA1208, SA1210, SA1502, SA1518, SA1600, SA1601, SA1611, SA1615, SA1648
    }
}
