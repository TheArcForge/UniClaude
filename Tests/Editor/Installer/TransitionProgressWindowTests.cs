using NUnit.Framework;
using UniClaude.Editor.Installer;

namespace UniClaude.Editor.Tests.Installer
{
    public class TransitionProgressWindowTests
    {
        [Test]
        public void Title_ToStandard()
        {
            Assert.AreEqual(
                "UniClaude: Converting to Standard Mode",
                TransitionProgressWindow.TitleFor(TransitionKind.ToStandard));
        }

        [Test]
        public void Title_DeleteFromNinja()
        {
            Assert.AreEqual(
                "UniClaude: Removing UniClaude",
                TransitionProgressWindow.TitleFor(TransitionKind.DeleteFromNinja));
        }

        [Test]
        public void RowStates_HappyPath_AllComplete()
        {
            var status = new TransitionStatus { Step = "complete", Result = "ok" };
            var rows = TransitionProgressWindow.ComputeRowStates(status);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Staging);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Quitting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Deleting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Relaunching);
        }

        [Test]
        public void RowStates_RelaunchFailed_LastRowFailed()
        {
            var status = new TransitionStatus
            {
                Step = "complete",
                Result = "ok",
                RelaunchError = "ENOENT: Unity binary not found",
            };
            var rows = TransitionProgressWindow.ComputeRowStates(status);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Staging);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Quitting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Deleting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Failed, rows.Relaunching);
        }

        [Test]
        public void RowStates_DeleteError_DeleteRowFailed()
        {
            var status = new TransitionStatus
            {
                Step = "deleting",
                Result = "error",
                Error = "EBUSY",
            };
            var rows = TransitionProgressWindow.ComputeRowStates(status);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Staging);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Quitting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Failed, rows.Deleting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Pending, rows.Relaunching);
        }

        [Test]
        public void RowStates_AwaitingExit_InProgress()
        {
            var status = new TransitionStatus { Step = "awaiting-exit", Result = "in-progress" };
            var rows = TransitionProgressWindow.ComputeRowStates(status);
            Assert.AreEqual(TransitionProgressWindow.RowState.Complete, rows.Staging);
            Assert.AreEqual(TransitionProgressWindow.RowState.Active, rows.Quitting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Pending, rows.Deleting);
            Assert.AreEqual(TransitionProgressWindow.RowState.Pending, rows.Relaunching);
        }

        [Test]
        public void IsTerminal_Complete_True()
        {
            Assert.IsTrue(TransitionProgressWindow.IsTerminal(
                new TransitionStatus { Step = "complete", Result = "ok" }));
        }

        [Test]
        public void IsTerminal_Error_True()
        {
            Assert.IsTrue(TransitionProgressWindow.IsTerminal(
                new TransitionStatus { Step = "deleting", Result = "error" }));
        }

        [Test]
        public void IsTerminal_InProgress_False()
        {
            Assert.IsFalse(TransitionProgressWindow.IsTerminal(
                new TransitionStatus { Step = "awaiting-exit", Result = "in-progress" }));
        }
    }
}
