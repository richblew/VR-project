using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The build guidance above the bench: the step text on the blueprint panel and, on the monitor beside it, a picture of the
    /// rocket as it should look after that step (SPEC 9 working version, planned with Richard on 12 October 2026).
    /// </summary>
    public sealed class BlueprintDisplay : MonoBehaviour
    {
        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");

        [SerializeField] TextMeshPro m_Text;
        [SerializeField, Tooltip("Optional monitor beside the text that shows the picture of the current step.")] Renderer m_ImageScreen;
        [SerializeField, Tooltip("The rocket after each step: fins, flaps, nose, motor, cap (complete). Made by VR Rocket > Capture Build Step Images.")]
        Texture[] m_StepImages = new Texture[0];

        MaterialPropertyBlock m_Block;

        /// <summary>Index into the step images of the picture on show, or -1 when there is no image screen.</summary>
        public int shownImage { get; private set; } = -1;

        public void Configure(TextMeshPro text) => m_Text = text;

        public void ConfigureImages(Renderer screen, Texture[] stepImages)
        {
            m_ImageScreen = screen; m_StepImages = stepImages;
        }

        public void ShowIdle()
        {
            Set("BLUEPRINT\nstandby");
            ShowImage(4);   // the finished rocket: what the player is about to build
        }

        public void ShowText(string text)
        {
            Set(text);
            ShowImage(4);
        }

        public void ShowState(AssemblyStateMachine m)
        {
            if (m == null) { ShowIdle(); return; }
            string step;
            int image;
            if (m.finsAttached < 3) { step = "STEP 1 / 5   TAIL FINS  " + m.finsAttached + "/3\nslide into the slots at the base, any order"; image = 0; }
            else if (m.flapsAttached < 3) { step = "STEP 2 / 5   WING FLAPS  " + m.flapsAttached + "/3\nmidpoint slots, swept edge toward the nose"; image = 1; }
            else if (!m.noseAttached) { step = "STEP 3 / 5   NOSE CONE\npress it onto the top"; image = 2; }
            else if (!m.motorAttached) { step = "STEP 4 / 5   MOTOR\ninsert from below, nozzle down"; image = 3; }
            else if (!m.capAttached) { step = "STEP 5 / 5   MOTOR CAP\nseat it, then twist clockwise until it clicks"; image = 4; }
            else { step = "COMPLETE\ncarry the rocket to the bin and SUBMIT"; image = 4; }
            Set(step);
            ShowImage(image);
        }

        void Set(string s)
        {
            if (m_Text != null) m_Text.text = s;
        }

        void ShowImage(int index)
        {
            if (m_ImageScreen == null || m_StepImages == null || m_StepImages.Length == 0) return;
            index = Mathf.Clamp(index, 0, m_StepImages.Length - 1);
            if (m_StepImages[index] == null) return;
            if (m_Block == null) m_Block = new MaterialPropertyBlock();
            m_ImageScreen.GetPropertyBlock(m_Block);
            m_Block.SetTexture(k_BaseMap, m_StepImages[index]);
            m_ImageScreen.SetPropertyBlock(m_Block);
            shownImage = index;
        }
    }
}
