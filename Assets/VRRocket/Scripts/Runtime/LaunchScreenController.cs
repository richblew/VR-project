using System.Text;
using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Drives the big screen on the front wall: title card, assembly status, submission received, countdown with the pad camera feed,
    /// live telemetry during the flight, and the result with the 2D failure diagram (SPEC 5.2 of the treatment: failed components in red).
    /// </summary>
    public sealed class LaunchScreenController : MonoBehaviour
    {
        [Header("Screen surface")]
        [SerializeField] Renderer m_Screen;
        [SerializeField] Material m_PlaceholderMaterial;
        [SerializeField] Material m_PadCameraMaterial;
        [Header("Text")]
        [SerializeField] TextMeshPro m_Title;
        [SerializeField] TextMeshPro m_Big;
        [SerializeField] TextMeshPro m_Status;
        [SerializeField] TextMeshPro m_Clock;
        [Header("Failure diagram (quads named Diag_*)")]
        [SerializeField] Transform m_Diagram;
        [SerializeField] Color m_DiagramOk = new Color(0.85f, 0.9f, 1f);
        [SerializeField] Color m_DiagramFailed = new Color(1f, 0.15f, 0.1f);

        static readonly int k_BaseColor = Shader.PropertyToID("_BaseColor");
        static readonly int k_Emission = Shader.PropertyToID("_EmissionColor");
        MaterialPropertyBlock m_Block;

        public void Configure(Renderer screen, Material placeholder, Material padCamera, TextMeshPro title, TextMeshPro big, TextMeshPro status, TextMeshPro clock, Transform diagram)
        {
            m_Screen = screen; m_PlaceholderMaterial = placeholder; m_PadCameraMaterial = padCamera; m_Title = title; m_Big = big; m_Status = status; m_Clock = clock; m_Diagram = diagram;
        }

        void Set(TextMeshPro t, string s) { if (t != null) t.text = s; }
        void Surface(bool padCamera) { if (m_Screen != null) m_Screen.sharedMaterial = padCamera && m_PadCameraMaterial != null ? m_PadCameraMaterial : m_PlaceholderMaterial; }
        void Diagram(bool show) { if (m_Diagram != null) m_Diagram.gameObject.SetActive(show); }

        public void ShowTitle(string headline, string sub)
        {
            Surface(false); Diagram(false);
            Set(m_Title, headline);
            Set(m_Big, "");
            Set(m_Status, sub);
            Set(m_Clock, "T-00:00:00");
        }

        public void ShowAssembly(AssemblyState state, string hint)
        {
            Surface(false); Diagram(false);
            Set(m_Title, "ASSEMBLY   |   " + Pretty(state));
            Set(m_Big, "");
            Set(m_Status, hint);
            Set(m_Clock, "T-00:00:00");
        }

        public void ShowPreLaunch()
        {
            Surface(true); Diagram(false);
            Set(m_Title, "PROTOTYPE RECEIVED   |   PAD 01 READY");
            Set(m_Big, "");
            Set(m_Status, "Press LAUNCH to begin the countdown. ABORT returns the prototype.");
            Set(m_Clock, "T-00:00:10");
        }

        public void ShowCountdown(int seconds)
        {
            Surface(true); Diagram(false);
            Set(m_Title, "COUNTDOWN   |   PAD 01");
            Set(m_Big, "T-" + seconds.ToString("00"));
            Set(m_Status, seconds <= 3 ? "IGNITION" : "ALL SYSTEMS GO");
            Set(m_Clock, "T-00:00:" + seconds.ToString("00"));
        }

        public void ShowFlight(float altitude, float speed, float t)
        {
            Surface(true); Diagram(false);
            Set(m_Title, "LIFTOFF   |   PAD 01");
            Set(m_Big, "");
            Set(m_Status, "T+" + t.ToString("00.0") + " s     ALT " + altitude.ToString("0") + " m     VEL " + speed.ToString("0") + " m/s");
            Set(m_Clock, "T+00:00:" + Mathf.FloorToInt(t).ToString("00"));
        }

        public void ShowResult(BuildReport report)
        {
            Surface(false);
            var success = report != null && report.outcome == LaunchOutcome.Success;
            Set(m_Title, success ? "MISSION SUCCESS" : "LAUNCH FAILED");
            Set(m_Big, report == null ? "" : (success ? "NOMINAL FLIGHT" : Pretty(report.outcome)));
            var sb = new StringBuilder();
            if (report != null && !success)
            {
                sb.Append("Failed components (").Append(report.FailedCount).Append("): ").Append(string.Join(", ", report.failedComponents));
                sb.Append("   |   The prototype is returned for inspection. A new kit is on the bench.");
            }
            else if (success) sb.Append("Clean launch. Well built.");
            Set(m_Status, sb.ToString());
            Diagram(report != null && !success);
            if (report != null && !success) PaintDiagram(report);
        }

        void PaintDiagram(BuildReport report)
        {
            if (m_Diagram == null) return;
            if (m_Block == null) m_Block = new MaterialPropertyBlock();
            foreach (var r in m_Diagram.GetComponentsInChildren<Renderer>(true))
            {
                var failed = false;
                var n = r.name;
                if (n == "Diag_Cap") failed = !report.capLocked;
                else if (n.StartsWith("Diag_Flap"))
                {
                    var id = n[n.Length - 1] - '0';
                    foreach (var f in report.flapPlacement) if (f.flapId == id && !f.IsCorrect) failed = true;
                }
                var c = failed ? m_DiagramFailed : m_DiagramOk;
                r.GetPropertyBlock(m_Block);
                m_Block.SetColor(k_BaseColor, c);
                m_Block.SetColor(k_Emission, c * (failed ? 2.5f : 0.6f));
                r.SetPropertyBlock(m_Block);
            }
        }

        static string Pretty(AssemblyState s)
        {
            switch (s)
            {
                case AssemblyState.BuildingAirframe: return "BUILDING AIRFRAME";
                case AssemblyState.AirframeComplete: return "AIRFRAME COMPLETE";
                case AssemblyState.MotorFitted: return "MOTOR FITTED";
                case AssemblyState.PrototypeComplete: return "PROTOTYPE COMPLETE";
                case AssemblyState.Submitted: return "SUBMITTED";
                default: return s.ToString();
            }
        }

        static string Pretty(LaunchOutcome o)
        {
            switch (o)
            {
                case LaunchOutcome.MotorRetentionLoss: return "MOTOR RETENTION LOSS";
                case LaunchOutcome.UnstableFlight: return "UNSTABLE FLIGHT";
                default: return "SUCCESS";
            }
        }
    }
}
