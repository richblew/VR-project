using System;

namespace VRRocket
{
    /// <summary>
    /// A small rocket flight model for the launch sequence, free of Unity dependencies so it can be unit-tested.
    /// Thrust along the rocket's body against a mass that falls as propellant burns, gravity, quadratic drag that grows when the
    /// rocket flies sideways, and per-outcome behaviour (SPEC 7.2):
    /// <list type="bullet">
    /// <item>Success flies straight up.</item>
    /// <item>UnstableFlight with one wrong flap veers off course: it pitches over towards <see cref="offCourseAzimuth"/>, arcs
    /// downrange and crashes into the ground under power.</item>
    /// <item>UnstableFlight with two or three wrong flaps breaks up: a growing corkscrew wobble, a tumble, and it is destroyed in
    /// the air soon after liftoff, low enough to be seen from the window.</item>
    /// <item>MotorRetentionLoss ejects the motor out of the base and never leaves the pad.</item>
    /// </list>
    /// Units are metres, seconds, kilograms, newtons in the visual world, where the launch vehicle is the 36x copy of the model
    /// rocket (about 13 m tall). Horizontal positions are world X and Z; azimuth 0 points along +Z and 90 along +X.
    /// </summary>
    public sealed class RocketFlightModel
    {
        // configuration
        public float dryMass = 2000f;
        public float propellantMass = 3000f;
        public float burnTime = 7f;
        public float thrust = 110000f;          // T/W about 2.2 at liftoff
        public float gravity = 9.81f;
        public float dragK = 0.35f;             // N per (m/s)^2, flying nose first
        public float broadsideDrag = 10f;       // extra N per (m/s)^2 when flying fully sideways
        public float length = 13f;              // nose to base, for ground contact
        public float groundBelowPad = 0f;       // the pad stands this high above the ground around it
        public LaunchOutcome outcome = LaunchOutcome.Success;
        /// <summary>Flaps in the wrong place or upside down (0 to 3). With UnstableFlight, one veers off course, more break up.</summary>
        public int wrongFlaps = 3;
        // off course (one wrong flap)
        public float offCourseOnset = 0.9f;
        public float pitchRate = 110f;          // degrees per second, reached after pitchRamp
        public float pitchRamp = 0.8f;
        public float maxPitch = 150f;           // past 90 the nose points at the ground
        public float offCourseAzimuth = 70f;
        // break up (two or three wrong flaps)
        public float instabilityOnset = 0.2f;
        public float instabilityGrowth = 2.4f;  // per second, exponential
        public float instabilityAmplitude = 6f; // degrees at onset
        public float wobbleFrequency = 6f;      // radians per second
        public float corkscrewRate = 220f;      // degrees per second of azimuth drift
        public float tumbleAngle = 45f;
        public float breakUpDelay = 0.35f;      // seconds from the tumble to the break-up
        public float explodeTime = 6f;          // a break-up never lasts longer than this
        // retention loss
        public float motorEjectTime = 0.6f;
        public float motorEjectSpeed = 25f;

        // state
        public float time { get; private set; }
        public float altitude { get; private set; }
        public float verticalSpeed { get; private set; }
        /// <summary>Speed along the flight path.</summary>
        public float speed { get; private set; }
        public float mass { get; private set; }
        public float tiltDeg { get; private set; }
        public float tiltAzimuthDeg { get; private set; }
        public float horizontalX { get; private set; }
        public float horizontalZ { get; private set; }
        public float motorEjectDistance { get; private set; }
        public bool burning { get; private set; }
        public bool hasLiftedOff { get; private set; }
        public bool hasTumbled { get; private set; }
        public bool hasExploded { get; private set; }
        /// <summary>It exploded by hitting the ground (off course), not in the air.</summary>
        public bool hasCrashed { get; private set; }
        public bool motorEjected { get; private set; }
        public float tumbleTime { get; private set; }
        public float maxAltitude { get; private set; }

        public bool veersOffCourse => outcome == LaunchOutcome.UnstableFlight && wrongFlaps <= 1;
        public bool breaksUp => outcome == LaunchOutcome.UnstableFlight && wrongFlaps > 1;
        /// <summary>Height of the lowest point of the rocket: the base, or the nose once it points below the horizon.</summary>
        public float lowestPoint => altitude + Math.Min(0f, length * (float)Math.Cos(tiltDeg * Math.PI / 180.0));

        float m_Vx, m_Vz;

        public RocketFlightModel()
        {
            Reset();
        }

        public void Reset()
        {
            time = 0f; altitude = 0f; verticalSpeed = 0f; speed = 0f; mass = dryMass + propellantMass;
            tiltDeg = 0f; tiltAzimuthDeg = veersOffCourse ? offCourseAzimuth : 0f; horizontalX = 0f; horizontalZ = 0f; motorEjectDistance = 0f;
            m_Vx = 0f; m_Vz = 0f; tumbleTime = -1f; maxAltitude = 0f;
            burning = false; hasLiftedOff = false; hasTumbled = false; hasExploded = false; hasCrashed = false; motorEjected = false;
        }

