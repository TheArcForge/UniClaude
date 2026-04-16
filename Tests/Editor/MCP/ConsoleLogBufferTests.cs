using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="ConsoleLogBuffer"/> persistence and log capture.
    /// </summary>
    public class ConsoleLogBufferTests
    {
        [Test]
        public void GetRecent_ReturnsEntries_AfterLogReceived()
        {
            // Force init so subscription is active
            ConsoleLogBuffer.Initialize();

            LogAssert.Expect(LogType.Error, "TestError_ConsoleBufferTest_12345");
            Debug.LogError("TestError_ConsoleBufferTest_12345");

            var entries = ConsoleLogBuffer.GetRecent(10);
            Assert.IsTrue(entries.Length > 0, "Expected at least one log entry");

            bool found = false;
            foreach (var e in entries)
            {
                if (e.message.Contains("TestError_ConsoleBufferTest_12345"))
                {
                    found = true;
                    Assert.AreEqual(LogType.Error, e.type);
                    break;
                }
            }
            Assert.IsTrue(found, "Expected to find test error message in buffer");
        }

        [Test]
        public void SaveAndRestore_PreservesEntries()
        {
            ConsoleLogBuffer.Initialize();
            LogAssert.Expect(LogType.Error, "PersistTest_12345");
            Debug.LogError("PersistTest_12345");

            // Force save
            ConsoleLogBuffer.SaveToSessionState();

            // Clear in-memory buffer
            ConsoleLogBuffer.ClearBuffer();
            var empty = ConsoleLogBuffer.GetRecent(10);

            // Restore
            ConsoleLogBuffer.RestoreFromSessionState();
            var restored = ConsoleLogBuffer.GetRecent(50);

            bool found = false;
            foreach (var e in restored)
            {
                if (e.message.Contains("PersistTest_12345"))
                {
                    found = true;
                    break;
                }
            }
            Assert.IsTrue(found, "Expected to find persisted entry after restore");
        }
    }
}
