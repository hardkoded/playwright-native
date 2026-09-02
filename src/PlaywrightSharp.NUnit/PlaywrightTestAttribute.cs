using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text.Json;
using NUnit.Framework;
using NUnit.Framework.Interfaces;
using NUnit.Framework.Internal;
using PlaywrightSharp.NUnit.TestExpectations;

namespace PlaywrightSharp.NUnit
{
    /// <summary>
    /// Enables decorating test facts with information about the corresponding test in the upstream repository.
    /// </summary>
    [AttributeUsage(AttributeTargets.Method, AllowMultiple = true)]
    public class PlaywrightTestAttribute : NUnitAttribute, IApplyToTest
    {
        private static TestExpectation[] _localExpectations;

        /// <summary>
        /// Gets whether the current product is Chromium.
        /// </summary>
        public static readonly bool IsChromium = string.IsNullOrEmpty(Environment.GetEnvironmentVariable("PRODUCT")) ||
            Environment.GetEnvironmentVariable("PRODUCT").Equals("CHROMIUM", StringComparison.OrdinalIgnoreCase);

        /// <summary>
        /// Gets whether the current product is Firefox.
        /// </summary>
        public static readonly bool IsFirefox =
            Environment.GetEnvironmentVariable("PRODUCT")?.Equals("FIREFOX", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Gets whether the current product is WebKit.
        /// </summary>
        public static readonly bool IsWebkit =
            Environment.GetEnvironmentVariable("PRODUCT")?.Equals("WEBKIT", StringComparison.OrdinalIgnoreCase) == true;

        /// <summary>
        /// Creates a new instance of the attribute.
        /// </summary>
        /// <param name="fileName"><see cref="FileName"/></param>
        /// <param name="nameOfTest"><see cref="TestName"/></param>
        public PlaywrightTestAttribute(string fileName, string nameOfTest)
        {
            FileName = fileName;
            TestName = nameOfTest;
        }

        /// <summary>
        /// Creates a new instance of the attribute.
        /// </summary>
        /// <param name="fileName"><see cref="FileName"/></param>
        /// <param name="describe"><see cref="Describe"/></param>
        /// <param name="nameOfTest"><see cref="TestName"/></param>
        public PlaywrightTestAttribute(string fileName, string describe, string nameOfTest) : this(fileName, nameOfTest)
        {
            Describe = describe;
        }

        /// <summary>
        /// The file name origin of the test.
        /// </summary>
        public string FileName { get; }

        /// <summary>
        /// Returns the trimmed file name.
        /// </summary>
        public string TrimmedName => FileName.Substring(0, FileName.IndexOf('.'));

        /// <summary>
        /// The name of the test, the decorated code is based on.
        /// </summary>
        public string TestName { get; }

        /// <summary>
        /// The describe of the test, the decorated code is based on, if one exists.
        /// </summary>
        public string Describe { get; }

        /// <inheritdoc/>
        public override string ToString()
            => Describe == null ? $"[{FileName}] {TestName}" : $"[{FileName}] {Describe} {TestName}";

        /// <inheritdoc/>
        public void ApplyToTest(Test test)
        {
            if (test == null)
            {
                return;
            }

            if (ShouldSkipByExpectation(test, out TestExpectation expectation))
            {
                test.RunState = RunState.Ignored;
                test.Properties.Set(PropertyNames.SkipReason, $"Skipped by expectation {expectation.TestIdPattern}");
            }
        }

        private bool ShouldSkipByExpectation(Test test, out TestExpectation output)
        {
            TestExpectation.TestExpectationPlatform currentPlatform = GetCurrentExpectationPlatform();
            TestExpectation.TestExpectationsParameter browserParam = IsChromium
                ? TestExpectation.TestExpectationsParameter.Chromium
                : IsFirefox
                    ? TestExpectation.TestExpectationsParameter.Firefox
                    : TestExpectation.TestExpectationsParameter.Webkit;

            string headlessEnv = Environment.GetEnvironmentVariable("HEADLESS");
            TestExpectation.TestExpectationsParameter modeParam =
                string.Equals(headlessEnv, "false", StringComparison.OrdinalIgnoreCase)
                    ? TestExpectation.TestExpectationsParameter.Headful
                    : TestExpectation.TestExpectationsParameter.Headless;

            TestExpectation.TestExpectationsParameter[] parameters = new[] { browserParam, modeParam };

            TestExpectation[] localExpectations = GetLocalExpectations();
            string testIdStr = ToString();

            foreach (TestExpectation expectation in localExpectations)
            {
                if (expectation.TestIdRegex.IsMatch(testIdStr))
                {
                    bool platformMatch = expectation.Platforms.Contains(currentPlatform);
                    bool paramsMatch = expectation.Parameters.Length == 0 ||
                        expectation.Parameters.All(p => parameters.Contains(p));

                    if (platformMatch && paramsMatch)
                    {
                        bool shouldSkip =
                            expectation.Expectations.Contains(TestExpectation.TestExpectationResult.Skip) ||
                            expectation.Expectations.Contains(TestExpectation.TestExpectationResult.Fail) ||
                            expectation.Expectations.Contains(TestExpectation.TestExpectationResult.Timeout);

                        if (shouldSkip)
                        {
                            output = expectation;
                            return true;
                        }

                        if (expectation.Expectations.Contains(TestExpectation.TestExpectationResult.Pass))
                        {
                            output = null;
                            return false;
                        }
                    }
                }
            }

            output = null;
            return false;
        }

        private static TestExpectation.TestExpectationPlatform GetCurrentExpectationPlatform()
        {
            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.Windows))
            {
                return TestExpectation.TestExpectationPlatform.Win32;
            }

            if (RuntimeInformation.IsOSPlatform(System.Runtime.InteropServices.OSPlatform.OSX))
            {
                return TestExpectation.TestExpectationPlatform.Darwin;
            }

            return TestExpectation.TestExpectationPlatform.Linux;
        }

        private static TestExpectation[] GetLocalExpectations() =>
            _localExpectations ??= LoadExpectationsFromResource("PlaywrightSharp.NUnit.TestExpectations.TestExpectations.local.json");

        private static readonly JsonSerializerOptions DefaultJsonSerializerOptions =
            new()
            {
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            };

        private static TestExpectation[] LoadExpectationsFromResource(string resourceName)
        {
            Assembly assembly = Assembly.GetExecutingAssembly();

            using Stream stream = assembly.GetManifestResourceStream(resourceName);
            using StreamReader reader = new StreamReader(stream);
            string fileContent = reader.ReadToEnd();
            return JsonSerializer.Deserialize<TestExpectation[]>(fileContent, DefaultJsonSerializerOptions);
        }
    }
}
