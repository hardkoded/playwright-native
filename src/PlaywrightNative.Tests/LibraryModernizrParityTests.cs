/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Globalization;
using System.IO;
using System.Net;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using NUnit.Framework;
using PlaywrightNative.NUnit;
using PlaywrightNative.TestServer;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/modernizr.spec.ts</c> parity. Both titles skip
    /// unless WebKit. Skip Node-only <c>library/heap.spec.ts</c>
    /// (<c>node:inspector</c>). Skip Node-only <c>browsertype-connect*</c>,
    /// <c>browsertype-launch-server</c>, <c>browsertype-launch-selenium</c>,
    /// <c>browsers-path.spec.ts</c>, <c>channels.spec.ts</c>,
    /// <c>library/browsercontext-reuse.spec.ts</c>
    /// (<c>_newContextForReuse</c>), and
    /// <c>library/browsercontext-fetch-happy-eyeballs.spec.ts</c>
    /// (<c>__testHookLookup</c>).
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryModernizrParityTests : PageTestEx
    {
        private static SimpleServer _ownedHttps;
        private static string HttpsPrefix = TestConstants.HttpsPrefix;
        private static string GoldensRoot;

        private static SimpleServer HttpsServer => _ownedHttps ?? TestServerSetup.HttpsServer;

        private static bool IsLinux => !TestConstants.IsWindows && !TestConstants.IsMacOSX;

        [OneTimeSetUp]
        public async Task StartOwnedServerAsync()
        {
            string contentRoot = TestUtils.FindParentDirectory("PlaywrightNative.TestServer");
            GoldensRoot = Path.Combine(contentRoot, "wwwroot", "modernizr");
            await StartOwnedHttpsAsync(contentRoot).ConfigureAwait(false);
            if (HttpsServer == null && TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
            }

            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        [OneTimeTearDown]
        public async Task StopOwnedServerAsync()
        {
            if (_ownedHttps != null)
            {
                await _ownedHttps.StopAsync().ConfigureAwait(false);
                _ownedHttps = null;
            }
        }

        [PlaywrightTest("modernizr.spec.ts", "Safari Desktop")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task SafariDesktop()
        {
            SkipUnlessWebKit();
            EnsureHttps();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            IBrowserContext context = await browser.NewContextAsync(new() { DeviceScaleFactor = 2, IgnoreHTTPSErrors = true }).ConfigureAwait(false);
            try
            {
                (JsonObject actual, JsonObject expected) = await CheckFeaturesAsync(context, "safari-26").ConfigureAwait(false);

                // Shipping Safari exposes `font-display` as a settable CSS property,
                // but open-source WebKit keeps it a @font-face descriptor only.
                expected["fontdisplay"] = false;

                actual.Remove("webglextensions");
                expected.Remove("webglextensions");
                CoerceTruthy(actual, "audio");
                CoerceTruthy(expected, "audio");
                CoerceTruthy(actual, "video");
                CoerceTruthy(expected, "video");

                if (IsLinux)
                {
                    expected["speechrecognition"] = false;
                    expected["mediastream"] = false;
                    expected["todataurlwebp"] = true;
                    actual.Remove("variablefonts");
                    expected.Remove("variablefonts");
                }

                if (TestConstants.IsWindows)
                {
                    ApplyWin32Overrides(expected);
                }

                AssertReportsEqual(actual, expected);
            }
            finally
            {
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        [PlaywrightTest("modernizr.spec.ts", "Mobile Safari")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public async Task MobileSafari()
        {
            SkipUnlessWebKit();
            EnsureHttps();
            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            BrowserContextOptions iPhone = new BrowserContextOptions(Playwright.Devices["iPhone 12"])
            {
                IgnoreHTTPSErrors = true,
            };
            IBrowserContext context = await browser.NewContextAsync(iPhone).ConfigureAwait(false);
            try
            {
                (JsonObject actual, JsonObject expected) = await CheckFeaturesAsync(context, "mobile-safari-26").ConfigureAwait(false);

                expected["capture"] = false;
                expected["cssscrollbar"] = true;
                expected["cssvhunit"] = true;
                expected["cssvmaxunit"] = true;
                expected["overflowscrolling"] = false;
                expected["mediasource"] = true;
                expected["scrolltooptions"] = false;

                expected["fontdisplay"] = false;

                actual.Remove("webglextensions");
                expected.Remove("webglextensions");
                CoerceTruthy(actual, "audio");
                CoerceTruthy(expected, "audio");
                CoerceTruthy(actual, "video");
                CoerceTruthy(expected, "video");

                if (IsLinux)
                {
                    expected["speechrecognition"] = false;
                    expected["mediastream"] = false;
                    expected["todataurlwebp"] = true;
                    actual.Remove("variablefonts");
                    expected.Remove("variablefonts");
                }

                if (TestConstants.IsWindows)
                {
                    ApplyWin32Overrides(expected);
                }

                AssertReportsEqual(actual, expected);
            }
            finally
            {
                await context.CloseAsync().ConfigureAwait(false);
            }
        }

        private static void SkipUnlessWebKit()
        {
            if (!TestConstants.IsWebKit)
            {
                Assert.Ignore("official skip: browserName !== 'webkit'");
            }
        }

        private static void EnsureHttps()
        {
            if (HttpsServer == null)
            {
                Assert.Ignore("HTTPS test server is unavailable.");
            }
        }

        private static async Task<(JsonObject Actual, JsonObject Expected)> CheckFeaturesAsync(
            IBrowserContext context,
            string name)
        {
            IPage page = await context.NewPageAsync().ConfigureAwait(false);
            await page.GoToAsync(HttpsPrefix + "/modernizr/index.html").ConfigureAwait(false);
            string actualJson = await page.EvaluateAsync<string>("() => JSON.stringify(window.report)").ConfigureAwait(false);
            JsonObject actual = JsonNode.Parse(actualJson) as JsonObject;
            Assert.That(actual, Is.Not.Null);
            string expectedPath = Path.Combine(GoldensRoot, name + ".json");
            JsonObject expected = JsonNode.Parse(File.ReadAllText(expectedPath)) as JsonObject;
            Assert.That(expected, Is.Not.Null);
            return (actual, expected);
        }

        private static void CoerceTruthy(JsonObject obj, string key)
        {
            if (!obj.ContainsKey(key) || obj[key] is null)
            {
                obj[key] = false;
                return;
            }

            if (obj[key] is JsonValue value && value.TryGetValue(out bool flag))
            {
                obj[key] = flag;
                return;
            }

            obj[key] = true;
        }

        private static void ApplyWin32Overrides(JsonObject expected)
        {
            expected["getusermedia"] = false;
            expected["peerconnection"] = false;
            expected["speechrecognition"] = false;
            expected["speechsynthesis"] = false;
            expected["todataurlwebp"] = true;
            expected["webaudio"] = false;
            expected["gamepads"] = false;
            expected.Remove("datalistelem");
            expected["mediastream"] = false;
            expected["mediasource"] = false;
            expected["datachannel"] = false;
            if (expected["inputtypes"] is JsonObject inputTypes)
            {
                inputTypes["color"] = false;
                inputTypes["date"] = false;
                inputTypes["datetime-local"] = false;
                inputTypes["time"] = false;
            }
        }

        private static void AssertReportsEqual(JsonObject actual, JsonObject expected)
        {
            Assert.That(
                JsonNode.DeepEquals(actual, expected),
                Is.True,
                "actual=" + actual.ToJsonString() + Environment.NewLine + "expected=" + expected.ToJsonString());
        }

        private static async Task StartOwnedHttpsAsync(string contentRoot)
        {
            if (TestServerSetup.HttpsServer != null)
            {
                HttpsPrefix = TestConstants.HttpsPrefix;
                return;
            }

            string certPath = EnsureTestCertificate(contentRoot);
            Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PATH", certPath);
            if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD")))
            {
                Environment.SetEnvironmentVariable("PLAYWRIGHT_TEST_CERT_PASSWORD", "playwright");
            }

            int basePort = 19883;
            for (int i = 0; i < 20; i++)
            {
                int port = basePort + i;
                try
                {
                    SimpleServer https = SimpleServer.CreateHttps(port, contentRoot);
                    await https.StartAsync().ConfigureAwait(false);
                    _ownedHttps = https;
                    HttpsPrefix = "https://localhost:" + port.ToString(CultureInfo.InvariantCulture);
                    return;
                }
                catch (Exception)
                {
                }
            }
        }

        private static string EnsureTestCertificate(string contentRoot)
        {
            string certPath = Path.Combine(contentRoot, "key.pfx");
            if (File.Exists(certPath))
            {
                return certPath;
            }

            using RSA rsa = RSA.Create(2048);
            CertificateRequest request = new(
                "CN=localhost",
                rsa,
                HashAlgorithmName.SHA256,
                RSASignaturePadding.Pkcs1);
            SubjectAlternativeNameBuilder san = new();
            san.AddDnsName("localhost");
            san.AddIpAddress(IPAddress.Loopback);
            request.CertificateExtensions.Add(san.Build());
            using X509Certificate2 cert = request.CreateSelfSigned(
                DateTimeOffset.UtcNow.AddDays(-1),
                DateTimeOffset.UtcNow.AddYears(10));
            File.WriteAllBytes(certPath, cert.Export(X509ContentType.Pfx, "playwright"));
            return certPath;
        }
    }
}
