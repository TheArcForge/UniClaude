using System.Collections.Generic;
using NUnit.Framework;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    public class DomainReloadStrategyTests
    {
        [Test]
        public void Auto_IsNotLocked_Initially()
        {
            var strategy = new AutoReloadStrategy(120);
            Assert.IsFalse(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Auto_OnToolCallStart_LocksOnFirstCall()
        {
            var strategy = new AutoReloadStrategy(120);
            strategy.OnToolCallStart("WriteFile");
            Assert.IsTrue(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Auto_OnToolCallEnd_StaysLocked()
        {
            var strategy = new AutoReloadStrategy(120);
            strategy.OnToolCallStart("WriteFile");
            strategy.OnToolCallEnd("WriteFile");
            Assert.IsTrue(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Auto_OnTurnComplete_Unlocks()
        {
            var strategy = new AutoReloadStrategy(120);
            strategy.OnToolCallStart("WriteFile");
            strategy.OnTurnComplete();
            Assert.IsFalse(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Auto_OnTurnComplete_WhenNotLocked_NoOp()
        {
            var strategy = new AutoReloadStrategy(120);
            Assert.DoesNotThrow(() => strategy.OnTurnComplete());
            strategy.Dispose();
        }

        [Test]
        public void Auto_LogsLockAndUnlock()
        {
            var logs = new List<string>();
            var strategy = new AutoReloadStrategy(120);
            strategy.OnLog += msg => logs.Add(msg);

            strategy.OnToolCallStart("WriteFile");
            strategy.OnTurnComplete();

            Assert.AreEqual(2, logs.Count);
            Assert.That(logs[0], Does.Contain("Locked"));
            Assert.That(logs[1], Does.Contain("Unlocked"));
            strategy.Dispose();
        }

        [Test]
        public void Manual_IsNotLocked_Initially()
        {
            var strategy = new ManualReloadStrategy();
            Assert.IsFalse(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Manual_OnToolCallStart_DoesNotLock()
        {
            var strategy = new ManualReloadStrategy();
            strategy.OnToolCallStart("WriteFile");
            Assert.IsFalse(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Manual_Lock_SetsIsLocked()
        {
            var strategy = new ManualReloadStrategy();
            strategy.Lock();
            Assert.IsTrue(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Manual_Unlock_ClearsIsLocked()
        {
            var strategy = new ManualReloadStrategy();
            strategy.Lock();
            strategy.Unlock();
            Assert.IsFalse(strategy.IsLocked);
            strategy.Dispose();
        }

        [Test]
        public void Manual_OnTurnComplete_SafetyUnlock()
        {
            var strategy = new ManualReloadStrategy();
            strategy.Lock();
            strategy.OnTurnComplete();
            Assert.IsFalse(strategy.IsLocked);
            strategy.Dispose();
        }
    }
}
