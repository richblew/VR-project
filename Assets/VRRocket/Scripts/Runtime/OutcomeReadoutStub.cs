using System.Text;
using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Stub of the launch screen's result (SPEC.md 8.5): a world-space panel showing the build report, with two buttons that run
    /// the failed-launch sequence (inspect-only return plus a new build) and the abort sequence (editable return to the stand).
    /// Delete when the real launch flow arrives.
    /// </summary>
    public sealed class OutcomeReadoutStub : MonoBehaviour
    {
        [SerializeField] RocketAssembly m_Assembly;
        [SerializeField] TextMeshPro m_Text;
        [SerializeField, Tooltip("Where a failed prototype comes back for inspection (the bin's return spot).")] Transform m_InspectAnchor;

        RocketAssembly m_Hooked;

        public RocketAssembly assembly
        {
            get
            {
                if (m_Assembly == null) m_Assembly = FindFirstObjectByType<RocketAssembly>();
                return m_Assembly;
            }
            set => m_Assembly = value;
        }

        public void Configure(RocketAssembly assembly, TextMeshPro text, Transform inspectAnchor)
        {
            m_Assembly = assembly; m_Text = text; m_InspectAnchor = inspectAnchor;
        }

        void OnEnable()
        {
            Hook();
            Show(null);
        }

        void OnDisable()
        {
            if (m_Hooked != null)
            {
                m_Hooked.Submitted -= OnSubmitted;
                m_Hooked.StateChanged -= OnStateChanged;
                m_Hooked = null;
            }
        }

        void Hook()
        {
            var a = assembly;
            if (a == null || a == m_Hooked) return;
            if (m_Hooked != null) { m_Hooked.Submitted -= OnSubmitted; m_Hooked.StateChanged -= OnStateChanged; }
            m_Hooked = a;
            a.Submitted += OnSubmitted;
            a.StateChanged += OnStateChanged;
        }

        void OnSubmitted(BuildReport report) => Show(report);
        void OnStateChanged(AssemblyState state) { if (state != AssemblyState.Submitted) Show(null); }

        public void Show(BuildReport report)
        {
            if (m_Text == null) return;
            var sb = new StringBuilder();
            sb.AppendLine("<b>OUTCOME READOUT (stub)</b>");
            var a = assembly;
            sb.AppendLine("State: " + (a != null ? a.State.ToString() : "?"));
            if (report == null)
            {
                sb.AppendLine("Waiting for submission.");
            }
            else
            {
                sb.AppendLine("Outcome: <b>" + report.outcome + "</b>");
                sb.AppendLine("Cap locked: " + report.capLocked + "    Flap error: " + report.FlapError);
                foreach (var f in report.flapPlacement)
                    sb.AppendLine("Flap " + f.flapId + ": " + f.slotName + "  " + f.position + "/" + f.orientation + (f.IsCorrect ? "" : "  (wrong)"));
                sb.AppendLine("Failed (" + report.FailedCount + "): " + string.Join(", ", report.failedComponents));
            }
            m_Text.text = sb.ToString();
        }

        /// <summary>Wired to the stub "Failed launch" button.</summary>
        public void OnFailedLaunchPressed()
        {
            var a = assembly;
            if (a == null || a.State != AssemblyState.Submitted) { Debug.Log("OutcomeReadoutStub: nothing submitted."); return; }
            a.ReturnPrototype(ReturnMode.InspectOnly, m_InspectAnchor);
            a.BeginNewBuild();
        }

        /// <summary>Wired to the stub "Abort" button.</summary>
        public void OnAbortPressed()
        {
            var a = assembly;
            if (a == null || a.State != AssemblyState.Submitted) { Debug.Log("OutcomeReadoutStub: nothing submitted."); return; }
            a.ReturnPrototype(ReturnMode.Editable, null);
        }
    }
}
