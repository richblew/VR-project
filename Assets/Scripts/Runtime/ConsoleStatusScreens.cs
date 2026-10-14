using System.Text;
using TMPro;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The two side monitors on the console (VRRocket_Final), either side of the wide mirror monitor.
    /// <list type="bullet">
    /// <item>Left, GO / NO-GO: a launch readiness poll of the build and the pad. It counts fitted parts only: it never says whether
    /// the flaps are the right way round or the cap is locked, because finding that out is the game.</item>
    /// <item>Right, FLIGHT DATA: range status and the mission log while building, the countdown, live altitude and speed in
    /// flight, then the flight's result.</item>
    /// </list>
    /// </summary>
    public sealed class ConsoleStatusScreens : MonoBehaviour
    {
        [SerializeField] GameFlowController m_Flow;
        [SerializeField] RocketAssembly m_Assembly;
        [SerializeField] LaunchSequence m_Launch;
        [SerializeField] TextMeshPro m_PollText;
        [SerializeField] TextMeshPro m_PollVerdict;
        [SerializeField] TextMeshPro m_DataTitle;
        [SerializeField] TextMeshPro m_DataText;
        [SerializeField, Tooltip("Seconds between text updates")] float m_Refresh = 0.1f;

        const string k_Go = "<color=#4CFF7A>GO</color>";
        const string k_NoGo = "<color=#FF5A4C>NO-GO</color>";
        const string k_Standby = "<color=#FFC14C>STBY</color>";

        readonly StringBuilder m_Sb = new StringBuilder(256);
        float m_Next, m_PhaseStart, m_MaxAltitude;
        string m_LastFlight = "NONE";

        public string pollText => m_PollText != null ? m_PollText.text : "";
        public string pollVerdict => m_PollVerdict != null ? m_PollVerdict.text : "";
        public string dataTitle => m_DataTitle != null ? m_DataTitle.text : "";
        public string dataText => m_DataText != null ? m_DataText.text : "";

        public void Configure(GameFlowController flow, RocketAssembly assembly, LaunchSequence launch, TextMeshPro pollText, TextMeshPro pollVerdict, TextMeshPro dataTitle, TextMeshPro dataText)
        {
            m_Flow = flow; m_Assembly = assembly; m_Launch = launch;
            m_PollText = pollText; m_PollVerdict = pollVerdict; m_DataTitle = dataTitle; m_DataText = dataText;
        }

        void OnEnable()
        {
            if (m_Flow == null) return;
            m_Flow.PhaseChanged += OnPhaseChanged;
            m_Flow.MiniGameChanged += Refresh;
        }

        void OnDisable()
        {
            if (m_Flow == null) return;
            m_Flow.PhaseChanged -= OnPhaseChanged;
            m_Flow.MiniGameChanged -= Refresh;
        }

        void OnPhaseChanged(GamePhase phase)
        {
            m_PhaseStart = Time.time;
            if (phase == GamePhase.Launch) m_MaxAltitude = 0f;
            if (phase == GamePhase.Result) m_LastFlight = Outcome(false);
            m_Next = 0f;
        }

        void Update()
        {
            if (m_Launch != null && m_Launch.isRunning) m_MaxAltitude = Mathf.Max(m_MaxAltitude, m_Launch.altitude);
            if (Time.time < m_Next) return;
            m_Next = Time.time + m_Refresh;
            Refresh();
        }

        /// <summary>Writes both screens now.</summary>
        public void Refresh()
        {
            var phase = m_Flow != null ? m_Flow.phase : GamePhase.Menu;
            WritePoll(phase);
            WriteData(phase);
        }

        void WritePoll(GamePhase phase)
        {
            if (m_PollText == null) return;
            var m = m_Assembly != null ? m_Assembly.machine : null;
            var idle = phase == GamePhase.Menu || phase == GamePhase.Intro;
            int fins = m != null ? m.finsAttached : 0, flaps = m != null ? m.flapsAttached : 0;
            bool nose = m != null && m.noseAttached, motor = m != null && m.motorAttached, cap = m != null && m.capAttached;
            var onPad = phase == GamePhase.Countdown || phase == GamePhase.Launch || (phase == GamePhase.PreLaunch && m_Launch != null && !m_Launch.isGrowing);

            m_Sb.Clear();
            var wiringRequired = m_Flow != null && m_Flow.requireMiniGame;
            if (wiringRequired) Row("WIRING", m_Flow.miniGameSolved ? k_Go : idle ? k_Standby : k_NoGo);
            Row("FINS " + fins + "/3", idle ? k_Standby : fins >= 3 ? k_Go : k_NoGo);
            Row("FLAPS " + flaps + "/3", idle ? k_Standby : flaps >= 3 ? k_Go : k_NoGo);
            Row("NOSE CONE", idle ? k_Standby : nose ? k_Go : k_NoGo);
            Row("BATTERY", idle ? k_Standby : motor ? k_Go : k_NoGo);
            Row("BATTERY CAP", idle ? k_Standby : cap ? k_Go : k_NoGo);
            Row(onPad ? "PAD VEHICLE" : "PAD EMPTY", idle || phase == GamePhase.Result ? k_Standby : onPad ? k_Go : k_NoGo);
            m_PollText.text = m_Sb.ToString();

            var complete = fins >= 3 && flaps >= 3 && nose && motor && cap;
            string verdict;
            switch (phase)
            {
                case GamePhase.Assembly:
                    verdict = m_Flow != null && !m_Flow.consoleOnline ? "<color=#FF5A4C>CONSOLE OFFLINE</color>  RESTORE WIRING"
                        : complete ? "<color=#FFC14C>READY TO SUBMIT</color>" : "<color=#FF5A4C>HOLD</color>  BUILD IN PROGRESS";
                    break;
                case GamePhase.PreLaunch: verdict = onPad ? "<color=#4CFF7A>ALL STATIONS GO</color>" : "<color=#FFC14C>HOLD</color>  VEHICLE TO PAD"; break;
                case GamePhase.Countdown: verdict = "<color=#4CFF7A>GO FOR LAUNCH</color>"; break;
                case GamePhase.Launch: verdict = "<color=#4CFF7A>LIFTOFF</color>"; break;
                case GamePhase.Result: verdict = "<color=#FFC14C>POLL CLOSED</color>"; break;
                default: verdict = "<color=#FFC14C>STANDBY</color>"; break;
            }
            if (m_PollVerdict != null) m_PollVerdict.text = verdict;
        }

        void Row(string name, string state)
        {
            m_Sb.Append(name.PadRight(12)).Append(state).Append('\n');
        }

        void WriteData(GamePhase phase)
        {
            if (m_DataText == null) return;
            var attempts = m_Flow != null ? m_Flow.attempts : 0;
            var successes = m_Flow != null ? m_Flow.successes : 0;
            m_Sb.Clear();
            string title;
            switch (phase)
            {
                case GamePhase.Countdown:
                {
                    title = "COUNTDOWN";
                    var left = Mathf.Max(0, Mathf.CeilToInt((m_Flow != null ? m_Flow.countdownSeconds : 0f) - (Time.time - m_PhaseStart)));
                    m_Sb.Append("<size=180%>T-00:").Append(left.ToString("00")).Append("</size>\n\n");
                    m_Sb.Append("PAD 01      ").Append(k_Go).Append('\n');
                    m_Sb.Append("RANGE       ").Append(k_Go).Append('\n');
                    m_Sb.Append("ABORT       AVAILABLE");
                    break;
                }
                case GamePhase.Launch:
                {
                    title = "FLIGHT DATA";
                    var alt = m_Launch != null ? m_Launch.altitude : 0f;
                    var vel = m_Launch != null ? m_Launch.speed : 0f;
                    m_Sb.Append("T+   ").Append((Time.time - m_PhaseStart).ToString("0.0")).Append(" s\n");
                    m_Sb.Append("ALT  ").Append(alt.ToString("0")).Append(" m\n");
                    m_Sb.Append("VEL  ").Append(vel.ToString("0")).Append(" m/s\n");
                    m_Sb.Append("MAX  ").Append(m_MaxAltitude.ToString("0")).Append(" m");
                    break;
                }
                case GamePhase.Result:
                    title = "FLIGHT " + attempts + " RESULT";
                    m_Sb.Append(Outcome(true)).Append('\n');
                    if (m_Flow != null && m_Flow.lastReport != null && m_Flow.lastReport.outcome != LaunchOutcome.Success) m_Sb.Append(m_Flow.lastReport.FlightDetail).Append('\n');
                    m_Sb.Append('\n');
                    m_Sb.Append("MAX ALT   ").Append(m_MaxAltitude.ToString("0")).Append(" m\n");
                    m_Sb.Append("FLIGHTS   ").Append(attempts).Append('\n');
                    m_Sb.Append("SUCCESSES ").Append(successes);
                    break;
                default:
                    title = "RANGE STATUS";
                    m_Sb.Append("WEATHER    ").Append(k_Go).Append('\n');
                    m_Sb.Append("WIND       4 KT\n");
                    m_Sb.Append("RANGE      CLEAR\n");
                    m_Sb.Append("PAD 01     ").Append(phase == GamePhase.PreLaunch ? "VEHICLE" : "EMPTY").Append("\n\n");
                    m_Sb.Append("FLIGHTS    ").Append(attempts).Append('\n');
                    m_Sb.Append("SUCCESSES  ").Append(successes).Append('\n');
                    m_Sb.Append("LAST       ").Append(m_LastFlight);
                    break;
            }
            m_DataText.text = m_Sb.ToString();
            if (m_DataTitle != null) m_DataTitle.text = title;
        }

        /// <summary>The last flight's outcome, coloured for the screen or plain for the mission log.</summary>
        string Outcome(bool coloured)
        {
            var report = m_Flow != null ? m_Flow.lastReport : null;
            if (report == null) return "NONE";
            switch (report.outcome)
            {
                case LaunchOutcome.Success: return coloured ? "<color=#4CFF7A>MISSION SUCCESS</color>" : "SUCCESS";
                case LaunchOutcome.UnstableFlight: return coloured ? "<color=#FF5A4C>UNSTABLE FLIGHT</color>" : "UNSTABLE";
                case LaunchOutcome.MotorRetentionLoss: return coloured ? "<color=#FF5A4C>BATTERY RETENTION LOSS</color>" : "BATTERY LOSS";
                default: return report.outcome.ToString().ToUpperInvariant();
            }
        }
    }
}
