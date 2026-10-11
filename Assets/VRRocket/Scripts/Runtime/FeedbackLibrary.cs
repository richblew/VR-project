using System;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// Every clip and haptic value of SPEC.md section 6 in one asset, so the teammates' final sounds can be swapped in without touching code.
    /// Haptics are designed for the Quest 3 Touch Plus controllers, whose voice-coil actuators honour frequency: low frequency for heavy
    /// events (clunks, locks), high for fine ones (ticks, textures), short durations everywhere, optional second pulse for a "settle".
    /// </summary>
    [CreateAssetMenu(menuName = "VR Rocket/Feedback Library", fileName = "FeedbackLibrary")]
    public sealed class FeedbackLibrary : ScriptableObject
    {
        [Serializable]
        public struct Haptic
        {
            [Range(0f, 1f)] public float amplitude;
            [Tooltip("Seconds.")] public float duration;
            [Tooltip("Hz. 0 = the runtime's default. Honoured by Touch Pro and Touch Plus controllers (voice-coil), ignored by Quest 2.")] public float frequency;
            [Header("Optional second pulse (a settle or echo after the first)")]
            [Range(0f, 1f)] public float secondAmplitude;
            public float secondDuration;
            [Tooltip("Seconds between the end of the first pulse and the start of the second.")] public float secondGap;

            public Haptic(float amplitude, float duration, float frequency = 0f, float secondAmplitude = 0f, float secondDuration = 0f, float secondGap = 0f)
            {
                this.amplitude = amplitude;
                this.duration = duration;
                this.frequency = frequency;
                this.secondAmplitude = secondAmplitude;
                this.secondDuration = secondDuration;
                this.secondGap = secondGap;
            }

            public bool isSilent => amplitude <= 0f || duration <= 0f;
            public bool hasSecond => secondAmplitude > 0f && secondDuration > 0f;
        }

        [Header("Audio clips (placeholders until the audio owner delivers)")]
        public AudioClip guideEngage;        // soft tick
        public AudioClip seat;               // click: fin, flap, motor
        public AudioClip noseSeat;           // soft click
        public AudioClip capSeatUnlocked;    // soft seat sound, not the click
        public AudioClip capDetent;          // ratchet tick
        public AudioClip capLock;            // firm click
        public AudioClip unseat;             // soft unseat
        public AudioClip standRelease;
        public AudioClip impactDesk;         // dull
        public AudioClip impactPart;         // sharp
        public AudioClip respawnPop;         // soft pop
        [Range(0f, 1f)] public float volume = 1f;

        [Header("SPEC 6 haptics on the holding hand (amplitude 0-1, seconds, Hz), tunable")]
        public Haptic hapticGuideEngage = new Haptic(0.15f, 0.02f, 220f);
        public Haptic hapticSeat = new Haptic(0.5f, 0.08f, 120f, 0.15f, 0.05f, 0.03f);
        public Haptic hapticNoseSeat = new Haptic(0.4f, 0.08f, 100f);
        public Haptic hapticCapSeatUnlocked = new Haptic(0.2f, 0.04f, 100f);
        public Haptic hapticCapDetent = new Haptic(0.2f, 0.02f, 250f);
        public Haptic hapticCapLock = new Haptic(1.0f, 0.20f, 80f, 0.3f, 0.10f, 0.05f);
        public Haptic hapticPartRemoved = new Haptic(0.2f, 0.04f, 150f);
        public Haptic hapticPrototypeComplete = new Haptic(0.3f, 0.10f, 90f, 0.2f, 0.06f, 0.08f);   // both hands: clamps opening

        [Header("Immersion haptics (not in the SPEC table), tunable")]
        [Tooltip("Presence tick when a hand comes within reach of a loose part. Replaces the rig's generic hover buzz.")]
        public Haptic hapticHoverPart = new Haptic(0.08f, 0.012f, 250f);
        [Tooltip("Pick-up contact for a loose part.")]
        public Haptic hapticGrabPart = new Haptic(0.3f, 0.03f, 160f);
        [Tooltip("Heft of the complete rocket or the inspection prototype when it is picked up.")]
        public Haptic hapticGrabRocket = new Haptic(0.55f, 0.05f, 90f, 0.2f, 0.08f, 0.04f);
        [Tooltip("Letting go of a loose part.")]
        public Haptic hapticReleasePart = new Haptic(0.12f, 0.015f, 180f);
        [Tooltip("Letting go of the rocket.")]
        public Haptic hapticReleaseRocket = new Haptic(0.25f, 0.03f, 110f);
        [Tooltip("Slot texture: one tick per hapticSlideTickDistance of travel while a part is guided.")]
        public Haptic hapticSlideTick = new Haptic(0.07f, 0.01f, 220f);
        [Tooltip("Thread texture between the cap's detents: one tick per hapticTwistTextureAngle of new progress.")]
        public Haptic hapticTwistTexture = new Haptic(0.08f, 0.012f, 200f);
        [Tooltip("The stand's clamp letting the rocket go as it is lifted out.")]
        public Haptic hapticStandLiftOut = new Haptic(0.35f, 0.05f, 120f, 0.15f, 0.06f, 0.03f);
        [Tooltip("The rocket snapping back into the stand.")]
        public Haptic hapticStandCapture = new Haptic(0.6f, 0.10f, 80f, 0.2f, 0.06f, 0.04f);
        [Tooltip("Both hands: the bin accepted the rocket.")]
        public Haptic hapticSubmitAccepted = new Haptic(0.5f, 0.12f, 100f, 0.3f, 0.15f, 0.10f);
        [Tooltip("Both hands: a fresh kit arrived on the tray.")]
        public Haptic hapticKitSpawned = new Haptic(0.15f, 0.04f, 200f);
        [Tooltip("A push button going down, on the pressing hand.")]
        public Haptic hapticButtonPress = new Haptic(0.6f, 0.03f, 200f);
        [Tooltip("A push button coming back up.")]
        public Haptic hapticButtonRelease = new Haptic(0.25f, 0.02f, 180f);

        [Header("Texture spacing (before rocketScale)")]
        [Tooltip("Metres of guided travel between slide ticks.")] public float hapticSlideTickDistance = 0.004f;
        [Tooltip("Degrees of new cap progress between thread-texture ticks. Detent frames skip the texture tick.")] public float hapticTwistTextureAngle = 6f;
        [Tooltip("Minimum seconds between two hover ticks from the same part.")] public float hapticHoverCooldown = 0.25f;

        public AudioClip SeatClipFor(PartType type)
        {
            switch (type)
            {
                case PartType.NoseCone: return noseSeat;
                case PartType.MotorCap: return capSeatUnlocked;
                default: return seat;
            }
        }

        public Haptic SeatHapticFor(PartType type)
        {
            switch (type)
            {
                case PartType.NoseCone: return hapticNoseSeat;
                case PartType.MotorCap: return hapticCapSeatUnlocked;
                default: return hapticSeat;
            }
        }
    }
}
