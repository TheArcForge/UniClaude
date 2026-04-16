using NUnit.Framework;
using UniClaude.Editor.MCP;
using UnityEditor;

namespace UniClaude.Editor.Tests.MCP
{
    public class MCPServerTests
    {
        MCPServer _server;

        [SetUp]
        public void SetUp()
        {
            // Use a unique prefix to avoid polluting real settings
            _server = new MCPServer();
        }

        [TearDown]
        public void TearDown()
        {
            _server?.Dispose();
            // Clean up test settings
            EditorPrefs.DeleteKey("Test_MCP_Port");
            EditorPrefs.DeleteKey("Test_MCP_Enabled");
            EditorPrefs.DeleteKey("Test_MCP_AutoStart");
            EditorPrefs.DeleteKey("Test_MCP_LogLevel");
            EditorPrefs.DeleteKey("Test_MCP_ReloadStrategy");
            EditorPrefs.DeleteKey("Test_MCP_ReloadTimeout");
            EditorPrefs.DeleteKey("Test_MCP_VerboseLogging");
        }

        [Test]
        public void Start_SetsInstance()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            Assert.IsNotNull(MCPServer.Instance);
        }

        [Test]
        public void Start_TransportIsRunning()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            Assert.IsTrue(_server.IsRunning);
        }

        [Test]
        public void Stop_ClearsRunning()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            _server.Stop();
            Assert.IsFalse(_server.IsRunning);
        }

        [Test]
        public void Endpoint_ReturnsTransportEndpoint()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            Assert.IsNotNull(_server.Endpoint);
            Assert.That(_server.Endpoint, Does.StartWith("http://127.0.0.1:"));
        }

        [Test]
        public void ReloadStrategy_IsNotNull_AfterStart()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            Assert.IsNotNull(_server.ActiveReloadStrategy);
        }

        [Test]
        public void ReloadStrategy_IsAuto_ByDefault()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            Assert.IsInstanceOf<AutoReloadStrategy>(_server.ActiveReloadStrategy);
        }

        [Test]
        public void ReloadStrategy_IsManual_WhenConfigured()
        {
            var settings = new MCPSettings("Test_MCP_");
            settings.DomainReloadStrategy = ReloadStrategy.Manual;
            _server.Start(settings);
            Assert.IsInstanceOf<ManualReloadStrategy>(_server.ActiveReloadStrategy);
        }

        [Test]
        public void NotifyTurnComplete_DoesNotThrow()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            Assert.DoesNotThrow(() => _server.NotifyTurnComplete());
        }

        [Test]
        public void Dispose_StopsTransport()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            _server.Dispose();
            Assert.IsFalse(_server.IsRunning);
        }

        [Test]
        public void Dispose_ClearsInstance()
        {
            _server.Start(new MCPSettings("Test_MCP_"));
            _server.Dispose();
            Assert.IsNull(MCPServer.Instance);
        }
    }
}
