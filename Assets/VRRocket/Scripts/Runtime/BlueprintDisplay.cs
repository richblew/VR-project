using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>The CRT above the bench: shows the current build step. A working stand-in for the teammates' blueprint display (SPEC 9).</summary>
    public sealed class BlueprintDisplay : MonoBehaviour
    {
        [SerializeField] TextMeshPro m_Text;

        public void Configure(TextMeshPro text) => m_Text = text;

        public void ShowIdle() => Set("BLUEPRINT\nstandby");

        public void ShowText(string text) => Set(text);

        public void ShowState(AssemblyStateMachine m)
        {
            if (m == null) { ShowIdle(); return; }
            string step;
            if (m.finsAttached < 3) step = "STEP 1 / 5   TAIL FINS  " + m.finsAttached + "/3\nslide into the slots at the base, any order";
            else if (m.flapsAttached < 3) step = "STEP 2 / 5   WING FLAPS  " + m.flapsAttached + "/3\nmidpoint slots, swept edge toward the nose";
            else if (!m.noseAttached) step = "STEP 3 / 5   NOSE CONE\npress it onto the top";
            else if (!m.motorAttached) step = "STEP 4 / 5   MOTOR\ninsert from below, nozzle down";
            else if (!m.capAttached) step = "STEP 5 / 5   MOTOR CAP\nseat it, then twist clockwise until it clicks";
            else step = "COMPLETE\ncarry the rocket to the bin and SUBMIT";
            Set(step);
        }

        void Set(string s)
        {
            if (m_Text != null) m_Text.text = s;
        }
    }
}
