/*
 * Copyright (c) 2026 Dario Kondratiuk
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
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using NUnit.Framework;

namespace PlaywrightNative.Tests
{
    /// <summary>
    /// Runs the web-platform-tests URL Pattern data (urlpattern/resources/urlpatterntestdata.json, BSD-3-Clause,
    /// see Assets/urlpattern/LICENSE.md) against <see cref="URLPattern"/>. It follows the logic of
    /// urlpattern/resources/urlpatterntests.js. These are not upstream Playwright tests, so they use
    /// <see cref="TestCaseSourceAttribute"/> and carry no PlaywrightTest attribute. No browser is needed.
    /// </summary>
    public class URLPatternTests
    {
        private static readonly string[] _components = ["protocol", "username", "password", "hostname", "port", "pathname", "search", "hash"];

        /// <summary>
        /// WPT entries that this port does not pass, by index in the data file, with the reason.
        /// </summary>
        private static readonly Dictionary<int, string> _knownFailures = new()
        {
            // {"pathname": "*{}**?"}: test() matches, but exec() reports group "1" as "" instead of undefined.
            // JavaScript rejects an optional group iteration that matches the empty string; .NET Regex keeps it.
            [329] = "an empty optional group is captured as \"\" instead of undefined (.NET Regex semantics).",
        };

        public static IEnumerable<TestCaseData> WptEntries()
        {
            string path = Path.Combine(AppContext.BaseDirectory, "Assets", "urlpattern", "urlpatterntestdata.json");
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(path));
            int index = 0;
            foreach (JsonElement entry in document.RootElement.EnumerateArray())
            {
                string json = entry.GetRawText();
                var data = new TestCaseData(index, json).SetName($"Wpt{index:D3}").SetDescription(json);
                if (_knownFailures.TryGetValue(index, out string reason))
                {
                    data.Ignore("Known failure: " + reason);
                }

                yield return data;
                index++;
            }
        }

        [TestCaseSource(nameof(WptEntries))]
        public void ShouldMatchWebPlatformTestsData(int index, string json)
        {
            using JsonDocument document = JsonDocument.Parse(json);
            JsonElement entry = document.RootElement;
            JsonElement[] pattern = entry.GetProperty("pattern").EnumerateArray().ToArray();
            JsonElement expectedObj = entry.TryGetProperty("expected_obj", out JsonElement obj) ? obj : default;

            Func<URLPattern> create = CreatePattern(pattern);
            if (create == null)
            {
                Assert.Ignore("The .NET overloads cannot express this call: " + json);
            }

            if (expectedObj.ValueKind == JsonValueKind.String && Text(expectedObj) == "error")
            {
                Assert.Throws<ArgumentException>(() => create());
                return;
            }

            URLPattern urlPattern = create();
            string[] exactlyEmpty = entry.TryGetProperty("exactly_empty_components", out JsonElement empty)
                ? empty.EnumerateArray().Select(e => Text(e)).ToArray()
                : [];

            foreach (string component in _components)
            {
                string expected = ExpectedPatternComponent(pattern, expectedObj, exactlyEmpty, component);
                Assert.That(GetComponent(urlPattern, component), Is.EqualTo(expected), $"compiled pattern property '{component}'");
            }

            if (!entry.TryGetProperty("inputs", out JsonElement inputsElement))
            {
                return;
            }

            JsonElement[] inputs = inputsElement.EnumerateArray().ToArray();
            JsonElement expectedMatch = entry.GetProperty("expected_match");
            Func<URLPatternResult> exec = CreateExec(urlPattern, inputs);
            Func<bool> test = CreateTest(urlPattern, inputs);
            if (exec == null)
            {
                Assert.Ignore("The .NET overloads cannot express this call: " + json);
            }

            bool shouldMatch = expectedMatch.ValueKind == JsonValueKind.Object;
            Assert.That(test(), Is.EqualTo(shouldMatch), "test() result");
            URLPatternResult result = exec();
            if (!shouldMatch)
            {
                Assert.That(result, Is.Null, "exec() failed match result");
                return;
            }

            Assert.That(result, Is.Not.Null, "exec() result");
            foreach (string component in _components)
            {
                URLPatternComponentResult actual = GetComponentResult(result, component);
                string expectedInput = string.Empty;
                var expectedGroups = new Dictionary<string, string>();
                if (expectedMatch.TryGetProperty(component, out JsonElement expectedComponent))
                {
                    expectedInput = Text(expectedComponent.GetProperty("input"));
                    foreach (JsonProperty group in expectedComponent.GetProperty("groups").EnumerateObject())
                    {
                        expectedGroups[group.Name] = group.Value.ValueKind == JsonValueKind.Null ? null : Text(group.Value);
                    }
                }
                else if (!exactlyEmpty.Contains(component))
                {
                    expectedGroups["0"] = string.Empty;
                }

                Assert.That(actual.Input, Is.EqualTo(expectedInput), $"exec() result for {component} input");
                Assert.That(actual.Groups, Is.EquivalentTo(expectedGroups), $"exec() result for {component} groups");
            }
        }

        [TestCase("https://example.com/books/1", true)]
        [TestCase("https://example.com/films/1", false)]
        [TestCase("not a url", false)]
        public void PredicateShouldBeStableAndMatchLikeTest(string url, bool expected)
        {
            var pattern = new URLPattern("https://example.com/books/:id");
            Func<string, bool> predicate = pattern.Predicate;
            Assert.That(pattern.Predicate, Is.SameAs(predicate));
            Assert.That(predicate(url), Is.EqualTo(expected));
        }

        private static Func<URLPattern> CreatePattern(JsonElement[] args)
        {
            if (args.Length == 0)
            {
                return () => new URLPattern(new URLPatternInit());
            }

            if (args[0].ValueKind == JsonValueKind.String)
            {
                string input = Text(args[0]);
                return args.Length switch
                {
                    1 => () => new URLPattern(input),
                    2 when args[1].ValueKind == JsonValueKind.String => () => new URLPattern(input, Text(args[1])),
                    2 when args[1].ValueKind == JsonValueKind.Object => () => new URLPattern(input, ToOptions(args[1])),
                    3 when args[1].ValueKind == JsonValueKind.String => () => new URLPattern(input, Text(args[1]), ToOptions(args[2])),
                    _ => null,
                };
            }

            URLPatternInit init = ToInit(args[0]);
            return args.Length switch
            {
                1 => () => new URLPattern(init),
                2 when args[1].ValueKind == JsonValueKind.Object => () => new URLPattern(init, ToOptions(args[1])),
                _ => null,
            };
        }

        private static Func<URLPatternResult> CreateExec(URLPattern pattern, JsonElement[] inputs)
        {
            if (inputs.Length == 0)
            {
                return () => pattern.Exec(new URLPatternInit());
            }

            if (inputs[0].ValueKind == JsonValueKind.String)
            {
                string input = Text(inputs[0]);
                string baseURL = inputs.Length > 1 ? Text(inputs[1]) : null;
                return () => pattern.Exec(input, baseURL);
            }

            return inputs.Length == 1 ? () => pattern.Exec(ToInit(inputs[0])) : null;
        }

        private static Func<bool> CreateTest(URLPattern pattern, JsonElement[] inputs)
        {
            if (inputs.Length == 0)
            {
                return () => pattern.Test(new URLPatternInit());
            }

            if (inputs[0].ValueKind == JsonValueKind.String)
            {
                string input = Text(inputs[0]);
                string baseURL = inputs.Length > 1 ? Text(inputs[1]) : null;
                return () => pattern.Test(input, baseURL);
            }

            return inputs.Length == 1 ? () => pattern.Test(ToInit(inputs[0])) : null;
        }

        private static URLPatternInit ToInit(JsonElement element) => new()
        {
            Protocol = Get(element, "protocol"),
            Username = Get(element, "username"),
            Password = Get(element, "password"),
            Hostname = Get(element, "hostname"),
            Port = Get(element, "port"),
            Pathname = Get(element, "pathname"),
            Search = Get(element, "search"),
            Hash = Get(element, "hash"),
            BaseURL = Get(element, "baseURL"),
        };

        /// <summary>
        /// Reads a JSON string. The data has lone surrogates, which <see cref="JsonElement.GetString"/> rejects,
        /// so this decodes the raw literal by hand in that case.
        /// </summary>
        private static string Text(JsonElement element)
        {
            try
            {
                return element.GetString();
            }
            catch (InvalidOperationException)
            {
                string raw = element.GetRawText();
                var result = new System.Text.StringBuilder();
                for (int i = 1; i < raw.Length - 1; i++)
                {
                    if (raw[i] != '\\')
                    {
                        result.Append(raw[i]);
                        continue;
                    }

                    char escape = raw[++i];
                    switch (escape)
                    {
                        case 'u':
                            result.Append((char)Convert.ToInt32(raw.Substring(i + 1, 4), 16));
                            i += 4;
                            break;
                        case 'b':
                            result.Append('\b');
                            break;
                        case 'f':
                            result.Append('\f');
                            break;
                        case 'n':
                            result.Append('\n');
                            break;
                        case 'r':
                            result.Append('\r');
                            break;
                        case 't':
                            result.Append('\t');
                            break;
                        default:
                            result.Append(escape);
                            break;
                    }
                }

                return result.ToString();
            }
        }

        private static URLPatternOptions ToOptions(JsonElement element)
            => new() { IgnoreCase = element.TryGetProperty("ignoreCase", out JsonElement value) && value.GetBoolean() };

        private static string Get(JsonElement element, string name)
            => element.TryGetProperty(name, out JsonElement value) ? Text(value) : null;

        /// <summary>
        /// Computes the expected pattern string the way urlpatterntests.js does.
        /// </summary>
        private static string ExpectedPatternComponent(JsonElement[] pattern, JsonElement expectedObj, string[] exactlyEmpty, string component)
        {
            if (expectedObj.ValueKind == JsonValueKind.Object && expectedObj.TryGetProperty(component, out JsonElement explicitValue))
            {
                return Text(explicitValue);
            }

            if (exactlyEmpty.Contains(component))
            {
                return string.Empty;
            }

            bool initPattern = pattern.Length > 0 && pattern[0].ValueKind == JsonValueKind.Object;
            if (initPattern && pattern[0].TryGetProperty(component, out JsonElement given) && Text(given).Length > 0)
            {
                return Text(given);
            }

            string[] earlier = component switch
            {
                "hostname" => ["protocol"],
                "port" => ["protocol", "hostname"],
                "pathname" => ["protocol", "hostname", "port"],
                "search" => ["protocol", "hostname", "port", "pathname"],
                "hash" => ["protocol", "hostname", "port", "pathname", "search"],
                _ => [],
            };
            if (initPattern && earlier.Any(c => pattern[0].TryGetProperty(c, out _)))
            {
                return "*";
            }

            string baseURL = null;
            if (initPattern && pattern[0].TryGetProperty("baseURL", out JsonElement initBase))
            {
                baseURL = Text(initBase);
            }
            else if (pattern.Length > 1 && pattern[1].ValueKind == JsonValueKind.String)
            {
                baseURL = Text(pattern[1]);
            }

            if (baseURL != null && component != "username" && component != "password")
            {
                UrlRecord url = UrlParser.Parse(baseURL);
                return component switch
                {
                    "protocol" => url.Scheme,
                    "hostname" => url.Host ?? string.Empty,
                    "port" => url.Port?.ToString(System.Globalization.CultureInfo.InvariantCulture) ?? string.Empty,
                    "pathname" => url.SerializePath(),
                    "search" => url.Query ?? string.Empty,
                    _ => url.Fragment ?? string.Empty,
                };
            }

            return "*";
        }

        private static string GetComponent(URLPattern pattern, string component) => component switch
        {
            "protocol" => pattern.Protocol,
            "username" => pattern.Username,
            "password" => pattern.Password,
            "hostname" => pattern.Hostname,
            "port" => pattern.Port,
            "pathname" => pattern.Pathname,
            "search" => pattern.Search,
            _ => pattern.Hash,
        };

        private static URLPatternComponentResult GetComponentResult(URLPatternResult result, string component) => component switch
        {
            "protocol" => result.Protocol,
            "username" => result.Username,
            "password" => result.Password,
            "hostname" => result.Hostname,
            "port" => result.Port,
            "pathname" => result.Pathname,
            "search" => result.Search,
            _ => result.Hash,
        };
    }
}
