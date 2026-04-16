using NUnit.Framework;
using UniClaude.Editor;

namespace UniClaude.Editor.Tests
{
    [TestFixture]
    public class AuthErrorDetectionTests
    {
        [TestCase("Authentication required", true)]
        [TestCase("unauthorized", true)]
        [TestCase("User is not authenticated", true)]
        [TestCase("Invalid API key provided", true)]
        [TestCase("invalid_api_key", true)]
        [TestCase("Please run claude login", true)]
        [TestCase("permission denied for api call", true)]
        [TestCase("permission denied on filesystem", false)]
        [TestCase("Network timeout", false)]
        [TestCase("Connection refused", false)]
        [TestCase("", false)]
        [TestCase(null, false)]
        public void IsAuthError_DetectsCorrectly(string message, bool expected)
        {
            Assert.AreEqual(expected, UniClaudeWindow.IsAuthError(message));
        }
    }
}
