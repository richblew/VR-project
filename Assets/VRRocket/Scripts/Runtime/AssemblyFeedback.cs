using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace VRRocket
{
    /// <summary>
    /// Turns assembly events into audio and haptics using the <see cref="FeedbackLibrary"/> (SPEC.md section 6, plus the immersion set).
    /// Glow is driven by the attach points and parts themselves. Sounds are 3D and play from the attach point or the part.
    /// Haptics go through the controller's <see cref="HapticImpulsePlayer"/> with amplitude, duration and frequency, which the
    /// Quest 3 Touch Plus voice-coil actuators honour. Rejected placements, break-aways and an early let-go of the cap get nothing.
    /// </summary>
    public sealed class AssemblyFeedback : MonoBehaviour
    {
        [SerializeField] FeedbackLibrary m_Library;
        [SerializeField, Range(1, 8)] int m_AudioSourceCount = 4;
        [SerializeField, Tooltip("Impact speed (m/s) that plays at full volume.")] float m_ImpactFullVolumeSpeed = 2.5f;

        AudioSource[] m_Sources;
        int m_NextSource;
        readonly List<NearFarInteractor> m_Hands = new List<NearFarInteractor>(2);
        readonly Dictionary<IXRInteractor, HapticImpulsePlayer> m_Players = new Dictionary<IXRInteractor, HapticImpulsePlayer>();
        RocketAssembly m_Assembly;

        public static AssemblyFeedback instance { get; private set; }

        public FeedbackLibrary library
        {
            get => m_Library;
            set => m_Library = value;
        }

        void Awake()
        {
            instance = this;
            m_Assembly = GetComponent<RocketAssembly>();
            m_Sources = new AudioSource[m_AudioSourceCount];
            for (var i = 0; i < m_AudioSourceCount; i++)
            {
                var go = new GameObject("AudioSource_" + i);
                go.transform.SetParent(transform, false);
                var src = go.AddComponent<AudioSource>();
                src.playOnAwake = false;
                src.spatialBlend = 1f;
                src.rolloffMode = AudioRolloffMode.Linear;
                src.minDistance = 0.2f;
                src.maxDistance = 6f;
                m_Sources[i] = src;
            }
        }

        void OnEnable()
        {
            if (instance == null) instance = this;
            AssemblyEvents.GuideEngaged += OnGuideEngaged;
            AssemblyEvents.PartSeated += OnPartSeated;
            AssemblyEvents.PartRemoved += OnPartRemoved;
            AssemblyEvents.PartRespawned += OnPartRespawned;
            AssemblyEvents.CapDetent += OnCapDetent;
            AssemblyEvents.CapLocked += OnCapLocked;
            AssemblyEvents.PrototypeComplete += OnPrototypeComplete;
            AssemblyEvents.PartImpact += OnPartImpact;
            AssemblyEvents.PartHovered += OnPartHovered;
            AssemblyEvents.PartGrabbed += OnPartGrabbed;
            AssemblyEvents.PartReleased += OnPartReleased;
            AssemblyEvents.GuideSlideTick += OnGuideSlideTick;
            AssemblyEvents.TwistTexture += OnTwistTexture;
            AssemblyEvents.KitSpawned += OnKitSpawned;
            if (m_Assembly != null) m_Assembly.Submitted += OnSubmitted;
        }

        void OnDisable()
        {
            AssemblyEvents.GuideEngaged -= OnGuideEngaged;
            AssemblyEvents.PartSeated -= OnPartSeated;
            AssemblyEvents.PartRemoved -= OnPartRemoved;
            AssemblyEvents.PartRespawned -= OnPartRespawned;
            AssemblyEvents.CapDetent -= OnCapDetent;
            AssemblyEvents.CapLocked -= OnCapLocked;
            AssemblyEvents.PrototypeComplete -= OnPrototypeComplete;
            AssemblyEvents.PartImpact -= OnPartImpact;
            AssemblyEvents.PartHovered -= OnPartHovered;
            AssemblyEvents.PartGrabbed -= OnPartGrabbed;
            AssemblyEvents.PartReleased -= OnPartReleased;
            AssemblyEvents.GuideSlideTick -= OnGuideSlideTick;
            AssemblyEvents.TwistTexture -= OnTwistTexture;
            AssemblyEvents.KitSpawned -= OnKitSpawned;
            if (m_Assembly != null) m_Assembly.Submitted -= OnSubmitted;
            if (instance == this) instance = null;
        }

        // ---- SPEC 6 events ----

        void OnGuideEngaged(RocketPart part, AttachPoint point)
        {
            if (m_Library == null) return;
            PlayAt(m_Library.guideEngage, point.transform.position);
            SendHaptic(part, m_Library.hapticGuideEngage);
        }

        void OnPartSeated(RocketPart part, AttachPoint point)
        {
            if (m_Library == null) return;
            if (part.partType == PartType.BodyTube)
            {
                // the rocket snapping back into the stand
                PlayAt(m_Library.seat, point.transform.position);
                SendHaptic(part, m_Library.hapticStandCapture);
                return;
            }
            PlayAt(m_Library.SeatClipFor(part.partType), point.transform.position);
            SendHaptic(part, m_Library.SeatHapticFor(part.partType));
        }

        void OnPartRemoved(RocketPart part, AttachPoint point)
        {
            if (m_Library == null) return;
            if (part.partType == PartType.BodyTube)
            {
                // lifting the finished rocket out of the stand: the clamp lets go
                SendHaptic(part, m_Library.hapticStandLiftOut);
                return;
            }
            PlayAt(m_Library.unseat, point.transform.position);
            SendHaptic(part, m_Library.hapticPartRemoved);
        }

        void OnPartRespawned(RocketPart part)
        {
            if (m_Library == null) return;
            PlayAt(m_Library.respawnPop, part.transform.position);
        }

        void OnCapDetent(RocketPart part)
        {
            if (m_Library == null) return;
            PlayAt(m_Library.capDetent, part.transform.position);
            SendHaptic(part, m_Library.hapticCapDetent);
        }

        void OnCapLocked(RocketPart part)
        {
            if (m_Library == null) return;
            PlayAt(m_Library.capLock, part.transform.position);
            SendHaptic(part, m_Library.hapticCapLock);
        }

        void OnPrototypeComplete(RocketPart tube)
        {
            if (m_Library == null) return;
            PlayAt(m_Library.standRelease, tube != null ? tube.transform.position : transform.position);
            SendHapticBothHands(m_Library.hapticPrototypeComplete);
        }

        void OnPartImpact(RocketPart part, float speed, bool hitPart)
        {
            if (m_Library == null) return;
            var volume = Mathf.Clamp01(speed / Mathf.Max(0.01f, m_ImpactFullVolumeSpeed));
            PlayAt(hitPart ? m_Library.impactPart : m_Library.impactDesk, part.transform.position, volume);
        }

        // ---- immersion haptics ----

        void OnPartHovered(RocketPart part, IXRInteractor interactor)
        {
            if (m_Library == null || part.state == PartState.Attached) return;
            Play(interactor, m_Library.hapticHoverPart);
        }

        void OnPartGrabbed(RocketPart part, IXRInteractor interactor)
        {
            if (m_Library == null) return;
            var isRocket = part.partType == PartType.BodyTube;
            if (!isRocket && part.state == PartState.Attached) return;   // gripping a seated part: the mechanic's own feedback follows
            Play(interactor, isRocket ? m_Library.hapticGrabRocket : m_Library.hapticGrabPart);
        }

        void OnPartReleased(RocketPart part, IXRInteractor interactor)
        {
            if (m_Library == null) return;
            if (part.state == PartState.Guided || part.state == PartState.Attached) return;   // seating or staying put: not a let-go
            Play(interactor, part.partType == PartType.BodyTube ? m_Library.hapticReleaseRocket : m_Library.hapticReleasePart);
        }

        void OnGuideSlideTick(RocketPart part)
        {
            if (m_Library == null) return;
            SendHaptic(part, m_Library.hapticSlideTick);
        }

        void OnTwistTexture(RocketPart part)
        {
            if (m_Library == null) return;
            SendHaptic(part, m_Library.hapticTwistTexture);
        }

        void OnSubmitted(BuildReport report)
        {
            if (m_Library == null) return;
            SendHapticBothHands(m_Library.hapticSubmitAccepted);
        }

        void OnKitSpawned()
        {
            if (m_Library == null) return;
            SendHapticBothHands(m_Library.hapticKitSpawned);
        }

        // ---- audio ----

        /// <summary>Plays a clip as a 3D sound at a world position using the pooled sources. No allocation.</summary>
        public void PlayAt(AudioClip clip, Vector3 position, float volume = 1f)
        {
            if (clip == null || m_Sources == null) return;
            var src = m_Sources[m_NextSource];
            m_NextSource = (m_NextSource + 1) % m_Sources.Length;
            src.transform.position = position;
            src.volume = (m_Library != null ? m_Library.volume : 1f) * volume;
            src.clip = clip;
            src.Play();
        }

        // ---- haptics ----

        /// <summary>Sends a haptic to the controller that is holding, or last held, the part.</summary>
        public static bool SendHaptic(RocketPart part, FeedbackLibrary.Haptic haptic)
        {
            if (part == null) return false;
            var fb = instance;
            if (fb == null) return false;
            return fb.Play(part.lastHoldingInteractor, haptic);
        }

        /// <summary>Sends a haptic (first pulse now, optional second pulse after its gap) to the controller behind an interactor.</summary>
        public bool Play(IXRInteractor interactor, FeedbackLibrary.Haptic haptic)
        {
            if (haptic.isSilent || interactor == null) return false;
            var player = PlayerFor(interactor);
            if (player == null) return false;
            var ok = Pulse(player, haptic.amplitude, haptic.duration, haptic.frequency);
            if (haptic.hasSecond) StartCoroutine(SecondPulse(player, haptic));
            return ok;
        }

        /// <summary>Sends a haptic to both hands (the near-far interactors of the rig).</summary>
        public void SendHapticBothHands(FeedbackLibrary.Haptic haptic)
        {
            if (haptic.isSilent) return;
            m_Hands.RemoveAll(h => h == null);
            if (m_Hands.Count == 0)
                foreach (var nf in FindObjectsByType<NearFarInteractor>(FindObjectsInactive.Include, FindObjectsSortMode.None)) m_Hands.Add(nf);
            foreach (var h in m_Hands) Play(h, haptic);
        }

        HapticImpulsePlayer PlayerFor(IXRInteractor interactor)
        {
            if (m_Players.TryGetValue(interactor, out var player) && player != null) return player;
            player = null;
            if (interactor is Component c)
            {
                player = c.GetComponentInParent<HapticImpulsePlayer>();
                if (player == null && interactor is XRBaseInputInteractor input)
                {
                    // XRBaseInputInteractor creates its own player on demand; trigger that and look again
                    input.SendHapticImpulse(0f, 0f);
                    player = c.GetComponentInParent<HapticImpulsePlayer>();
                }
            }
            if (player != null) m_Players[interactor] = player;
            return player;
        }

        static bool Pulse(HapticImpulsePlayer player, float amplitude, float duration, float frequency)
        {
            return frequency > 0f ? player.SendHapticImpulse(amplitude, duration, frequency) : player.SendHapticImpulse(amplitude, duration);
        }

        IEnumerator SecondPulse(HapticImpulsePlayer player, FeedbackLibrary.Haptic haptic)
        {
            yield return new WaitForSeconds(haptic.duration + haptic.secondGap);
            if (player != null) Pulse(player, haptic.secondAmplitude, haptic.secondDuration, haptic.frequency);
        }
    }
}
