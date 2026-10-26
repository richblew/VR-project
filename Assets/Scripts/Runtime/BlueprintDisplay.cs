using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The build guidance at the bench: the step text on the blueprint panel and, on the monitor beside it, a picture of the
    /// rocket as it should look after that step (planned with Richard on 12 October 2026). The player can browse the steps with
    /// BACK and NEXT buttons (<see cref="ShowPreviousStep"/>, <see cref="ShowNextStep"/>); as soon as the build changes, both
    /// screens go back to the live step.
    /// </summary>
    public sealed class BlueprintDisplay : MonoBehaviour
    {
        static readonly int k_BaseMap = Shader.PropertyToID("_BaseMap");

        public const int StepCount = 5;
        static readonly string[] k_StepTitles = { "TAIL FINS", "WING FLAPS", "NOSE CONE", "BATTERY", "BATTERY CAP" };
        static readonly string[] k_StepHints =
        {
            "slide orange fins into the slots at the base, any order",
            "slide red fins into the upper slots, any order",
            "place on the top of the cylinder",
            "hover battery above bottom hole and drop into cylinder",
            "place it over battery, and twist hand",
        };

        [SerializeField] TextMeshPro m_Text;
        [SerializeField, Tooltip("Optional monitor beside the text that shows the picture of the current step.")] Renderer m_ImageScreen;
        [SerializeField, Tooltip("The rocket after each step: fins, flaps, nose, battery, cap (complete). Made by VR Rocket > Capture Build Step Images.")]
        Texture[] m_StepImages = new Texture[0];

        MaterialPropertyBlock m_Block;
        int m_LiveImage = StepCount - 1;

        /// <summary>Index into the step images of the picture on show, or -1 when there is no image screen.</summary>
        public int shownImage { get; private set; } = -1;

        /// <summary>The text on the blueprint panel.</summary>
        public string shownText => m_Text != null ? m_Text.text : null;

        /// <summary>The step being browsed (0 to 4), or -1 while the screens follow the build.</summary>
        public int browsedStep { get; private set; } = -1;

        public void Configure(TextMeshPro text) => m_Text = text;

        public void ConfigureImages(Renderer screen, Texture[] stepImages)
        {
            m_ImageScreen = screen; m_StepImages = stepImages;
        }

        public void ShowIdle() => ShowLive("BLUEPRINT\nstandby", StepCount - 1);   // the finished rocket: what the player is about to build

        public void ShowText(string text) => ShowLive(text, StepCount - 1);

        public void ShowState(AssemblyStateMachine m)
        {
            if (m == null) { ShowIdle(); return; }
            int step;
            if (m.finsAttached < 3) step = 0;
            else if (m.flapsAttached < 3) step = 1;
            else if (!m.noseAttached) step = 2;
            else if (!m.motorAttached) step = 3;
            else if (!m.capAttached) step = 4;
            else { ShowLive("COMPLETE\ncarry the rocket to the bin and SUBMIT", StepCount - 1); return; }
            var count = step == 0 ? "  " + m.finsAttached + "/3" : step == 1 ? "  " + m.flapsAttached + "/3" : "";
            ShowLive("STEP " + (step + 1) + " / " + StepCount + "   " + k_StepTitles[step] + count + "\n" + k_StepHints[step], step);
        }

        /// <summary>Wired to the NEXT button: shows the following step's picture and instruction.</summary>
        public void ShowNextStep() => Browse(+1);

        /// <summary>Wired to the BACK button: shows the previous step's picture and instruction.</summary>
        public void ShowPreviousStep() => Browse(-1);

        void Browse(int delta)
        {
            var from = browsedStep >= 0 ? browsedStep : m_LiveImage;
            browsedStep = Mathf.Clamp(from + delta, 0, StepCount - 1);
            Set("VIEWING STEP " + (browsedStep + 1) + " / " + StepCount + "   " + k_StepTitles[browsedStep] + "\n" + k_StepHints[browsedStep]);
            ShowImage(browsedStep);
        }

        /// <summary>The build (or the game) moved on: show it, and stop browsing.</summary>
        void ShowLive(string text, int image)
        {
            m_LiveImage = image;
            browsedStep = -1;
            Set(text);
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
