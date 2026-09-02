// Copyright (c) Microsoft Corporation.
// Licensed under the MIT license.
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// <see cref="PageTest"/> plus the shared HTTP/HTTPS test servers.
    /// Browser install/resolve comes from <see cref="BrowserTest.LaunchOptionsAsync"/>.
    /// </summary>
    public class PageTestEx : PageTest
    {
        /// <summary>Gets the HTTP test server (port 8081).</summary>
        public SimpleServer Server => TestServerSetup.Server;

        /// <summary>Gets the HTTPS test server (port 8082).</summary>
        public SimpleServer HttpsServer => TestServerSetup.HttpsServer;

        /// <inheritdoc />
        [SetUp]
        public void ResetServers()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }
    }

    /// <summary>
    /// <see cref="ContextTest"/> plus the shared HTTP/HTTPS test servers.
    /// </summary>
    public class ContextTestEx : ContextTest
    {
        /// <summary>Gets the HTTP test server (port 8081).</summary>
        public SimpleServer Server => TestServerSetup.Server;

        /// <summary>Gets the HTTPS test server (port 8082).</summary>
        public SimpleServer HttpsServer => TestServerSetup.HttpsServer;

        /// <inheritdoc />
        [SetUp]
        public void ResetServers()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }
    }

    /// <summary>
    /// <see cref="BrowserTest"/> plus the shared HTTP/HTTPS test servers.
    /// </summary>
    public class BrowserTestEx : BrowserTest
    {
        /// <summary>Gets the HTTP test server (port 8081).</summary>
        public SimpleServer Server => TestServerSetup.Server;

        /// <summary>Gets the HTTPS test server (port 8082).</summary>
        public SimpleServer HttpsServer => TestServerSetup.HttpsServer;

        /// <inheritdoc />
        [SetUp]
        public void ResetServers()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }
    }

    /// <summary>
    /// <see cref="PlaywrightTest"/> plus the shared HTTP/HTTPS test servers (no auto browser).
    /// </summary>
    public class PlaywrightTestEx : PlaywrightNative.NUnit.PlaywrightTest
    {
        /// <summary>Gets the HTTP test server (port 8081).</summary>
        public SimpleServer Server => TestServerSetup.Server;

        /// <summary>Gets the HTTPS test server (port 8082).</summary>
        public SimpleServer HttpsServer => TestServerSetup.HttpsServer;

        /// <inheritdoc />
        [SetUp]
        public void ResetServers()
        {
            Server?.Reset();
            HttpsServer?.Reset();
        }
    }
}
