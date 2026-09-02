using System;
using Microsoft.AspNetCore.Http;

namespace PlaywrightNative.TestServer
{
    public class RequestReceivedEventArgs : EventArgs
    {
        public HttpRequest Request { get; set; }
    }
}
