using UnityEngine;

namespace VRRocket
{
    /// <summary>Every tunable of SPEC.md section 10.4. Distances scale with <see cref="rocketScale"/>.</summary>
    [CreateAssetMenu(menuName = "VR Rocket/Assembly Tuning", fileName = "AssemblyTuning")]
    public sealed class AssemblyTuning : ScriptableObject
    {
        [Header("Scale")]
        [Tooltip("Uniform scale of the whole kit. Try 1.5 if parts feel fiddly in the headset.")]
        public float rocketScale = 1.0f;

        [Header("Guide radii (metres, before rocketScale)")]
        public float glowRadius = 0.12f;
        public float captureRadius = 0.05f;
        public float breakRadius = 0.08f;

        [Header("Guide length along the axis (metres, before rocketScale)")]
        public float guideLengthPlates = 0.05f;
        public float guideLengthNose = 0.06f;
        [Tooltip("An attached battery's track when grabbed to take it out (it comes free once its + end leaves the tube)")]
        public float guideLengthMotor = 0.14f;
        public float guideLengthCap = 0.04f;
        [Tooltip("How far the finished rocket is lifted before it leaves the stand's clamp.")]
        public float guideLengthTube = 0.10f;

        [Header("Orientation tolerance (degrees)")]
        public float orientationTolerancePlates = 40f;
        public float orientationToleranceOthers = 45f;

        [Header("Battery (the motor part) insertion")]
        [Tooltip("The battery is caught once its + end is past the tube's base end and within this distance of the tube's axis: the opening, plus a little give. Never before.")]
        public float motorEntryRadius = 0.02f;
        [Tooltip("How far off the tube's axis the battery may lean and still be caught once its + end is in (more, or the wrong way up, and it is kept out)")]
        public float motorEntryTilt = 60f;
        [Tooltip("Pulled back out until its + end is this far past the tube's end, the battery comes off the track")]
        public float motorExitMargin = 0.01f;
        [Tooltip("The battery's length (metres, before rocketScale), from its - end (its pivot) to its + end")]
        public float motorLength = 0.07f;
        [Tooltip("A battery the wrong way up is kept out of the tube within this distance of its axis")]
        public float motorBlockRadius = 0.04f;
        [Header("Seating")]
        public float seatDuration = 0.12f;
        public float seatDurationMotor = 0.30f;
        [Range(0f, 1f), Tooltip("Fraction of the hand's distance shown at the seat while guiding the nose. Fades to 1 at the guide's outer end.")]
        public float noseMagnetism = 0.5f;
        [Tooltip("Seconds over which the part eases between free and guided poses, so engaging and breaking away never jump.")]
        public float guideBlendDuration = 0.1f;

        [Header("Motor cap twist")]
        public float capLockAngle = 180f;
        public float capDetentAngle = 30f;
        public float capUnlockedGap = 0.002f;
        [Tooltip("An unlocked cap comes off when pulled this far, mostly along the axis, while the rocket is held or on the stand")]
        public float capPullOff = 0.06f;
        [Tooltip("Cap turn per degree of wrist turn, like the XRI example knob: one comfortable turn closes it")]
        public float capTwistSensitivity = 1.5f;
        [Range(0f, 1f), Tooltip("Letting go after this fraction of the lock angle finishes the lock")]
        public float capAutoFinishFraction = 0.75f;
        [Tooltip("A hand this close to an unlocked seated cap grabs the cap, not the rocket")]
        public float capGrabPriorityRadius = 0.07f;

        [Header("Rules")]
        public bool enforceOrder = false;   // free build order (Praise, 11 Oct 2026); the cap still needs the motor

        [Header("Workstation")]
        public float standClearance = 0.20f;
        public float respawnDelay = 1.5f;

        [Header("Near-only grab")]
        [Tooltip("A hand further than this from a part's collider cannot grab or hover it. This is what blocks far casting.")]
        public float nearGrabDistance = 0.12f;

        public float GuideLengthFor(PartType type)
        {
            switch (type)
            {
                case PartType.NoseCone: return guideLengthNose;
                case PartType.Motor: return guideLengthMotor;
                case PartType.MotorCap: return guideLengthCap;
                case PartType.BodyTube: return guideLengthTube;
                default: return guideLengthPlates;
            }
        }

        public float OrientationToleranceFor(PartType type)
        {
            if (type == PartType.Motor) return motorEntryTilt;
            return type == PartType.TailFin || type == PartType.WingFlap ? orientationTolerancePlates : orientationToleranceOthers;
        }

        public float SeatDurationFor(PartType type)
        {
            return type == PartType.Motor ? seatDurationMotor : seatDuration;
        }
    }
}
