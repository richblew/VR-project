using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

namespace VRRocket.Tests
{
    /// <summary>
    /// The launch outside VRRocket_Final's window: fire that starts under the rocket and never climbs its side, pad smoke that
    /// rolls out along the ground, a trail behind the climb, and the failures (a crash on the ground in the window's view for one
    /// wrong flap, a break-up low in the air for more), each leaving an explosion and debris that a reset clears.
    /// </summary>
    public class LaunchEffectsTests : DevSceneTestBase
    {
        protected override string scenePath => FinalSceneTests.Path;

        LaunchSequence Launch => Object.FindAnyObjectByType<LaunchSequence>();

        // where the player stands at the console, looking out of the window along +X
        static readonly Vector3 k_Console = new Vector3(0f, 1.6f, 1.8f);

        IEnumerator Ignite(LaunchSequence launch)
        {
            launch.PlaceVehicleOnPad(0f);
            yield return null;
            launch.Ignite();
        }

        /// <summary>How far above the nozzle the highest flame streak reaches (negative: they all stay below it).</summary>
        static float FlameReachAboveNozzle(ParticleSystem flame)
        {
            var particles = new ParticleSystem.Particle[flame.main.maxParticles];
            var n = flame.GetParticles(particles);
            var r = flame.GetComponent<ParticleSystemRenderer>();
            var highest = float.NegativeInfinity;
            for (var i = 0; i < n; i++)
            {
                // local space, +Z points down out of the nozzle; a stretched streak is centred on its particle
                var p = particles[i];
                var half = 0.5f * (p.GetCurrentSize(flame) * r.lengthScale + p.velocity.magnitude * r.velocityScale);
                highest = Mathf.Max(highest, half - p.position.z);
            }
            return n > 0 ? highest : float.NegativeInfinity;
        }

        [UnityTest]
        public IEnumerator Ignition_Lights_Under_The_Rocket_Builds_And_Rolls_Smoke_Out_Along_The_Ground()
        {
            var launch = Launch;
            yield return Ignite(launch);
            yield return new WaitForSeconds(0.3f);
            Assert.IsTrue(launch.isIgnited);
            Assert.Less(launch.flameLevel, 0.6f, "it lights small");
            Assert.IsTrue(launch.padSmoke.isEmitting && launch.padFire.isEmitting, "smoke and fire start at ignition, not at liftoff");
            var start = launch.vehicle.transform.position;
            for (var t = 0f; t < 2.5f; t += Time.deltaTime)
            {
                yield return null;
                Assert.Less(FlameReachAboveNozzle(launch.flame), 0.15f, "no flame streak reaches up the rocket's side");
            }
            Assert.Greater(launch.flameLevel, 0.65f, "and builds up before liftoff");
            Assert.Less(Vector3.Distance(start, launch.vehicle.transform.position), 1e-3f, "held on the mount until liftoff");

            var smoke = launch.padSmoke;
            var particles = new ParticleSystem.Particle[smoke.main.maxParticles];
            var n = smoke.GetParticles(particles);
            Assert.Greater(n, 20, "a cloud has built up");
            float spread = 0f, rise = 0f;
            var centre = smoke.transform.position;
            for (var i = 0; i < n; i++)
            {
                var d = particles[i].position - centre;
                spread += new Vector2(d.x, d.z).magnitude; rise += d.y;
            }
            Assert.Greater(spread / n, 1.5f * (rise / n), "it rolls out along the ground rather than climbing beside the rocket");
        }

        [UnityTest]
        public IEnumerator Liftoff_Climbs_On_Its_Flame_Leaves_A_Trail_And_The_Pad_Fire_Dies_Away()
        {
            var launch = Launch;
            yield return Ignite(launch);
            yield return new WaitForSeconds(0.5f);
            var done = false;
            launch.Liftoff(LaunchOutcome.Success, () => done = true, 0);
            for (var f = 0; f < 60; f++)
            {
                yield return null;
                Assert.Less(FlameReachAboveNozzle(launch.flame), 0.15f, "the flame stays under the nozzle as it climbs");
            }
            yield return new WaitForSeconds(2.5f);
            Assert.Greater(launch.altitude, 30f);
            Assert.AreEqual(1f, launch.flameLevel, "full thrust");
            Assert.Greater(launch.exhaustTrail.particleCount, 20, "a trail is left behind");
            Assert.AreEqual(0f, launch.padFire.emission.rateOverTimeMultiplier, "the pad fire has died away with the nozzle gone");
            Assert.IsFalse(done);
            Assert.IsNull(launch.activeExplosion);
        }

        [UnityTest]
        public IEnumerator One_Wrong_Flap_Veers_Off_And_Crashes_On_The_Ground_In_The_Windows_View()
        {
            var launch = Launch;
            yield return Ignite(launch);
            var done = false;
            launch.Liftoff(LaunchOutcome.UnstableFlight, () => done = true, 1);
            for (var t = 0f; t < 10f && launch.activeExplosion == null; t += Time.deltaTime) yield return null;
            var boom = launch.activeExplosion;
            Assert.IsNotNull(boom, "it crashed");
            Assert.IsTrue(launch.model.hasCrashed, "on the ground, not in the air");
            var ground = boom.transform.Find("Ground");
            Assert.IsTrue(ground.gameObject.activeSelf, "with its ground effects (dirt, scorch, smoke column)");
            Assert.AreEqual(0f, ground.position.y, 0.3f, "the ground effects sit on the ground");
            Assert.Greater(boom.transform.position.y, 2f, "the fireball sits above the impact, not half buried");
            var look = boom.transform.position - k_Console;
            var bearing = Mathf.Atan2(look.z, look.x) * Mathf.Rad2Deg;
            Assert.That(bearing, Is.InRange(-25f, 25f), "out of the window, not behind a wall");
            Assert.Less(new Vector2(look.x, look.z).magnitude, 160f, "near enough to see");
            Assert.Greater(Vector3.Distance(boom.transform.position, launch.vehicle.transform.parent.position), 30f, "away from the pad");
            Assert.AreEqual(8, launch.debris.Count, "fins, flaps, nose and cap are thrown out");
            foreach (var d in launch.debris) Assert.IsNotNull(d.GetComponent<Rigidbody>());
            Assert.IsFalse(launch.vehicle.activeSelf);
            Assert.IsFalse(launch.flame.isEmitting);
            yield return new WaitForSeconds(3f);
            Assert.IsTrue(done, "the flight ends a moment after the crash");
        }

