namespace PlaywrightNative
{
    /// <summary>
    /// Browser context events for <see cref="IBrowserContext.WaitForEventAsync{T}"/>.
    /// </summary>
    public static class BrowserContextEvent
    {
        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Page"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> Page { get; } = new PlaywrightEvent<IPage>() { Name = "Page" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Close"/>.
        /// </summary>
        public static PlaywrightEvent<IBrowserContext> Close { get; } = new PlaywrightEvent<IBrowserContext>() { Name = "Close" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Request"/>.
        /// </summary>
        public static PlaywrightEvent<IRequest> Request { get; } = new PlaywrightEvent<IRequest>() { Name = "Request" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Response"/>.
        /// </summary>
        public static PlaywrightEvent<IResponse> Response { get; } = new PlaywrightEvent<IResponse>() { Name = "Response" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.RequestFailed"/>.
        /// </summary>
        public static PlaywrightEvent<IRequest> RequestFailed { get; } = new PlaywrightEvent<IRequest>() { Name = "RequestFailed" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.RequestFinished"/>.
        /// </summary>
        public static PlaywrightEvent<IRequest> RequestFinished { get; } = new PlaywrightEvent<IRequest>() { Name = "RequestFinished" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.ServiceWorker"/>.
        /// </summary>
        public static PlaywrightEvent<IWorker> ServiceWorker { get; } = new PlaywrightEvent<IWorker>() { Name = "ServiceWorker" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Console"/>.
        /// </summary>
        public static PlaywrightEvent<IConsoleMessage> Console { get; } = new PlaywrightEvent<IConsoleMessage>() { Name = "Console" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Download"/>.
        /// </summary>
        public static PlaywrightEvent<IDownload> Download { get; } = new PlaywrightEvent<IDownload>() { Name = "Download" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.Dialog"/>.
        /// </summary>
        public static PlaywrightEvent<IDialog> Dialog { get; } = new PlaywrightEvent<IDialog>() { Name = "Dialog" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.DialogClosed"/>.
        /// </summary>
        public static PlaywrightEvent<IDialog> DialogClosed { get; } = new PlaywrightEvent<IDialog>() { Name = "DialogClosed" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.PageClose"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> PageClose { get; } = new PlaywrightEvent<IPage>() { Name = "PageClose" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.PageLoad"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> PageLoad { get; } = new PlaywrightEvent<IPage>() { Name = "PageLoad" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.FrameAttached"/>.
        /// </summary>
        public static PlaywrightEvent<IFrame> FrameAttached { get; } = new PlaywrightEvent<IFrame>() { Name = "FrameAttached" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.FrameDetached"/>.
        /// </summary>
        public static PlaywrightEvent<IFrame> FrameDetached { get; } = new PlaywrightEvent<IFrame>() { Name = "FrameDetached" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.FrameNavigated"/>.
        /// </summary>
        public static PlaywrightEvent<IFrame> FrameNavigated { get; } = new PlaywrightEvent<IFrame>() { Name = "FrameNavigated" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.WebError"/>.
        /// </summary>
        public static PlaywrightEvent<IWebError> WebError { get; } = new PlaywrightEvent<IWebError>() { Name = "WebError" };

        /// <summary>
        /// <see cref="PlaywrightEvent{T}"/> representing a <see cref="IBrowserContext.BackgroundPage"/>.
        /// </summary>
        public static PlaywrightEvent<IPage> BackgroundPage { get; } = new PlaywrightEvent<IPage>() { Name = "BackgroundPage" };
    }
}
