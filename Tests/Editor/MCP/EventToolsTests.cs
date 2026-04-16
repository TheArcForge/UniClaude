using NUnit.Framework;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UI;
using UniClaude.Editor.MCP;

namespace UniClaude.Editor.Tests.MCP
{
    /// <summary>
    /// Tests for <see cref="EventTools"/> MCP tools.
    /// </summary>
    public class EventToolsTests
    {
        GameObject _buttonGO;
        GameObject _targetGO;
        string _originalScenePath;

        [SetUp]
        public void SetUp()
        {
            _originalScenePath = UnityEngine.SceneManagement.SceneManager.GetActiveScene().path;
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            _buttonGO = new GameObject("EventTestButton");
            _buttonGO.AddComponent<Button>();

            _targetGO = new GameObject("EventTestTarget");
            _targetGO.AddComponent<AudioSource>();
        }

        [TearDown]
        public void TearDown()
        {
            if (_buttonGO != null) Object.DestroyImmediate(_buttonGO);
            if (_targetGO != null) Object.DestroyImmediate(_targetGO);
            // Discard dirty test scene without save prompt, then restore original
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!string.IsNullOrEmpty(_originalScenePath))
                EditorSceneManager.OpenScene(_originalScenePath);
        }

        [Test]
        public void EventAddListener_VoidMethod_AddsListener()
        {
            var result = EventTools.AddListener(
                "EventTestButton", "Button", "m_OnClick",
                "EventTestTarget", "Play");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");

            var button = _buttonGO.GetComponent<Button>();
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount());
            Assert.AreEqual("Play", button.onClick.GetPersistentMethodName(0));
        }

        [Test]
        public void EventAddListener_MethodNotFound_ReturnsError()
        {
            var result = EventTools.AddListener(
                "EventTestButton", "Button", "m_OnClick",
                "EventTestTarget", "NonExistentMethod999");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("not found", result.Text);
        }

        [Test]
        public void EventAddListener_GONotFound_ReturnsError()
        {
            var result = EventTools.AddListener(
                "NonExistentGO999", "Button", "m_OnClick",
                "EventTestTarget", "Play");

            Assert.IsTrue(result.IsError);
            StringAssert.Contains("NonExistentGO999", result.Text);
        }

        [Test]
        public void EventRemoveListener_RemovesByIndex()
        {
            EventTools.AddListener(
                "EventTestButton", "Button", "m_OnClick",
                "EventTestTarget", "Play");

            var button = _buttonGO.GetComponent<Button>();
            Assert.AreEqual(1, button.onClick.GetPersistentEventCount(), "Precondition");

            var result = EventTools.RemoveListener(
                "EventTestButton", "Button", "m_OnClick", "0");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            Assert.AreEqual(0, button.onClick.GetPersistentEventCount());
        }

        [Test]
        public void EventRemoveListener_InvalidIndex_ReturnsError()
        {
            var result = EventTools.RemoveListener(
                "EventTestButton", "Button", "m_OnClick", "99");

            Assert.IsTrue(result.IsError);
        }

        [Test]
        public void EventListListeners_EmptyEvent_ReturnsEmpty()
        {
            var result = EventTools.ListListeners(
                "EventTestButton", "Button", "m_OnClick");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("0", result.Text);
        }

        [Test]
        public void EventListListeners_WithListeners_ReturnsDetails()
        {
            EventTools.AddListener(
                "EventTestButton", "Button", "m_OnClick",
                "EventTestTarget", "Play");

            var result = EventTools.ListListeners(
                "EventTestButton", "Button", "m_OnClick");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("Play", result.Text);
            StringAssert.Contains("EventTestTarget", result.Text);
        }

        [Test]
        public void EventFindAll_FindsButtonOnClick()
        {
            var result = EventTools.FindAllEvents("EventTestButton", "Button");

            Assert.IsFalse(result.IsError, $"Expected success but got error: {result.Text}");
            StringAssert.Contains("m_OnClick", result.Text);
        }

        [Test]
        public void EventFindAll_GONotFound_ReturnsError()
        {
            var result = EventTools.FindAllEvents("NonExistentGO999");

            Assert.IsTrue(result.IsError);
        }
    }
}
