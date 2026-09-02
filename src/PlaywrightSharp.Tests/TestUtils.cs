using System.IO;

namespace PlaywrightSharp.Tests
{
    internal class TestUtils
    {
        internal static string FindParentDirectory(string directory)
        {
            string current = Directory.GetCurrentDirectory();
            while (!Directory.Exists(Path.Combine(current, directory)))
            {
                current = Directory.GetParent(current).FullName;
            }
            return Path.Combine(current, directory);
        }

        internal static string GetWebServerFile(string path) => Path.Combine(FindParentDirectory("PlaywrightSharp.TestServer"), "wwwroot", path);
    }
}
