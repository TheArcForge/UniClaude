using Newtonsoft.Json;
using NUnit.Framework;
using UniClaude.Editor.Installer;

namespace UniClaude.Editor.Tests.Installer
{
    [TestFixture]
    public class TransitionStatusTests
    {
        [Test]
        public void Parse_OkResult_PopulatesFields()
        {
            var json = @"{""command"":""to-ninja"",""result"":""ok"",""mode"":""ninja"",""timestamp"":""2026-04-17T00:00:00Z""}";
            var s = JsonConvert.DeserializeObject<TransitionStatus>(json);
            Assert.AreEqual("to-ninja", s.Command);
            Assert.AreEqual("ok", s.Result);
            Assert.AreEqual("ninja", s.Mode);
        }

        [Test]
        public void Parse_ErrorResult_ExposesErrorMessage()
        {
            var json = @"{""command"":""to-ninja"",""result"":""error"",""error"":""clone failed""}";
            var s = JsonConvert.DeserializeObject<TransitionStatus>(json);
            Assert.AreEqual("error", s.Result);
            Assert.AreEqual("clone failed", s.Error);
        }

        [Test]
        public void Serialize_RoundTrip_PreservesFields()
        {
            var s = new TransitionStatus { Command = "to-ninja", Result = "ok", Mode = "ninja" };
            var json = JsonConvert.SerializeObject(s);
            var back = JsonConvert.DeserializeObject<TransitionStatus>(json);
            Assert.AreEqual(s.Command, back.Command);
            Assert.AreEqual(s.Result, back.Result);
            Assert.AreEqual(s.Mode, back.Mode);
        }
    }
}
