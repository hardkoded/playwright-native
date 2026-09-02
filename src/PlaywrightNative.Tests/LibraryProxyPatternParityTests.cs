/*
 * Copyright (c) 2020 Dario Kondratiuk
 * Modifications copyright (c) Microsoft Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *     http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */
using System;
using NUnit.Framework;
using PlaywrightNative.Helpers;
using PlaywrightNative.NUnit;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Official <c>library/proxy-pattern.spec.ts</c> parity. SOCKS
    /// <c>parsePattern</c> matcher. Official title keeps the upstream typo
    /// <c>socks proxy patter matcher</c>.
    /// </summary>
    [TestFixture]
    [NonParallelizable]
    public class LibraryProxyPatternParityTests : PageTestEx
    {
        [PlaywrightTest("proxy-pattern.spec.ts", "socks proxy patter matcher")]
        [Test]
        [Timeout(TestConstants.DefaultTestTimeout)]
        public void SocksProxyPatterMatcher()
        {
            Func<string, int, bool> m1 = SocksProxyPattern.Parse("*");
            Func<string, int, bool> m2 = SocksProxyPattern.Parse("<loopback>");
            Func<string, int, bool> m3 = SocksProxyPattern.Parse("<loopback>:3000");
            Func<string, int, bool> m4 = SocksProxyPattern.Parse(".com:80");
            Func<string, int, bool> m5 = SocksProxyPattern.Parse("example.com");
            Func<string, int, bool> m6 = SocksProxyPattern.Parse("*.com");
            Func<string, int, bool> m7 = SocksProxyPattern.Parse("123.123.123.123:9222");
            Func<string, int, bool> m8 = SocksProxyPattern.Parse("example.com:80,localhost,*:9222");
            Func<string, int, bool> m9 = SocksProxyPattern.Parse("127.*.*.1");
            Func<string, int, bool> m10 = SocksProxyPattern.Parse("foo?/bar.*.com");

            Assert.Multiple(() =>
            {
                Assert.That(m1("example.com", 80), Is.True);
                Assert.That(m1("some.long.example.com", 80), Is.True);
                Assert.That(m1("localhost", 3000), Is.True);
                Assert.That(m1("foo.localhost", 3000), Is.True);
                Assert.That(m1("127.0.0.1", 9222), Is.True);
                Assert.That(m1("123.123.123.123", 9222), Is.True);
                Assert.That(m1("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.True);
                Assert.That(m1("[::1]", 5000), Is.True);

                Assert.That(m2("example.com", 80), Is.False);
                Assert.That(m2("some.long.example.com", 80), Is.False);
                Assert.That(m2("localhost", 3000), Is.True);
                Assert.That(m2("foo.localhost", 3000), Is.True);
                Assert.That(m2("127.0.0.1", 9222), Is.True);
                Assert.That(m2("123.123.123.123", 9222), Is.False);
                Assert.That(m2("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m2("[::1]", 5000), Is.True);

                Assert.That(m3("example.com", 80), Is.False);
                Assert.That(m3("some.long.example.com", 80), Is.False);
                Assert.That(m3("localhost", 3000), Is.True);
                Assert.That(m3("foo.localhost", 3000), Is.True);
                Assert.That(m3("127.0.0.1", 9222), Is.False);
                Assert.That(m3("123.123.123.123", 9222), Is.False);
                Assert.That(m3("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m3("[::1]", 5000), Is.False);

                Assert.That(m4("example.com", 80), Is.True);
                Assert.That(m4("some.long.example.com", 80), Is.True);
                Assert.That(m4("localhost", 3000), Is.False);
                Assert.That(m4("foo.localhost", 3000), Is.False);
                Assert.That(m4("127.0.0.1", 9222), Is.False);
                Assert.That(m4("123.123.123.123", 9222), Is.False);
                Assert.That(m4("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m4("[::1]", 5000), Is.False);

                Assert.That(m5("example.com", 80), Is.True);
                Assert.That(m5("some.long.example.com", 80), Is.False);
                Assert.That(m5("localhost", 3000), Is.False);
                Assert.That(m5("foo.localhost", 3000), Is.False);
                Assert.That(m5("127.0.0.1", 9222), Is.False);
                Assert.That(m5("123.123.123.123", 9222), Is.False);
                Assert.That(m5("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m5("[::1]", 5000), Is.False);

                Assert.That(m6("example.com", 80), Is.True);
                Assert.That(m6("some.long.example.com", 80), Is.True);
                Assert.That(m6("localhost", 3000), Is.False);
                Assert.That(m6("foo.localhost", 3000), Is.False);
                Assert.That(m6("127.0.0.1", 9222), Is.False);
                Assert.That(m6("123.123.123.123", 9222), Is.False);
                Assert.That(m6("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m6("[::1]", 5000), Is.False);

                Assert.That(m7("example.com", 80), Is.False);
                Assert.That(m7("some.long.example.com", 80), Is.False);
                Assert.That(m7("localhost", 3000), Is.False);
                Assert.That(m7("foo.localhost", 3000), Is.False);
                Assert.That(m7("127.0.0.1", 9222), Is.False);
                Assert.That(m7("123.123.123.123", 9222), Is.True);
                Assert.That(m7("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m7("[::1]", 5000), Is.False);

                Assert.That(m8("example.com", 80), Is.True);
                Assert.That(m8("some.long.example.com", 80), Is.False);
                Assert.That(m8("localhost", 3000), Is.True);
                Assert.That(m8("foo.localhost", 3000), Is.False);
                Assert.That(m8("127.0.0.1", 9222), Is.True);
                Assert.That(m8("123.123.123.123", 9222), Is.True);
                Assert.That(m8("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m8("[::1]", 5000), Is.False);

                Assert.That(m9("example.com", 80), Is.False);
                Assert.That(m9("some.long.example.com", 80), Is.False);
                Assert.That(m9("localhost", 3000), Is.False);
                Assert.That(m9("foo.localhost", 3000), Is.False);
                Assert.That(m9("127.0.0.1", 9222), Is.False);
                Assert.That(m9("123.123.123.123", 9222), Is.False);
                Assert.That(m9("[2001:db8:3333:4444:CCCC:DDDD:EEEE:FFFF]", 8080), Is.False);
                Assert.That(m9("[::1]", 5000), Is.False);

                Assert.That(m10("foo?/bar.X.com", 80), Is.True);
                Assert.That(m10("foo?/bar.Y.com", 80), Is.True);
                Assert.That(m10("foo?/bar.com", 80), Is.False);
                Assert.That(m10("fo/bar.X.com", 80), Is.False);
                Assert.That(m10("fo?/bar.X.com", 80), Is.False);
            });
        }
    }
}
