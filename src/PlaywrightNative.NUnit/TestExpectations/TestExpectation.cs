// * MIT License
//  *
//  * Copyright (c) Dario Kondratiuk
//  *
//  * Permission is hereby granted, free of charge, to any person obtaining a copy
//  * of this software and associated documentation files (the "Software"), to deal
//  * in the Software without restriction, including without limitation the rights
//  * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
//  * copies of the Software, and to permit persons to whom the Software is
//  * furnished to do so, subject to the following conditions:
//  *
//  * The above copyright notice and this permission notice shall be included in all
//  * copies or substantial portions of the Software.
//  *
//  * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
//  * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
//  * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
//  * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
//  * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
//  * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
//  * SOFTWARE.

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
