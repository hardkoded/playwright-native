using System.Collections.Generic;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    [SetUpFixture]
    public class TestServerSetup
    {
        public static SimpleServer Server { get; private set; }
        public static SimpleServer HttpsServer { get; private set; }

        [OneTimeSetUp]
        public async Task InitAllAsync()
        {
            try
            {
                string contentRoot = TestUtils.FindParentDirectory("PlaywrightSharp.TestServer");
                Server = SimpleServer.Create(TestConstants.Port, contentRoot);
                HttpsServer = SimpleServer.CreateHttps(TestConstants.HttpsPort, contentRoot);

                try
                {
                    await Server.StartAsync().ConfigureAwait(false);
                }
                catch
                {
                    Server = null;
                }

                try
                {
                    await HttpsServer.StartAsync().ConfigureAwait(false);
                }
                catch
                {
                    // Missing/invalid testCert.cer must not fail the HTTP fixture.
                    HttpsServer = null;
                }
            }
            catch
            {
                // Server setup may fail (port conflicts, missing certs).
                // Some tests (for example fixtures that never start a page) don't need the server.
            }
        }

        [OneTimeTearDown]
        public async Task ShutDownAsync()
        {
            List<Task> stopTasks = new List<Task>();
            if (Server != null)
            {
                stopTasks.Add(Server.StopAsync());
            }

            if (HttpsServer != null)
            {
                stopTasks.Add(HttpsServer.StopAsync());
            }

            await Task.WhenAll(stopTasks).ConfigureAwait(false);
        }
    }
}
