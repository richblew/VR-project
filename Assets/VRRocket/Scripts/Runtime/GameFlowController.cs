using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRRocket
{
    public enum GamePhase { Menu, Assembly, PreLaunch, Countdown, Launch, Result }

    /// <summary>
    /// The experience flow of SPEC section 2 as a working version: Menu, Assembly, PreLaunch (prototype submitted), Countdown, Launch,
    /// Result, and the returns of 8.4. Drives the big screen, the menu panel, the blueprint display and the launch sequence from the
    /// console's real buttons. The wire puzzle of the Pre-launch state is not implemented here (SPEC 9, teammates); Launch is enabled
    /// as soon as the prototype is accepted.
    /// </summary>
    public sealed class GameFlowController : MonoBehaviour
    {
        [SerializeField] RocketAssembly m_Assembly;
        [SerializeField] SubmissionBin m_Bin;
        [SerializeField] LaunchSequence m_Launch;
        [SerializeField] LaunchScreenController m_Screen;
        [SerializeField] MenuPanel m_Menu;
        [SerializeField] BlueprintDisplay m_Blueprint;
        [SerializeField] AudioSource m_Audio;
        [Header("Timing")]
        [SerializeField] float m_CountdownSeconds = 10f;
        [SerializeField] float m_IgnitionAt = 3f;
        [SerializeField] float m_ResultSeconds = 9f;
        [SerializeField] float m_SubmitToPadSeconds = 2.5f;

        public UnityEvent<GamePhase> onPhaseChanged = new UnityEvent<GamePhase>();
        public event Action<GamePhase> PhaseChanged;

        public GamePhase phase { get; private set; } = GamePhase.Menu;
        public BuildReport lastReport { get; private set; }
        public int successes { get; private set; }
        /// <summary>Flights flown (liftoffs), successful or not.</summary>
        public int attempts { get; private set; }
        public float countdownSeconds { get => m_CountdownSeconds; set => m_CountdownSeconds = value; }
        public float resultSeconds { get => m_ResultSeconds; set => m_ResultSeconds = value; }
        public float submitToPadSeconds { get => m_SubmitToPadSeconds; set => m_SubmitToPadSeconds = value; }

        AudioClip m_Beep, m_BeepHigh, m_Chime, m_Buzz;
        Coroutine m_Routine;
        bool m_PadReady;   // the vehicle is on the pad; Launch is refused before that

        public void Configure(RocketAssembly assembly, SubmissionBin bin, LaunchSequence launch, LaunchScreenController screen, MenuPanel menu, BlueprintDisplay blueprint, AudioSource audio)
        {
            m_Assembly = assembly; m_Bin = bin; m_Launch = launch; m_Screen = screen; m_Menu = menu; m_Blueprint = blueprint; m_Audio = audio;
        }

        void Awake()
        {
            m_Beep = ProceduralAudio.Beep(880f, 0.09f);
            m_BeepHigh = ProceduralAudio.Beep(1320f, 0.25f);
            m_Chime = ProceduralAudio.Chime(660f, 990f, 0.35f);
            m_Buzz = ProceduralAudio.Beep(160f, 0.3f, 0.5f);
        }

        void OnEnable()
        {
            if (m_Assembly != null)
            {
                m_Assembly.StateChanged += OnAssemblyState;
                m_Assembly.Submitted += OnSubmitted;
            }
        }

        void OnDisable()
        {
            if (m_Assembly != null)
            {
                m_Assembly.StateChanged -= OnAssemblyState;
                m_Assembly.Submitted -= OnSubmitted;
            }
        }

        void Start()
        {
            EnterMenu(false);
        }

        void SetPhase(GamePhase p)
        {
            phase = p;
            PhaseChanged?.Invoke(p);
            onPhaseChanged.Invoke(p);
        }

        // ---- menu ----

        void EnterMenu(bool afterSuccess)
        {
            if (m_Routine != null) { StopCoroutine(m_Routine); m_Routine = null; }
            SetPhase(GamePhase.Menu);
            m_PadReady = false;
            if (m_Assembly != null) m_Assembly.interactionEnabled = false;
            if (m_Launch != null) m_Launch.ResetPad();
            if (m_Screen != null) m_Screen.ShowTitle(afterSuccess ? "MISSION SUCCESS" : "VR ROCKET", afterSuccess ? "Flight " + attempts + " reached orbit insertion. Press START to build another." : "Build it. Submit it. Launch it.");
            if (m_Blueprint != null) m_Blueprint.ShowIdle();
            if (m_Menu != null) m_Menu.Show(afterSuccess ? "MISSION SUCCESS" : "VR ROCKET", afterSuccess ? "Your rocket flew clean. Build another?" : "Assemble a model rocket by hand, submit it at the console, and watch the full-size launch outside.", afterSuccess ? "BUILD AGAIN" : "START");
        }

        /// <summary>Wired to the menu's START button.</summary>
        public void StartGame()
        {
            if (phase != GamePhase.Menu) return;
            if (m_Audio != null) m_Audio.PlayOneShot(m_Chime);
            EnterAssembly("Build the rocket on the bench. Fins at the base, flaps at the midpoint with the swept edge up, nose on top, motor nozzle down, then twist the cap until it clicks.");
        }

        // ---- assembly ----

        void EnterAssembly(string hint)
        {
            SetPhase(GamePhase.Assembly);
            m_PadReady = false;
            if (m_Menu != null) m_Menu.Hide();
            if (m_Assembly != null) m_Assembly.interactionEnabled = true;
            if (m_Launch != null) m_Launch.ResetPad();
            if (m_Screen != null) m_Screen.ShowAssembly(m_Assembly != null ? m_Assembly.State : AssemblyState.BuildingAirframe, hint);
            if (m_Blueprint != null && m_Assembly != null) m_Blueprint.ShowState(m_Assembly.machine);
        }

        void OnAssemblyState(AssemblyState state)
        {
            if (phase != GamePhase.Assembly) return;
            if (m_Blueprint != null && m_Assembly != null) m_Blueprint.ShowState(m_Assembly.machine);
            if (m_Screen == null) return;
            switch (state)
            {
                case AssemblyState.AirframeComplete: m_Screen.ShowAssembly(state, "Airframe complete. Insert the motor, nozzle down."); break;
                case AssemblyState.MotorFitted: m_Screen.ShowAssembly(state, "Motor fitted. Seat the cap and twist it clockwise until it clicks."); break;
                case AssemblyState.PrototypeComplete: m_Screen.ShowAssembly(state, "Prototype complete. Carry it to the submission bin and press SUBMIT."); break;
                default: m_Screen.ShowAssembly(state, "Keep building. Three fins, three flaps, the nose cone."); break;
            }
        }

        // ---- submission and pre-launch ----

        void OnSubmitted(BuildReport report)
        {
            lastReport = report;
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(SubmissionRoutine());
        }

        IEnumerator SubmissionRoutine()
        {
            SetPhase(GamePhase.PreLaunch);
            m_PadReady = false;
            if (m_Assembly != null) m_Assembly.interactionEnabled = false;
            if (m_Audio != null) m_Audio.PlayOneShot(m_Chime);
            if (m_Screen != null) m_Screen.ShowTitle("PROTOTYPE RECEIVED", "Scaling for launch...");
            yield return new WaitForSeconds(m_SubmitToPadSeconds);
            if (m_Launch != null) m_Launch.PlaceVehicleOnPad();
            m_PadReady = true;
            if (m_Screen != null) m_Screen.ShowPreLaunch();
            if (m_Blueprint != null) m_Blueprint.ShowText("Rocket on the pad.\nLAUNCH or ABORT at the console.");
            m_Routine = null;
        }

        /// <summary>Wired to the console's green Launch button. Refused until the vehicle is on the pad.</summary>
        public void Launch()
        {
            if (phase != GamePhase.PreLaunch || !m_PadReady) { if (m_Audio != null) m_Audio.PlayOneShot(m_Buzz); return; }
            if (m_Routine != null) StopCoroutine(m_Routine);
            m_Routine = StartCoroutine(CountdownRoutine());
        }

        /// <summary>Wired to the console's red Abort button. Works from PreLaunch and Countdown; returns the prototype editable.</summary>
        public void Abort()
        {
            if (phase != GamePhase.PreLaunch && phase != GamePhase.Countdown) { if (m_Audio != null) m_Audio.PlayOneShot(m_Buzz); return; }
            if (m_Routine != null) { StopCoroutine(m_Routine); m_Routine = null; }
            if (m_Launch != null) m_Launch.AbortCountdown();
            if (m_Audio != null) m_Audio.PlayOneShot(m_Buzz);
            if (m_Bin != null) m_Bin.PlayOpen();
            if (m_Assembly != null) m_Assembly.ReturnPrototype(ReturnMode.Editable, null);
            EnterAssembly("Aborted. The prototype is back on the bench. Correct it and submit again.");
        }

        IEnumerator CountdownRoutine()
        {
            SetPhase(GamePhase.Countdown);
            var seconds = Mathf.Max(1, Mathf.RoundToInt(m_CountdownSeconds));
            var ignited = false;
            for (var t = seconds; t > 0; t--)
            {
                if (m_Screen != null) m_Screen.ShowCountdown(t);
                if (m_Audio != null) m_Audio.PlayOneShot(m_Beep);
                if (!ignited && t <= m_IgnitionAt && m_Launch != null) { m_Launch.Ignite(); ignited = true; }
                yield return new WaitForSeconds(1f);
            }
            if (!ignited && m_Launch != null) m_Launch.Ignite();
            if (m_Audio != null) m_Audio.PlayOneShot(m_BeepHigh);
            attempts++;   // a flight, not a submission: aborted submissions do not count
            SetPhase(GamePhase.Launch);
            var outcome = lastReport != null ? lastReport.outcome : LaunchOutcome.Success;
            var done = false;
            if (m_Launch != null) m_Launch.Liftoff(outcome, () => done = true);
            else done = true;
            var t0 = Time.time;
            while (!done)
            {
                if (m_Screen != null && m_Launch != null) m_Screen.ShowFlight(m_Launch.altitude, m_Launch.speed, Time.time - t0);
                yield return null;
            }
            m_Routine = StartCoroutine(ResultRoutine(outcome));
        }

        IEnumerator ResultRoutine(LaunchOutcome outcome)
        {
            SetPhase(GamePhase.Result);
            if (m_Screen != null) m_Screen.ShowResult(lastReport);
            if (m_Audio != null) m_Audio.PlayOneShot(outcome == LaunchOutcome.Success ? m_Chime : m_Buzz);
            yield return new WaitForSeconds(m_ResultSeconds);
            if (outcome == LaunchOutcome.Success)
            {
                successes++;
                if (m_Assembly != null) m_Assembly.BeginNewBuild();
                EnterMenu(true);
            }
            else
            {
                if (m_Bin != null) m_Bin.PlayOpen();
                if (m_Assembly != null)
                {
                    m_Assembly.ReturnPrototype(ReturnMode.InspectOnly, m_Bin != null ? m_Bin.returnSpot : null);
                    m_Assembly.BeginNewBuild();
                }
                EnterAssembly("Launch failed: " + (lastReport != null ? string.Join(", ", lastReport.failedComponents) : "") + ". Examine the returned prototype, then build a new one.");
            }
            m_Routine = null;
        }
    }
}
