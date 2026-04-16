using System;
using System.Net.Http;
using System.Text;
using System.Threading.Tasks;
using NUnit.Framework;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    public class HttpTransportTests
    {
        HttpTransport _transport;

        [SetUp]
        public void SetUp()
        {
            _transport = new HttpTransport();
        }

        [TearDown]
        public void TearDown()
        {
            _transport?.Dispose();
        }

        [Test]
        public void Start_SetsIsRunningTrue()
        {
            _transport.SetRequestHandler(json => Task.FromResult("{\"ok\":true}"));
            _transport.Start(0);
            Assert.IsTrue(_transport.IsRunning);
        }

        [Test]
        public void Start_AssignsEndpoint()
        {
            _transport.SetRequestHandler(json => Task.FromResult("{\"ok\":true}"));
            _transport.Start(0);
            Assert.IsNotNull(_transport.Endpoint);
            Assert.That(_transport.Endpoint, Does.StartWith("http://127.0.0.1:"));
        }

        [Test]
        public void Stop_SetsIsRunningFalse()
        {
            _transport.SetRequestHandler(json => Task.FromResult("{\"ok\":true}"));
            _transport.Start(0);
            _transport.Stop();
            Assert.IsFalse(_transport.IsRunning);
        }

        [Test]
        public void Start_WithoutHandler_ThrowsInvalidOperation()
        {
            Assert.Throws<InvalidOperationException>(() => _transport.Start(0));
        }

        [Test]
        public async Task RPC_RoundTrip_ReturnsHandlerResponse()
        {
            _transport.SetRequestHandler(json => Task.FromResult("{\"echo\":true}"));
            _transport.Start(0);

            using var client = new HttpClient();
            var content = new StringContent("{\"test\":1}", Encoding.UTF8, "application/json");
            var response = await client.PostAsync(_transport.Endpoint + "rpc", content);
            var body = await response.Content.ReadAsStringAsync();

            Assert.AreEqual("{\"echo\":true}", body);
        }

        [Test]
        public async Task RPC_Returns404_ForUnknownPath()
        {
            _transport.SetRequestHandler(json => Task.FromResult("{}"));
            _transport.Start(0);

            using var client = new HttpClient();
            var response = await client.GetAsync(_transport.Endpoint + "unknown");

            Assert.AreEqual(404, (int)response.StatusCode);
        }
    }
}
