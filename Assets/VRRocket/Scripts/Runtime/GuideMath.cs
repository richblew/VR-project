using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The geometry of the guided attach mechanic (SPEC.md section 5.2), kept free of scene references so it can be unit-tested.
    /// All positions are world space. <c>axis</c> is the unit guide direction pointing away from the seated position.
    /// </summary>
    public static class GuideMath
    {
        /// <summary>Signed distance of a position along the guide axis from the seat. Positive is away from the tube.</summary>
        public static float Depth(Vector3 position, Vector3 seat, Vector3 axis)
        {
            return Vector3.Dot(position - seat, axis);
        }

        /// <summary>Distance of a position from the guide axis line.</summary>
        public static float Sideways(Vector3 position, Vector3 seat, Vector3 axis)
        {
            var rel = position - seat;
            return (rel - axis * Vector3.Dot(rel, axis)).magnitude;
        }

        /// <summary>
        /// Where the part is shown while guided: on the axis, clamped between the seat and guideLength,
        /// scaled by displayFraction (1 for most parts, noseMagnetism for the nose cone).
        /// </summary>
        public static Vector3 GuidedPosition(Vector3 seat, Vector3 axis, float depth, float guideLength, float displayFraction)
        {
            var clamped = Mathf.Clamp(depth, 0f, guideLength);
            return seat + axis * (clamped * displayFraction);
        }

        /// <summary>The guide lets go when the hand drifts sideways past breakRadius or pulls beyond the end of the guide.</summary>
        public static bool ShouldBreak(float depth, float sideways, float guideLength, float breakRadius)
        {
            return sideways > breakRadius || depth > guideLength;
        }

        /// <summary>Is the held part roughly the right way round? Compares the part's reference axis with the point's.</summary>
        public static bool WithinOrientation(Vector3 partAxis, Vector3 pointAxis, float toleranceDegrees)
        {
            return Vector3.Angle(partAxis, pointAxis) <= toleranceDegrees;
        }

        /// <summary>
        /// The rotation a part takes while guided and when seated (SPEC.md section 5.3).
        /// Fins: the slot's rotation, roll corrected. Flaps: the slot's rotation, kept whichever way up it is held.
        /// Nose cone, motor, cap: the part's axis aligned to the tube axis, roll kept.
        /// </summary>
        public static Quaternion SeatedRotation(PartType type, Quaternion pointRotation, Quaternion currentRotation)
        {
            switch (type)
            {
                case PartType.TailFin:
                    return pointRotation;
                case PartType.WingFlap:
                {
                    var flipped = pointRotation * Quaternion.Euler(0f, 0f, 180f);
                    return Quaternion.Angle(currentRotation, flipped) < Quaternion.Angle(currentRotation, pointRotation) ? flipped : pointRotation;
                }
                default:
                {
                    var currentUp = currentRotation * Vector3.up;
                    var pointUp = pointRotation * Vector3.up;
                    return Quaternion.FromToRotation(currentUp, pointUp) * currentRotation;
                }
            }
        }

        /// <summary>Which way up is a flap seated with this rotation relative to its slot? Up means the strongly swept edge is toward the nose.</summary>
        public static FlapOrientation FlapOrientationFor(Quaternion slotRotation, Quaternion seatedRotation)
        {
            var slotUp = slotRotation * Vector3.up;
            var partUp = seatedRotation * Vector3.up;
            return Vector3.Dot(slotUp, partUp) >= 0f ? FlapOrientation.Up : FlapOrientation.Down;
        }
    }
}
