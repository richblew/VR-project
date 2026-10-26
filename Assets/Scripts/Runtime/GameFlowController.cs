using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace VRRocket
{
    public enum GamePhase { Menu, Assembly, PreLaunch, Countdown, Launch, Result, Intro }   // Intro added last: values stay stable

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
        [SerializeField, Tooltip("Optional. The room's intro, played on demand after START (VRRocket_Final). Empty, or an intro that plays by itself, means START goes straight to building.")]
        TransmissionBridge m_Intro;
        [SerializeField, Tooltip("Optional. Flashes the room's side screens and sounds a siren when the launch is aborted (VRRocket_Final).")]
        AbortAlarm m_AbortAlarm;
        [SerializeField, Tooltip("Where a rocket that failed its launch comes back for inspection (VRRocket_Final: the side desk under the moon-landing painting). Empty: the bin's return spot.")]
        Transform m_FailedRocketSpot;
        [SerializeField] AudioSource m_Audio;
        [Header("Timing")]
        [SerializeField] float m_CountdownSeconds = 10f;
        [SerializeField] float m_IgnitionAt = 3f;
        [SerializeField] float m_ResultSeconds = 9f;
        [SerializeField] float m_SubmitToPadSeconds = 2.5f;
        [Header("Text")]
        [SerializeField, TextArea, Tooltip("Hint shown when assembly starts. The big screen shows its first sentence, the console mirror all of it.")]
        string m_AssemblyHint = "Build the rocket on the bench. Fins at the base, flaps at the midpoint with the swept edge up, nose on top, battery + end up, then twist the cap until it clicks.";

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
        public string assemblyHint { get => m_AssemblyHint; set => m_AssemblyHint = value; }
        public Transform failedRocketSpot { get => m_FailedRocketSpot; set => m_FailedRocketSpot = value; }

        /// <summary>A console button (or the build) was refused: the hand that pressed gets a double buzz (ButtonPressHaptics).</summary>
        public event Action Refused;
        /// <summary>A refusal's title and explanation, for the console's status monitor.</summary>
        public event Action<string, string> Notice;

        /// <summary>
        /// Every refused press answers the same way: a buzz, a double vibration on the hand that pressed, and (when a title is
        /// given) the reason on the status monitor and, while building or on the pad, on the big screen and console monitor for a
        /// few seconds. On the menu and in the intro the screens are not shown, so it is the buzz and the vibration only.
        /// </summary>
        public void Refuse(string title, string big, string detail)
        {
            if (m_Audio != null) m_Audio.PlayOneShot(m_Buzz);
            Refused?.Invoke();
            if (string.IsNullOrEmpty(title)) return;
            Notice?.Invoke(title, detail);
            if (m_Screen != null && (phase == GamePhase.Assembly || phase == GamePhase.PreLaunch))
                ShowMessage(() => m_Screen.ShowRefusal(title, big, detail));
        }

        // The wiring mini game (Yi-Xuan's cable puzzle inside the console, VRRocket_Final). Rules agreed with Praise:
        // - it can be solved at any time in a play, even before the rocket is built;
        // - until it is solved the console is offline: SUBMIT refuses the rocket, so there is no launch or abort either;
        // - once solved it stays solved: after a failed launch and a new kit, no need to solve it again;
        // - a new play (back to the menu after a successful flight) needs it solved again: ResetMiniGame() takes the console
        //   offline and the puzzle (CablePuzzle.ConsoleWiring) unplugs, puts the ends back and shuffles the sockets.
        [SerializeField, Tooltip("The console stays offline until MiniGameSolved() is called (VRRocket_Final's wiring puzzle)")]
        bool m_RequireMiniGame;
        bool m_MiniGameSolved;

        /// <summary>True once the wiring puzzle is solved.</summary>
        public bool miniGameSolved => m_MiniGameSolved;
        /// <summary>The console's buttons work: no puzzle is required, or it is solved.</summary>
        public bool consoleOnline => !m_RequireMiniGame || m_MiniGameSolved;
        public bool requireMiniGame { get => m_RequireMiniGame; set => m_RequireMiniGame = value; }
        public event Action MiniGameChanged;

        /// <summary>Called when the player solves the wiring puzzle (CablePairManager.onAllConnected in VRRocket_Final).</summary>
        public void MiniGameSolved()
        {
            if (m_MiniGameSolved) return;
            m_MiniGameSolved = true;
            if (m_Audio != null) m_Audio.PlayOneShot(m_Chime);
            MiniGameChanged?.Invoke();
            if (phase == GamePhase.Assembly && m_Screen != null) ShowMessage(m_Screen.ShowConsoleOnline);
        }

        /// <summary>SUBMIT was refused because the console is offline: the screens say why for a few seconds.</summary>
        public void ReportConsoleOffline()
        {
            if (m_Audio != null) m_Audio.PlayOneShot(m_Buzz);
            Refused?.Invoke();
            if (phase != GamePhase.Assembly) return;
            Notice?.Invoke("CONSOLE OFFLINE", "Reconnect the wiring under the hatch between SUBMIT and LAUNCH.");
            if (m_Screen != null) ShowMessage(m_Screen.ShowConsoleFault);
        }

        [SerializeField, Tooltip("Seconds a console message (fault, online) stays on the screens before the build text returns")]
        float m_MessageSeconds = 4f;
        Coroutine m_Message;

        void ShowMessage(Action show)
        {
            if (m_Message != null) StopCoroutine(m_Message);
            show();
            m_Message = StartCoroutine(BackToBuildText());
        }

        IEnumerator BackToBuildText()
        {
            yield return new WaitForSeconds(m_MessageSeconds);
            m_Message = null;
            if (phase == GamePhase.PreLaunch && m_Screen != null) { if (m_PadReady) m_Screen.ShowPreLaunch(); else m_Screen.ShowReceived(); }
            else if (m_Assembly != null) OnAssemblyState(m_Assembly.State);
        }

        /// <summary>Takes the console offline again until the puzzle is solved anew.</summary>
        public void ResetMiniGame()
        {
            if (!m_MiniGameSolved) return;
            m_MiniGameSolved = false;
            MiniGameChanged?.Invoke();
        }

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
                m_Assembly.PartAttached += OnPartCountChanged;
                m_Assembly.PartRemoved += OnPartCountChanged;
                m_Assembly.Submitted += OnSubmitted;
            }
            AssemblyEvents.PartRefused += OnPartRefused;
        }

        void OnDisable()
        {
            if (m_Assembly != null)
            {
                m_Assembly.StateChanged -= OnAssemblyState;
                m_Assembly.PartAttached -= OnPartCountChanged;
                m_Assembly.PartRemoved -= OnPartCountChanged;
                m_Assembly.Submitted -= OnSubmitted;
            }
            AssemblyEvents.PartRefused -= OnPartRefused;
        }

        /// <summary>A part was brought to a point that will not take it yet: the cap before the battery.</summary>
        void OnPartRefused(RocketPart part, AttachPoint point)
        {
            if (phase != GamePhase.Assembly || part == null || part.partType != PartType.MotorCap) return;
            Refuse("BATTERY FIRST", "CAP REFUSED", "Fit the battery first, + end up. Then the cap goes on.");
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
            if (afterSuccess) ResetMiniGame();   // a new play: the wiring is undone and must be connected again (ConsoleWiring)
            if (m_Assembly != null) m_Assembly.interactionEnabled = false;
            if (m_Launch != null) m_Launch.ResetPad();
            // the menu's words are edited per state on the MenuPanel, in the Inspector; a big screen showing during the menu repeats its title
            var state = afterSuccess ? MenuState.AfterSuccess : MenuState.FirstPlay;
            var words = m_Menu != null ? m_Menu.Words(state) : new MenuWords();
            if (m_Screen != null) m_Screen.ShowTitle(words.title, words.kicker);
            if (m_Blueprint != null) m_Blueprint.ShowIdle();
            if (m_Menu != null) m_Menu.Show(state, introOnDemand);
        }

        /// <summary>True when the room's intro waits to be started from the menu (VRRocket_Final).</summary>
        public bool introOnDemand => m_Intro != null && m_Intro.canPlayOnDemand;

        /// <summary>Wired to the menu's START button (START WITH INTRO when the intro plays on demand).</summary>
        public void StartGame()
        {
            if (phase != GamePhase.Menu) return;
            if (m_Audio != null) m_Audio.PlayOneShot(m_Chime);
            if (introOnDemand) EnterIntro();
            else EnterAssembly(m_AssemblyHint);
        }

        /// <summary>Wired to the menu's SKIP INTRO button: straight to building.</summary>
        public void StartWithoutIntro()
        {
            if (phase != GamePhase.Menu) return;
            if (m_Audio != null) m_Audio.PlayOneShot(m_Chime);
            if (m_Intro != null) m_Intro.Stop();
            EnterAssembly(m_AssemblyHint);
        }

        /// <summary>Ends a running intro early and starts building. Not wired today (the choice is made on the menu).</summary>
        public void SkipIntro()
        {
            if (phase != GamePhase.Intro) return;
            if (m_Intro != null) m_Intro.Stop();
            EnterAssembly(m_AssemblyHint);
        }

        void EnterIntro()
        {
            SetPhase(GamePhase.Intro);
            m_PadReady = false;
            if (m_Menu != null) m_Menu.Hide();
            if (m_Assembly != null) m_Assembly.interactionEnabled = false;
            if (m_Launch != null) m_Launch.ResetPad();
            m_Intro.Play(() => { if (phase == GamePhase.Intro) EnterAssembly(m_AssemblyHint); });
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

        // The step monitor counts fins and flaps within a stage, so it follows every part, not only stage changes.
        void OnPartCountChanged()
        {
            if (phase == GamePhase.Assembly && m_Blueprint != null && m_Assembly != null) m_Blueprint.ShowState(m_Assembly.machine);
        }

        void OnAssemblyState(AssemblyState state)
        {
            if (phase != GamePhase.Assembly) return;
            if (m_Blueprint != null && m_Assembly != null) m_Blueprint.ShowState(m_Assembly.machine);
            if (m_Screen == null) return;
            switch (state)
            {
                case AssemblyState.AirframeComplete: m_Screen.ShowAssembly(state, "Airframe complete. Insert the battery, + end up."); break;
                case AssemblyState.MotorFitted: m_Screen.ShowAssembly(state, "Battery fitted. Seat the cap and twist it clockwise until it clicks."); break;
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
            // the prototype appears on the pad at its own size, built exactly as the player built it, and grows to full size
            if (m_Launch != null) { m_Launch.MatchBuild(lastReport); m_Launch.PlaceVehicleOnPad(m_SubmitToPadSeconds); }
            if (m_Screen != null) m_Screen.ShowReceived();
            yield return new WaitForSeconds(m_SubmitToPadSeconds);
            m_PadReady = true;
            if (m_Screen != null) m_Screen.ShowPreLaunch();
            if (m_Blueprint != null) m_Blueprint.ShowText("Rocket on the pad.\nLAUNCH or ABORT at the console.");
            m_Routine = null;
        }

        /// <summary>Wired to the console's green Launch button. Starts the countdown once the vehicle is on the pad; otherwise says why not.</summary>
        public void Launch()
        {
            if (phase == GamePhase.PreLaunch && m_PadReady)
            {
                if (m_Routine != null) StopCoroutine(m_Routine);
                m_Routine = StartCoroutine(CountdownRoutine());
                return;
            }
            switch (phase)
            {
                case GamePhase.Assembly:
                    if (!consoleOnline) ReportConsoleOffline();
                    else Refuse("NO ROCKET ON THE PAD", "SUBMIT FIRST", "Submit a finished rocket first: put it in the bin, let go, press SUBMIT.");
                    break;
                case GamePhase.PreLaunch:
                    Refuse("STAND BY", "READYING", "The rocket is still being readied. Press LAUNCH once it stands at full size.");
                    break;
                case GamePhase.Countdown:
                case GamePhase.Launch:
                    Refuse("LAUNCH IN PROGRESS", "ALREADY LAUNCHING", "The countdown has already started.");
                    break;
                case GamePhase.Result:
                    Refuse("FLIGHT OVER", "STAND BY", "Wait for the next build.");
                    break;
                default:
                    Refuse(null, null, null);   // the menu or the intro: nothing to launch, and the screens are not up
                    break;
            }
        }

        /// <summary>Wired to the console's red Abort button. Works from PreLaunch and Countdown, returning the prototype editable; otherwise says why not.</summary>
        public void Abort()
        {
            if (phase != GamePhase.PreLaunch && phase != GamePhase.Countdown)
            {
                switch (phase)
                {
                    case GamePhase.Assembly:
                        if (!consoleOnline) ReportConsoleOffline();
                        else Refuse("NOTHING TO ABORT", "NO LAUNCH", "Nothing is on the pad. ABORT stops a launch before liftoff.");
                        break;
                    case GamePhase.Launch:
                        Refuse("TOO LATE TO ABORT", "IN FLIGHT", "The rocket has left the pad.");
                        break;
                    case GamePhase.Result:
                        Refuse("TOO LATE TO ABORT", "FLIGHT OVER", "The flight is over. Wait for the next build.");
                        break;
                    default:
                        Refuse(null, null, null);
                        break;
                }
                return;
            }
            if (m_Routine != null) { StopCoroutine(m_Routine); m_Routine = null; }
            if (m_Launch != null) m_Launch.AbortCountdown();
            if (m_Audio != null) m_Audio.PlayOneShot(m_Buzz);
            if (m_Bin != null) m_Bin.PlayOpen();
            if (m_Assembly != null) m_Assembly.ReturnPrototype(ReturnMode.Editable, null);
            EnterAssembly("Aborted. The prototype is back on the bench. Correct it and submit again.");
            if (m_AbortAlarm != null)
            {
                m_AbortAlarm.Play();
                if (m_Screen != null) { m_Screen.ShowAborted(); m_Routine = StartCoroutine(AfterAbortAlarm()); }
            }
        }

        /// <summary>The big screen says LAUNCH ABORTED while the alarm runs, then shows the build again.</summary>
        IEnumerator AfterAbortAlarm()
        {
            yield return new WaitForSeconds(m_AbortAlarm.seconds);
            m_Routine = null;
            if (phase == GamePhase.Assembly && m_Screen != null && m_Assembly != null)
                m_Screen.ShowAssembly(m_Assembly.State, "Aborted. The prototype is back on the bench. Correct it and submit again.");
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
            if (m_Launch != null) m_Launch.Liftoff(outcome, () => done = true, lastReport != null ? lastReport.wrongFlapCount : 0);
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
            if (outcome == LaunchOutcome.Success) successes++;   // before the result shows, so the result screens count this flight
            SetPhase(GamePhase.Result);
            if (m_Screen != null) m_Screen.ShowResult(lastReport);
            if (m_Audio != null) m_Audio.PlayOneShot(outcome == LaunchOutcome.Success ? m_Chime : m_Buzz);
            yield return new WaitForSeconds(m_ResultSeconds);
            if (outcome == LaunchOutcome.Success)
            {
                if (m_Assembly != null) m_Assembly.BeginNewBuild();
                EnterMenu(true);
            }
            else
            {
                if (m_Bin != null) m_Bin.PlayOpen();
                if (m_Assembly != null)
                {
                    m_Assembly.ReturnPrototype(ReturnMode.InspectOnly, m_FailedRocketSpot != null ? m_FailedRocketSpot : m_Bin != null ? m_Bin.returnSpot : null);
                    m_Assembly.BeginNewBuild();
                }
                EnterAssembly("Launch failed: " + (lastReport != null ? string.Join(", ", BuildReport.DisplayNames(lastReport.failedComponents)) : "") + ". Examine the failed rocket on the side desk under the painting, then build a new one.");
            }
            m_Routine = null;
        }
    }
}
