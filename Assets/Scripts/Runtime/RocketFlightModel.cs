using System;

namespace VRRocket
{
    /// <summary>
    /// A small rocket flight model for the launch sequence, free of Unity dependencies so it can be unit-tested.
    /// Thrust against a mass that falls as propellant burns, gravity, quadratic drag, and per-outcome behaviour (SPEC 7.2):
    /// Success flies straight; UnstableFlight develops a growing corkscrew tilt, tumbles and is destroyed; MotorRetentionLoss
    /// ejects the motor out of the base and never leaves the pad. Units are metres, seconds, kilograms, newtons in the visual world,
    /// where the launch vehicle is the 36x copy of the model rocket (about 13 m tall).
    /// </summary>
    public sealed class RocketFlightModel
    {
        // configuration
        public float dryMass = 2000f;
        public float propellantMass = 3000f;
        public float burnTime = 7f;
        public float thrust = 110000f;          // T/W about 2.2 at liftoff
        public float gravity = 9.81f;
        public float dragK = 0.35f;             // N per (m/s)^2
        public LaunchOutcome outcome = LaunchOutcome.Success;
        public float instabilityOnset = 1.5f;
        public float instabilityGrowth = 0.9f;  // per second, exponential
        public float instabilityAmplitude = 3f; // degrees at onset
        public float wobbleFrequency = 2.5f;    // radians per second
        public float corkscrewRate = 140f;      // degrees per second of azimuth drift
        public float tumbleAngle = 45f;
        public float explodeTime = 7.5f;
        public float motorEjectTime = 0.6f;
        public float motorEjectSpeed = 25f;

        // state
        public float time { get; private set; }
        public float altitude { get; private set; }
        public float verticalSpeed { get; private set; }
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
        public bool motorEjected { get; private set; }

        float m_HorizontalVx, m_HorizontalVz;

        public RocketFlightModel()
        {
            Reset();
        }

        public void Reset()
        {
            time = 0f; altitude = 0f; verticalSpeed = 0f; mass = dryMass + propellantMass;
            tiltDeg = 0f; tiltAzimuthDeg = 0f; horizontalX = 0f; horizontalZ = 0f; motorEjectDistance = 0f;
            m_HorizontalVx = 0f; m_HorizontalVz = 0f;
            burning = false; hasLiftedOff = false; hasTumbled = false; hasExploded = false; motorEjected = false;
        }

        /// <summary>True once the flight has reached its end: exploded, or (for the pad failure) the motor is long gone, or a success has climbed past <paramref name="successAltitude"/>.</summary>
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

            if (outcome == LaunchOutcome.UnstableFlight && time > instabilityOnset)
            {
                var t = time - instabilityOnset;
                var amp = instabilityAmplitude * (float)Math.Exp(instabilityGrowth * t);
                tiltDeg = Math.Min(90f, amp * (0.55f + 0.45f * (float)Math.Sin(wobbleFrequency * t)));
                tiltAzimuthDeg = (tiltAzimuthDeg + corkscrewRate * dt) % 360f;
                if (tiltDeg >= tumbleAngle) hasTumbled = true;
                if (time >= explodeTime)
                {
                    hasExploded = true;
                    burning = false;
                    return;
                }
            }

            var thrustNow = burning ? thrust : 0f;
            var tiltRad = tiltDeg * (float)Math.PI / 180f;
            var vertical = thrustNow * (float)Math.Cos(tiltRad);
            var lateral = thrustNow * (float)Math.Sin(tiltRad);
            var drag = dragK * verticalSpeed * Math.Abs(verticalSpeed);
            var accel = vertical / mass - gravity - drag / mass;
            verticalSpeed += accel * dt;
            altitude += verticalSpeed * dt;
            if (altitude <= 0f)
            {
                altitude = 0f;
                if (verticalSpeed < 0f) verticalSpeed = 0f;
            }
            if (altitude > 0.05f) hasLiftedOff = true;

            if (hasLiftedOff)
            {
                var azRad = tiltAzimuthDeg * (float)Math.PI / 180f;
                m_HorizontalVx += lateral / mass * (float)Math.Sin(azRad) * dt;
                m_HorizontalVz += lateral / mass * (float)Math.Cos(azRad) * dt;
                horizontalX += m_HorizontalVx * dt;
                horizontalZ += m_HorizontalVz * dt;
            }
        }
    }
}