        /// <summary>The rocket's body axis (base to nose) as a world direction.</summary>
        public void BodyAxis(out float x, out float y, out float z)
        {
            var tilt = tiltDeg * Math.PI / 180.0;
            var az = tiltAzimuthDeg * Math.PI / 180.0;
            x = (float)(Math.Sin(tilt) * Math.Sin(az));
            y = (float)Math.Cos(tilt);
            z = (float)(Math.Sin(tilt) * Math.Cos(az));
        }

        /// <summary>True once the flight has reached its end: exploded or crashed, or (for the pad failure) the motor is long gone, or a success has climbed past <paramref name="successAltitude"/>.</summary>
        public bool IsFinished(float successAltitude)
        {
            if (hasExploded) return true;
            if (outcome == LaunchOutcome.MotorRetentionLoss) return time > motorEjectTime + 4f;
            return altitude >= successAltitude || (!burning && verticalSpeed <= 0f && time > burnTime + 1f);
        }

        public void Step(float dt)
        {
            if (dt <= 0f || hasExploded) return;
            time += dt;
            burning = time < burnTime && !(outcome == LaunchOutcome.MotorRetentionLoss && motorEjected);
            mass = dryMass + propellantMass * Math.Max(0f, 1f - time / burnTime);

            if (outcome == LaunchOutcome.MotorRetentionLoss)
            {
                // The motor ignites and is driven out of the base. The airframe stays on the pad.
                if (!motorEjected && time >= motorEjectTime) motorEjected = true;
                if (motorEjected) motorEjectDistance += motorEjectSpeed * dt * Math.Max(0f, 1f - (time - motorEjectTime) / 2.5f);
                burning = !motorEjected;
                return;
            }

            if (breaksUp && time > instabilityOnset)
            {
                var t = time - instabilityOnset;
                var amp = instabilityAmplitude * (float)Math.Exp(instabilityGrowth * t);
                tiltDeg = Math.Min(90f, amp * (0.55f + 0.45f * (float)Math.Sin(wobbleFrequency * t)));
                tiltAzimuthDeg = (tiltAzimuthDeg + corkscrewRate * dt) % 360f;
                if (tiltDeg >= tumbleAngle && !hasTumbled) { hasTumbled = true; tumbleTime = time; }
                if ((hasTumbled && time >= tumbleTime + breakUpDelay) || time >= explodeTime)
                {
                    hasExploded = true;
                    burning = false;
                    return;
                }
            }
            else if (veersOffCourse && time > offCourseOnset)
            {
                // one flap fights the others: the rocket weathercocks over to one side and keeps pitching until it meets the ground
                var ramp = Math.Min(1f, (time - offCourseOnset) / pitchRamp);
                tiltDeg = Math.Min(maxPitch, tiltDeg + pitchRate * ramp * dt);
                tiltAzimuthDeg = offCourseAzimuth;
                if (tiltDeg >= tumbleAngle && !hasTumbled) { hasTumbled = true; tumbleTime = time; }
            }

            BodyAxis(out var bx, out var by, out var bz);
            var vy = verticalSpeed;
            var v = (float)Math.Sqrt(m_Vx * m_Vx + vy * vy + m_Vz * m_Vz);
            // flying sideways (body across the flight path) costs far more drag than flying nose first
            var across = 0f;
            if (v > 0.01f)
            {
                float hx = m_Vx / v, hy = vy / v, hz = m_Vz / v;
                float cx = by * hz - bz * hy, cy = bz * hx - bx * hz, cz = bx * hy - by * hx;
                across = (float)Math.Sqrt(cx * cx + cy * cy + cz * cz);
            }
            var k = dragK + broadsideDrag * across;
            var thrustNow = burning ? thrust : 0f;
            m_Vx += (thrustNow * bx - k * v * m_Vx) / mass * dt;
            vy += ((thrustNow * by - k * v * vy) / mass - gravity) * dt;
            m_Vz += (thrustNow * bz - k * v * m_Vz) / mass * dt;
            if (!hasLiftedOff) { m_Vx = 0f; m_Vz = 0f; }

            altitude += vy * dt;
            if (altitude <= 0f && !hasLiftedOff)
            {
                altitude = 0f;
                if (vy < 0f) vy = 0f;
            }
            verticalSpeed = vy;
            horizontalX += m_Vx * dt;
            horizontalZ += m_Vz * dt;
            speed = (float)Math.Sqrt(m_Vx * m_Vx + vy * vy + m_Vz * m_Vz);
            if (altitude > 0.05f) hasLiftedOff = true;
            maxAltitude = Math.Max(maxAltitude, altitude);

            if (hasLiftedOff && lowestPoint <= -groundBelowPad)
            {
                // back on the ground: it hits nose first (or tail first) and explodes there
                altitude -= lowestPoint + groundBelowPad;
                hasCrashed = true;
                hasExploded = true;
                burning = false;
            }
        }
    }
}
