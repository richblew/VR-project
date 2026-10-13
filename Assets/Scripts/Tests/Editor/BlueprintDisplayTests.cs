using NUnit.Framework;
using TMPro;
using UnityEngine;

namespace VRRocket.Tests
{
    /// <summary>The build-step monitor: every build stage shows its own step text and the matching picture on the screen.</summary>
    public class BlueprintDisplayTests
    {
        GameObject m_Root;
        BlueprintDisplay m_Display;
        TextMeshPro m_Text;
        MeshRenderer m_Screen;
        Texture2D[] m_Images;

        [SetUp]
        public void SetUp()
        {
            m_Root = new GameObject("BlueprintTest");
            m_Display = m_Root.AddComponent<BlueprintDisplay>();
            m_Text = new GameObject("Text").AddComponent<TextMeshPro>();
            m_Text.transform.SetParent(m_Root.transform);
            m_Screen = new GameObject("Screen").AddComponent<MeshRenderer>();
            m_Screen.transform.SetParent(m_Root.transform);
            m_Images = new Texture2D[5];
            for (var i = 0; i < 5; i++) m_Images[i] = new Texture2D(2, 2) { name = "Step" + i };
            m_Display.Configure(m_Text);
            m_Display.ConfigureImages(m_Screen, m_Images);
        }

        [TearDown]
        public void TearDown()
        {
            Object.DestroyImmediate(m_Root);
            foreach (var t in m_Images) Object.DestroyImmediate(t);
        }

        Texture Shown()
        {
            var block = new MaterialPropertyBlock();
            m_Screen.GetPropertyBlock(block);
            return block.GetTexture("_BaseMap");
        }

        void Expect(int image, string textStart, string why)
        {
            Assert.AreEqual(image, m_Display.shownImage, why);
            Assert.AreSame(m_Images[image], Shown(), why + ": the screen really shows that picture");
            StringAssert.StartsWith(textStart, m_Text.text, why);
        }

        [Test]
        public void Every_Stage_Shows_Its_Step_And_Picture()
        {
            var m = new AssemblyStateMachine { enforceOrder = false };
            m_Display.ShowIdle();
            Expect(4, "BLUEPRINT", "menu: the finished rocket");
            m_Display.ShowState(m);
            Expect(0, "STEP 1 / 5", "bare tube: fins");
            m.Apply(PartType.TailFin, true); m.Apply(PartType.TailFin, true);
            m_Display.ShowState(m);
            Expect(0, "STEP 1 / 5   TAIL FINS  2/3", "two fins: still the fins step");
            m.Apply(PartType.TailFin, true);
            m_Display.ShowState(m);
            Expect(1, "STEP 2 / 5", "fins done: flaps");
            for (var i = 0; i < 3; i++) m.Apply(PartType.WingFlap, true);
            m_Display.ShowState(m);
            Expect(2, "STEP 3 / 5", "flaps done: nose");
            m.Apply(PartType.NoseCone, true);
            m_Display.ShowState(m);
            Expect(3, "STEP 4 / 5", "nose done: motor");
            m.Apply(PartType.Motor, true);
            m_Display.ShowState(m);
            Expect(4, "STEP 5 / 5", "motor in: cap");
            m.Apply(PartType.MotorCap, true);
            m_Display.ShowState(m);
            Expect(4, "COMPLETE", "complete: the finished rocket");
            m.Apply(PartType.NoseCone, false);
            m_Display.ShowState(m);
            Expect(2, "STEP 3 / 5", "taking the nose off goes back to the nose step");
            m_Display.ShowText("Rocket on the pad.");
            Expect(4, "Rocket on the pad.", "free text keeps the finished rocket");
        }

        [Test]
        public void Back_And_Next_Browse_The_Steps_Until_The_Build_Changes()
        {
            var m = new AssemblyStateMachine { enforceOrder = false };
            for (var i = 0; i < 3; i++) m.Apply(PartType.TailFin, true);
            m_Display.ShowState(m);
            Expect(1, "STEP 2 / 5", "live: the flaps step");
            m_Display.ShowNextStep();
            Expect(2, "VIEWING STEP 3 / 5", "NEXT shows the nose step");
            Assert.AreEqual(2, m_Display.browsedStep);
            for (var i = 0; i < 5; i++) m_Display.ShowNextStep();
            Expect(4, "VIEWING STEP 5 / 5", "NEXT stops at the last step");
            for (var i = 0; i < 9; i++) m_Display.ShowPreviousStep();
            Expect(0, "VIEWING STEP 1 / 5", "BACK stops at the first step");
            m.Apply(PartType.WingFlap, true);
            m_Display.ShowState(m);
            Expect(1, "STEP 2 / 5   WING FLAPS  1/3", "a part attached: back to the live step");
            Assert.AreEqual(-1, m_Display.browsedStep);
        }

        [Test]
        public void Without_A_Screen_Or_Pictures_Only_The_Text_Changes()
        {
            m_Display.ConfigureImages(null, null);
            Assert.DoesNotThrow(() => m_Display.ShowState(new AssemblyStateMachine()));
            StringAssert.StartsWith("STEP 1 / 5", m_Text.text);
        }
    }
}
