/*
 * Copyright (c) Dario Kondratiuk
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
using System.Runtime.Serialization;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;

namespace PlaywrightNative.NUnit.TestExpectations;

public class TestExpectation
{
    private Lazy<Regex> _testIdRegex;

    public string TestIdPattern
    {
        get => _testIdPattern;
        set
        {
            _testIdPattern = value;
            _testIdRegex = new Lazy<Regex>(() =>
            {
                // Replace `*` with a placeholder before escaping special characters.
                string patternRegExString = Regex.Escape(_testIdPattern.Replace("*", "--STAR--"));

                // Replace placeholder with greedy match
                patternRegExString = patternRegExString.Replace("--STAR--", "(.*)?");

                // Match beginning and end explicitly
                return new Regex($"^{patternRegExString}$");
            });
        }
    }

    private string _testIdPattern;

    public Regex TestIdRegex => _testIdRegex?.Value;

    public TestExpectationPlatform[] Platforms { get; set; }

    public TestExpectationsParameter[] Parameters { get; set; }

    public TestExpectationResult[] Expectations { get; set; }

    [JsonConverter(typeof(JsonStringEnumConverter<TestExpectationResult>))]
    public enum TestExpectationResult
    {
        [EnumMember(Value = "FAIL")] Fail,
        [EnumMember(Value = "PASS")] Pass,
        [EnumMember(Value = "SKIP")] Skip,
        [EnumMember(Value = "TIMEOUT")] Timeout,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<TestExpectationsParameter>))]
    public enum TestExpectationsParameter
    {
        [EnumMember(Value = "chromium")] Chromium,
        [EnumMember(Value = "firefox")] Firefox,
        [EnumMember(Value = "webkit")] Webkit,
        [EnumMember(Value = "headless")] Headless,
        [EnumMember(Value = "headful")] Headful,
    }

    [JsonConverter(typeof(JsonStringEnumConverter<TestExpectationPlatform>))]
    public enum TestExpectationPlatform
    {
        [EnumMember(Value = "darwin")] Darwin,
        [EnumMember(Value = "linux")] Linux,
        [EnumMember(Value = "win32")] Win32,
    }
}
