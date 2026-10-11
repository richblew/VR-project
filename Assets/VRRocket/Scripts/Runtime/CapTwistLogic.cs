using System;

namespace VRRocket
{
    /// <summary>
    /// The ratchet of SPEC.md section 5.4, free of Unity dependencies so it can be unit-tested (section 12.1).
    /// Progress is clockwise-only turn in degrees. Turning back never unwinds. Progress is kept between grabs,
    /// locks exactly at lockAngle, resets on pull-off and cannot change once locked.
    /// </summary>
    [Serializable]
    public sealed class CapTwistLogic
    {
        public float lockAngle = 180f;
        public float detentAngle = 30f;

        public float progress { get; private set; }
        public bool locked { get; private set; }

        public CapTwistLogic() { }

        public CapTwistLogic(float lockAngle, float detentAngle)
        {
            this.lockAngle = lockAngle;
            this.detentAngle = detentAngle;
        }

        /// <summary>
        /// Feed one step of hand rotation in degrees (positive = clockwise viewed from below).
        /// Returns how many detent ticks were crossed and whether the cap locked on this step.
        /// A detent coinciding with the lock is not reported: the lock click replaces it.
        /// </summary>
        public (int detents, bool justLocked) Advance(float deltaDegrees)
        {
            if (locked) return (0, false);
            var d = Math.Max(0f, deltaDegrees);
            if (d <= 0f) return (0, false);
            const float k_Epsilon = 1e-4f;
            var before = progress;
            progress = Math.Min(lockAngle, before + d);
            // Float error from summed hand deltas must not leave the cap at 179.9999: that is a lock.
            var justLocked = progress >= lockAngle - k_Epsilon;
            if (justLocked)
            {
                locked = true;
                progress = lockAngle;
            }
            var detents = 0;
            if (detentAngle > 0f)
            {
                // Count every detent boundary crossed on this step, strictly below the lock angle:
                // the detent that coincides with the lock is replaced by the lock click.
                for (var k = (int)Math.Floor(before / detentAngle) + 1; ; k++)
                {
                    var boundary = k * detentAngle;
                    if (boundary > progress + k_Epsilon) break;
                    if (boundary <= before + k_Epsilon) continue;
                    if (boundary >= lockAngle - k_Epsilon) break;
                    detents++;
                }
            }
            return (detents, justLocked);
        }

        /// <summary>Pull-off: progress goes back to zero.</summary>
        public void Reset()
        {
            progress = 0f;
            locked = false;
        }
    }
}
