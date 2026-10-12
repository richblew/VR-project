using System.Reflection;
using NUnit.Framework;
using UnityEngine;

namespace VRRocket.Tests
{
    /// <summary>
    /// The team project enters Play mode without a domain reload, so every static the VR Rocket systems keep must be reset by a
    /// [RuntimeInitializeOnLoadMethod] at the start of each session. Each reset is invoked here the way Unity would.
    /// </summary>
    public class StaticResetsTests
    {
        static void InvokeReset(System.Type type)
        {
            var m = type.GetMethod("ResetStatics", BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public);
            Assert.IsNotNull(m, type.Name + ".ResetStatics exists");
            var attr = m.GetCustomAttribute<RuntimeInitializeOnLoadMethodAttribute>();
            Assert.IsNotNull(attr, type.Name + ".ResetStatics runs on load");
            Assert.AreEqual(RuntimeInitializeLoadType.SubsystemRegistration, attr.loadType, type.Name + " resets before any scene loads");
            m.Invoke(null, null);
        }

        [Test]
        public void AssemblyEvents_Lose_Their_Subscribers()
        {
            System.Action<RocketPart> handler = _ => { };
            AssemblyEvents.CapLocked += handler;
            InvokeReset(typeof(AssemblyEvents));
            foreach (var f in typeof(AssemblyEvents).GetFields(BindingFlags.Static | BindingFlags.NonPublic | BindingFlags.Public))
                if (typeof(System.Delegate).IsAssignableFrom(f.FieldType)) Assert.IsNull(f.GetValue(null), f.Name + " cleared");
        }

        [Test]
        public void Singletons_Are_Forgotten()
        {
            InvokeReset(typeof(AssemblyFeedback));
            Assert.IsNull(AssemblyFeedback.instance);
            InvokeReset(typeof(RoomSceneLoader));
            Assert.IsNull(RoomSceneLoader.instance);
            InvokeReset(typeof(AttachPoint));
            var active = typeof(AttachPoint).GetField("s_Active", BindingFlags.Static | BindingFlags.NonPublic);
            Assert.IsNotNull(active);
            Assert.AreEqual(0, (int)active.FieldType.GetProperty("Count").GetValue(active.GetValue(null)), "no attach points carried over");
        }
    }

    /// <summary>Haptics designed for Touch Plus voice coils are lifted to what an HTC Vive wand's motor can render.</summary>
    public class FeedbackDeviceTuningTests
    {
        FeedbackLibrary m_Library;

        [SetUp] public void SetUp() => m_Library = ScriptableObject.CreateInstance<FeedbackLibrary>();
        [TearDown] public void TearDown() => Object.DestroyImmediate(m_Library);

        [Test]
        public void Vive_Gets_Floors_And_The_Runtime_Frequency()
        {
            var tick = new FeedbackLibrary.Haptic(0.07f, 0.01f, 220f, 0.05f, 0.01f, 0.02f);
            var tuned = m_Library.Tune(tick, "ViveController HTC Vive Controller OpenXR");
            Assert.AreEqual(0.35f, tuned.amplitude, 1e-6f);
            Assert.AreEqual(0.04f, tuned.duration, 1e-6f);
            Assert.AreEqual(0f, tuned.frequency, "eccentric motors get the runtime's default frequency");
            Assert.AreEqual(0.35f, tuned.secondAmplitude, 1e-6f);
            Assert.AreEqual(0.04f, tuned.secondDuration, 1e-6f);
            var strong = m_Library.Tune(new FeedbackLibrary.Haptic(1f, 0.2f, 80f), "ViveController");
            Assert.AreEqual(1f, strong.amplitude, "strong pulses are left alone");
            Assert.AreEqual(0.2f, strong.duration, 1e-6f);
        }

        [Test]
        public void Index_Keeps_Frequency_And_TouchPlus_Is_Untouched()
        {
            var tick = new FeedbackLibrary.Haptic(0.07f, 0.01f, 220f);
            var index = m_Library.Tune(tick, "ValveIndexController Knuckles");
            Assert.AreEqual(0.25f, index.amplitude, 1e-6f);
            Assert.AreEqual(0.02f, index.duration, 1e-6f);
            Assert.AreEqual(220f, index.frequency);
            var touch = m_Library.Tune(tick, "MetaQuestTouchPlusController Meta Quest Touch Plus");
            Assert.AreEqual(tick.amplitude, touch.amplitude);
            Assert.AreEqual(tick.duration, touch.duration);
            Assert.AreEqual(tick.frequency, touch.frequency);
            var none = m_Library.Tune(tick, "");
            Assert.AreEqual(tick.amplitude, none.amplitude, "no controller: as designed");
            var silent = m_Library.Tune(new FeedbackLibrary.Haptic(0f, 0f), "ViveController");
            Assert.IsTrue(silent.isSilent, "silence stays silent");
        }
    }

    /// <summary>The big screen shows the first sentence of long status lines; the console mirror shows all of it.</summary>
    public class ScreenTextTests
    {
        [Test]
        public void FirstSentences_Cuts_At_Full_Stops_And_Bars()
        {
            Assert.AreEqual("Turn around.", LaunchScreenController.FirstSentences("Turn around. The kit is on the desk.", 1));
            Assert.AreEqual("Turn around. The kit is on the desk.", LaunchScreenController.FirstSentences("Turn around. The kit is on the desk.", 2));
            Assert.AreEqual("Failed components (1): motor cap", LaunchScreenController.FirstSentences("Failed components (1): motor cap   |   The prototype is returned.", 1));
            Assert.AreEqual("T+12.5 s     ALT 120 m", LaunchScreenController.FirstSentences("T+12.5 s     ALT 120 m", 1), "decimals are not sentence ends");
            Assert.AreEqual("All of it. Really.", LaunchScreenController.FirstSentences("All of it. Really.", 0));
            Assert.AreEqual("", LaunchScreenController.FirstSentences("", 1));
        }
    }
}
