using System.Text;
using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Drives the big screen on the front wall: title card, assembly status, submission received, countdown with the pad camera feed,
    /// live telemetry during the flight, and the result with the 2D failure diagram (SPEC 5.2 of the treatment: failed components in red).
    /// An optional mirror (a console monitor within reach) repeats the surface, title, clock and the full status text, so the
    /// operator can follow the launch without reading a screen across the room.
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
        [SerializeField, Tooltip("Sentences of the status text shown on the big screen; 0 shows all. The mirror always shows all.")]
        int m_StatusSentences;
        [SerializeField, Tooltip("Optional backdrop behind the big text, shown only while it has text (the countdown over the bright pad feed).")]
        GameObject m_BigBackdrop;
        [Header("Mirror (optional)")]
        [SerializeField] Renderer m_MirrorScreen;
        [SerializeField] TextMeshPro m_MirrorTitle;
        [SerializeField] TextMeshPro m_MirrorStatus;
        [SerializeField] TextMeshPro m_MirrorClock;
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

        public void ConfigureMirror(Renderer screen, TextMeshPro title, TextMeshPro status, TextMeshPro clock, int bigScreenStatusSentences)
        {
            m_MirrorScreen = screen; m_MirrorTitle = title; m_MirrorStatus = status; m_MirrorClock = clock; m_StatusSentences = bigScreenStatusSentences;
        }

        public void ConfigureBigBackdrop(GameObject backdrop) => m_BigBackdrop = backdrop;

        void Set(TextMeshPro t, string s)
        {
            if (t == null) return;
            if (t == m_Status)
            {
                t.text = FirstSentences(s, m_StatusSentences);
                if (m_MirrorStatus != null) m_MirrorStatus.text = s;
                return;
            }
            t.text = s;
            if (t == m_Big && m_BigBackdrop != null) m_BigBackdrop.SetActive(!string.IsNullOrEmpty(s));
            if (t == m_Title && m_MirrorTitle != null) m_MirrorTitle.text = s;
            else if (t == m_Clock && m_MirrorClock != null) m_MirrorClock.text = s;
        }

        void Surface(bool padCamera)
        {
            var m = padCamera && m_PadCameraMaterial != null ? m_PadCameraMaterial : m_PlaceholderMaterial;
            if (m_Screen != null) m_Screen.sharedMaterial = m;
            if (m_MirrorScreen != null) m_MirrorScreen.sharedMaterial = m;
        }

        /// <summary>The first <paramref name="count"/> sentences of <paramref name="s"/> (0: all). Sentences end at ". " or a " | " separator.</summary>
        public static string FirstSentences(string s, int count)
        {
            if (string.IsNullOrEmpty(s) || count <= 0) return s;
            int end = 0, from = 0;
            for (var n = 0; n < count; n++)
            {
                var dot = s.IndexOf(". ", from, System.StringComparison.Ordinal);
                var bar = s.IndexOf(" | ", from, System.StringComparison.Ordinal);
                if (dot < 0 && bar < 0) return s;
                if (dot >= 0 && (bar < 0 || dot < bar)) { end = dot + 1; from = dot + 2; }
                else { end = bar; from = bar + 3; }
            }
            return s.Substring(0, end).TrimEnd();
        }
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

        /// <summary>The prototype was accepted and is growing to full size on the pad; the pad feed shows it.</summary>
        public void ShowReceived()
        {
            Surface(true); Diagram(false);
            Set(m_Title, "PROTOTYPE RECEIVED");
            Set(m_Big, "");
            Set(m_Status, "Scaling for launch...");
            Set(m_Clock, "T-00:00:00");
        }

        /// <summary>The launch was aborted: shown while the abort alarm runs.</summary>
        public void ShowAborted()
        {
            Surface(false); Diagram(false);
            Set(m_Title, "LAUNCH ABORTED");
            Set(m_Big, "ABORT");
            Set(m_Status, "Launch aborted. The prototype is back on the bench: correct it and submit again.");
            Set(m_Clock, "T-00:00:00");
        }

        /// <summary>SUBMIT was pressed while the console's wiring is still disconnected (VRRocket_Final's wiring puzzle).</summary>
        public void ShowConsoleFault()
        {
            Surface(false); Diagram(false);
            Set(m_Title, "CONSOLE FAULT");
            Set(m_Big, "OFFLINE");
            Set(m_Status, "Console wiring disconnected. Slide open the hatch between SUBMIT and LAUNCH and reconnect every cable to its socket.");
        }

        /// <summary>The wiring has just been reconnected: the console works again.</summary>
        public void ShowConsoleOnline()
        {
            Surface(false); Diagram(false);
            Set(m_Title, "CONSOLE ONLINE");
            Set(m_Big, "ONLINE");
            Set(m_Status, "Wiring restored. All console systems online: finish the rocket and submit it at the bin.");
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

#if UNITY_EDITOR
        // Stage previews for laying out the screen and the mirror (component context menu, or from editor tooling).
        [ContextMenu("Preview/Title")] public void PreviewTitle() => ShowTitle("VR ROCKET", "Build it. Submit it. Launch it.");
        [ContextMenu("Preview/Assembly")] public void PreviewAssembly() => ShowAssembly(AssemblyState.BuildingAirframe, "Turn around: the kit is on the desk behind you. Fins at the base, flaps at the midpoint with the swept edge up, nose on top, motor nozzle down, then twist the cap until it clicks.");
        [ContextMenu("Preview/Received")] public void PreviewReceived() => ShowReceived();
        [ContextMenu("Preview/Pre-launch")] public void PreviewPreLaunch() => ShowPreLaunch();
        [ContextMenu("Preview/Countdown")] public void PreviewCountdown() => ShowCountdown(7);
        [ContextMenu("Preview/Flight")] public void PreviewFlight() => ShowFlight(812f, 145f, 12.4f);
        [ContextMenu("Preview/Result success")] public void PreviewResultSuccess() => ShowResult(BuildReport.Build(PreviewFlaps(true, true, true), true));
        [ContextMenu("Preview/Result failure")] public void PreviewResultFailure() => ShowResult(BuildReport.Build(PreviewFlaps(true, false, true), false));

        static FlapPlacement[] PreviewFlaps(params bool[] correct)
        {
            var flaps = new FlapPlacement[correct.Length];
            for (var i = 0; i < correct.Length; i++)
                flaps[i] = new FlapPlacement { flapId = i + 1, slotName = "FlapSlot_" + (i + 1), position = FlapPosition.Midpoint, orientation = correct[i] ? FlapOrientation.Up : FlapOrientation.Down };
            return flaps;
        }
#endif

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