        [UnityTest]
        public IEnumerator Two_Or_More_Wrong_Flaps_Break_Up_Low_In_The_Air_And_A_Reset_Clears_It()
        {
            var launch = Launch;
            yield return Ignite(launch);
            launch.Liftoff(LaunchOutcome.UnstableFlight, null, 3);
            for (var t = 0f; t < 6f && launch.activeExplosion == null; t += Time.deltaTime) yield return null;
            var boom = launch.activeExplosion;
            Assert.IsNotNull(boom, "it broke up");
            Assert.IsFalse(launch.model.hasCrashed, "in the air");
            Assert.IsFalse(boom.transform.Find("Ground").gameObject.activeSelf, "no ground effects in the air");
            Assert.That(boom.transform.position.y, Is.InRange(10f, 32f), "low enough to watch through the window");
            Assert.AreEqual(8, launch.debris.Count);
            yield return new WaitForSeconds(1.5f);
            var fell = 0;
            foreach (var d in launch.debris) if (d != null && d.GetComponent<Rigidbody>().linearVelocity.y < 0f) fell++;
            Assert.Greater(fell, 0, "the pieces fall");

            launch.ResetPad();
            yield return null;
            Assert.IsTrue(launch.activeExplosion == null, "the explosion is gone");
            Assert.AreEqual(0, launch.debris.Count);
            foreach (var b in Object.FindObjectsByType<Rigidbody>()) Assert.IsFalse(b.name.StartsWith("Debris_"), "no debris left behind: " + b.name);
            Assert.IsFalse(launch.vehicle.activeSelf, "the pad is empty");
        }

        [UnityTest]
        public IEnumerator Every_Vehicle_Part_Is_36_Times_Its_Kit_Part()
        {
            yield return null;
            var launch = Launch;
            launch.PlaceVehicleOnPad(0f);
            yield return null;
            foreach (var (type, name) in new[] { (PartType.TailFin, "TailFin_1"), (PartType.TailFin, "TailFin_3"), (PartType.WingFlap, "WingFlap_1"), (PartType.NoseCone, "NoseCone_1"), (PartType.Motor, "Motor_1"), (PartType.MotorCap, "MotorCap_1") })
                Assert.AreEqual(36f, VehicleRatio(launch, name, Part(type)), 0.5f, name + ": the launch rocket shows the kit part at 36 times (a model swap must not shrink it)");
        }

        [UnityTest]
        public IEnumerator A_Blown_Out_Battery_Comes_Back_Its_Own_Size()
        {
            yield return null;
            var launch = Launch;
            launch.PlaceVehicleOnPad(0f);
            yield return null;
            var battery = VehiclePart(launch, "Motor_1");
            var cap = VehiclePart(launch, "MotorCap_1");
            var batterySize = PartSize(battery);
            var capSize = PartSize(cap);
            var seat = battery.parent;
            launch.Ignite();
            var done = false;
            launch.Liftoff(LaunchOutcome.MotorRetentionLoss, () => done = true, 0);
            var ejected = false;
            for (var t = 0f; t < 15f && !done; t += Time.deltaTime) { ejected |= launch.model.motorEjected; yield return null; }
            Assert.IsTrue(done, "the blow-out flight ends");
            Assert.IsTrue(ejected, "the battery blew out of the base");
            launch.PlaceVehicleOnPad(0f);   // the next submission: resets the pad, then builds the vehicle again
            yield return null;
            Assert.AreSame(seat, battery.parent, "the battery is back in its seat");
            Assert.AreEqual(batterySize, PartSize(battery), batterySize * 0.01f, "at its own size, not 36 times it");
            Assert.AreEqual("CapSeat", cap.parent.name, "the cap is back on its seat");
            Assert.AreEqual(capSize, PartSize(cap), capSize * 0.01f, "at its own size");
            Assert.Less(Vector3.Distance(battery.position, seat.position), 0.01f, "and in place");
        }

        [UnityTest]
        public IEnumerator Abort_During_Ignition_Puts_Everything_Out()
        {
            var launch = Launch;
            yield return Ignite(launch);
            yield return new WaitForSeconds(1f);
            launch.AbortCountdown();
            yield return null;
            Assert.IsFalse(launch.isIgnited);
            Assert.IsFalse(launch.flame.isEmitting);
            Assert.IsFalse(launch.padSmoke.isEmitting);
            Assert.IsFalse(launch.padFire.isEmitting);
            Assert.AreEqual(0f, launch.flameLevel);
            yield return new WaitForSeconds(3f);
            Assert.IsFalse(launch.flame.isEmitting, "and it stays out: the build-up does not relight it");
        }
    }
}
