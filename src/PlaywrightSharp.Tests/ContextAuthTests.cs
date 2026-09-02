/*
 * MIT License
 *
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 */
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using NUnit.Framework;
using PlaywrightSharp.NUnit;
using PlaywrightSharp.TestServer;

namespace PlaywrightSharp.Tests
{
    /// <summary>
    /// NewContext httpCredentials applied to pages as HTTP Basic auth.
    /// </summary>
    [TestFixture]
    public class ContextAuthTests : PageTestEx
    {
        private static SimpleServer Server => TestServerSetup.Server;

        [PlaywrightTest("browsercontext-credentials.spec.ts", "httpCredentials is applied")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextHttpCredentialsShouldApplyToPage()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "user", "pass");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "user", Password = "pass" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "options bag httpCredentials")]
        [Test]
        [Timeout(30_000)]
        public async Task NewContextOptionsBagShouldApplyHttpCredentials()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "alice", "secret");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new BrowserContextOptions
            {
                HttpCredentials = new HttpCredentials { Username = "alice", Password = "secret" },
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "SetHttpCredentialsAsync after NewContext")]
        [Test]
        [Timeout(30_000)]
        public async Task SetHttpCredentialsShouldApplyToExistingPage()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "wave103", "secret");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync().ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            await context.SetHttpCredentialsAsync(new HttpCredentials
            {
                Username = "wave103",
                Password = "secret",
            }).ConfigureAwait(false);

            IResponse allowed = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(allowed, Is.Not.Null);
            Assert.That(allowed.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "SetHttpCredentialsAsync clears credentials")]
        [Test]
        [Timeout(30_000)]
        public async Task SetHttpCredentialsNullShouldStopSendingAuthorization()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "wave103", "secret");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "wave103", Password = "secret" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse allowed = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(allowed.Status, Is.EqualTo(200));

            await context.SetHttpCredentialsAsync(System.Array.Empty<HttpCredentials>()).ConfigureAwait(false);

            try
            {
                // Use a different origin so Chromium's HTTP-auth cache for localhost
                // cannot satisfy the request after credentials are cleared.
                IResponse denied = await page.GoToAsync(TestConstants.CrossProcessHttpPrefix + "/empty.html", timeout: 5_000).ConfigureAwait(false);
                Assert.That(denied, Is.Not.Null);
                Assert.That(denied.Status, Is.Not.EqualTo(200));
            }
            catch (NavigationException)
            {
                // Chromium reports net::ERR_INVALID_AUTH_CREDENTIALS.
            }
            catch (TimeoutException)
            {
                // WebKit waits on the auth challenge after credentials are cleared.
            }
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "Chromium answers a Digest challenge")]
        [Test]
        [Timeout(30_000)]
        public async Task ChromiumShouldAnswerServerDigestChallenge()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Fetch.authRequired challenge handling is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            List<string> authorizations = new();
            Server.SetRoute("/digest.html", http =>
            {
                string authorization = http.Request.Headers["Authorization"].ToString();
                authorizations.Add(authorization ?? string.Empty);
                if (!string.IsNullOrEmpty(authorization)
                    && authorization.StartsWith("Digest ", StringComparison.Ordinal))
                {
                    http.Response.ContentType = "text/html";
                    return http.Response.WriteAsync("<html><body>digest-ok</body></html>");
                }

                http.Response.Headers["WWW-Authenticate"] =
                    "Digest realm=\"Secure Area\", nonce=\"pwsharp\", qop=\"auth\", algorithm=MD5";
                http.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return http.Response.WriteAsync("HTTP Error 401 Unauthorized: Access is denied");
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "challenge", Password = "token" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.ServerUrl + "/digest.html").ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(
                response.Status,
                Is.EqualTo(200),
                "authorizations: " + string.Join(" | ", authorizations));
            Assert.That(await page.InnerTextAsync("body").ConfigureAwait(false), Is.EqualTo("digest-ok"));
            Assert.That(authorizations, Has.Some.StartsWith("Digest "));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "Chromium answers a server Basic challenge")]
        [Test]
        [Timeout(30_000)]
        public async Task ChromiumShouldAnswerServerBasicChallengeWithoutPreemptiveHeader()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Fetch.authRequired challenge handling is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "challenge", "token");
            List<string> authorizations = new();
            Server.Subscribe("/empty.html", context =>
            {
                if (context == null)
                {
                    return;
                }

                authorizations.Add(context.Request.Headers["Authorization"].ToString());
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "challenge", Password = "token" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(authorizations, Has.Count.GreaterThanOrEqualTo(2));
            Assert.That(authorizations[0], Is.Null.Or.Empty);
            Assert.That(authorizations[authorizations.Count - 1], Does.StartWith("Basic "));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "httpCredentials send Always is preemptive")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpCredentialsSendAlwaysShouldSendAuthorizationOnTheFirstRequest()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "challenge", "token");
            List<string> authorizations = new();
            Server.Subscribe("/empty.html", context =>
            {
                if (context == null)
                {
                    return;
                }

                authorizations.Add(context.Request.Headers["Authorization"].ToString());
            });

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "challenge",
                    Password = "token",
                    Send = HttpCredentialsSend.Always,
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
            Assert.That(authorizations, Has.Count.GreaterThanOrEqualTo(1));
            Assert.That(authorizations[0], Does.StartWith("Basic "));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "wrong credentials cancel the challenge")]
        [Test]
        [Timeout(30_000)]
        public async Task ChromiumShouldCancelChallengeWhenCredentialsAreWrong()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Fetch.authRequired challenge handling is Chromium-only.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "challenge", "token");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new() { HttpCredentials = new HttpCredentials { Username = "challenge", Password = "wrong" } }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            try
            {
                IResponse denied = await page.GoToAsync(TestConstants.EmptyPage, timeout: 5_000).ConfigureAwait(false);
                Assert.That(denied, Is.Not.Null);
                Assert.That(denied.Status, Is.Not.EqualTo(200));
            }
            catch (NavigationException)
            {
                // Chromium reports net::ERR_INVALID_AUTH_CREDENTIALS.
            }
            catch (TimeoutException)
            {
                // Safety net if the cancelled challenge hangs the navigation.
            }
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "httpCredentials origin matches")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpCredentialsOriginShouldApplyWhenRequestMatches()
        {
            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "origin-user", "origin-pass");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "origin-user",
                    Password = "origin-pass",
                    Origin = TestConstants.ServerUrl,
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            IResponse response = await page.GoToAsync(TestConstants.EmptyPage).ConfigureAwait(false);
            Assert.That(response, Is.Not.Null);
            Assert.That(response.Status, Is.EqualTo(200));
        }

        [PlaywrightTest("browsercontext-credentials.spec.ts", "httpCredentials origin mismatch")]
        [Test]
        [Timeout(30_000)]
        public async Task HttpCredentialsOriginShouldNotApplyToOtherOrigins()
        {
            if (!TestConstants.IsChromium)
            {
                Assert.Ignore("Origin matching is enforced on Chromium Fetch.authRequired.");
                return;
            }

            if (Server == null)
            {
                Assert.Ignore("Test server is unavailable.");
                return;
            }

            Server.Reset();
            Server.SetAuth("/empty.html", "origin-user", "origin-pass");

            await using IBrowser browser = await BrowserLauncher.LaunchAsync().ConfigureAwait(false);
            await using IBrowserContext context = await browser.NewContextAsync(new()
            {
                HttpCredentials = new HttpCredentials
                {
                    Username = "origin-user",
                    Password = "origin-pass",
                    Origin = TestConstants.ServerUrl,
                }
            }).ConfigureAwait(false);
            IPage page = await context.NewPageAsync().ConfigureAwait(false);

            try
            {
                IResponse denied = await page.GoToAsync(TestConstants.CrossProcessHttpPrefix + "/empty.html", timeout: 5_000).ConfigureAwait(false);
                Assert.That(denied, Is.Not.Null);
                Assert.That(denied.Status, Is.Not.EqualTo(200));
            }
            catch (NavigationException)
            {
                // Chromium reports net::ERR_INVALID_AUTH_CREDENTIALS.
            }
            catch (TimeoutException)
            {
                // Safety net if the cancelled challenge hangs the navigation.
            }
        }
    }
}
