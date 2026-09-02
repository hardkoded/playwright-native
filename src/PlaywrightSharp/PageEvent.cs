using System;
using System.Collections.Generic;

namespace PlaywrightSharp
{
    /// <summary>
    /// Page events for WaitForEventAsync.
    /// </summary>
    public static class PageEvent
    {
        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Request"/>.
        /// </summary>
        public static PlaywrightEvent<IRequest> Request { get; } = new PlaywrightEvent<IRequest>() { Name = "Request" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.RequestFinished"/>.
        /// </summary>
        public static PlaywrightEvent<IRequest> RequestFinished { get; } = new PlaywrightEvent<IRequest>() { Name = "RequestFinished" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.RequestFailed"/>.
        /// </summary>
        public static PlaywrightEvent<IRequest> RequestFailed { get; } = new PlaywrightEvent<IRequest>() { Name = "RequestFailed" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing the Crash event.
        /// </summary>
        public static PlaywrightEvent<IPage> Crash { get; } = new PlaywrightEvent<IPage>() { Name = "Crash" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Close"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> Close { get; } = new PlaywrightEvent<IPage>() { Name = "Close" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Response"/>.
        /// </summary>
        public static PlaywrightEvent<IResponse> Response { get; } = new PlaywrightEvent<IResponse>() { Name = "Response" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing the Download event.
        /// </summary>
        public static PlaywrightEvent<IDownload> Download { get; } = new PlaywrightEvent<IDownload>() { Name = "Download" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Console"/>.
        /// </summary>
        public static PlaywrightEvent<IConsoleMessage> Console { get; } = new PlaywrightEvent<IConsoleMessage>() { Name = "Console" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Popup"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> Popup { get; } = new PlaywrightEvent<IPage>() { Name = "Popup" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.FrameNavigated"/>.
        /// </summary>
        public static PlaywrightEvent<IFrame> FrameNavigated { get; } = new PlaywrightEvent<IFrame>() { Name = "FrameNavigated" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.FrameAttached"/>.
        /// </summary>
        public static PlaywrightEvent<IFrame> FrameAttached { get; } = new PlaywrightEvent<IFrame>() { Name = "FrameAttached" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.FrameDetached"/>.
        /// </summary>
        public static PlaywrightEvent<IFrame> FrameDetached { get; } = new PlaywrightEvent<IFrame>() { Name = "FrameDetached" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing the Worker event.
        /// </summary>
        public static PlaywrightEvent<IWorker> Worker { get; } = new PlaywrightEvent<IWorker>() { Name = "Worker" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Dialog"/>.
        /// </summary>
        public static PlaywrightEvent<IDialog> Dialog { get; } = new PlaywrightEvent<IDialog>() { Name = "Dialog" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.DialogClosed"/>.
        /// </summary>
        public static PlaywrightEvent<IDialog> DialogClosed { get; } = new PlaywrightEvent<IDialog>() { Name = "DialogClosed" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing the FileChooser event.
        /// </summary>
        public static PlaywrightEvent<IFileChooser> FileChooser { get; } = new PlaywrightEvent<IFileChooser>() { Name = "FileChooser" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.PageError"/>.
        /// </summary>
        public static PlaywrightEvent<PageErrorEventArgs> PageError { get; } = new PlaywrightEvent<PageErrorEventArgs>() { Name = "PageError" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.Load"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> Load { get; } = new PlaywrightEvent<IPage>() { Name = "Load" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IPage.DOMContentLoaded"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> DOMContentLoaded { get; } = new PlaywrightEvent<IPage>() { Name = "DOMContentLoaded" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing the WebSocket event.
        /// </summary>
        public static PlaywrightEvent<IWebSocket> WebSocket { get; } = new PlaywrightEvent<IWebSocket>() { Name = "WebSocket" };
    }
}
