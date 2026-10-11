using NUnit.Framework;

namespace VRRocket.Tests
{
    /// <summary>SPEC.md 12.1: forward path, every backward move, each removal exception of 5.5, gating with enforceOrder on and off.</summary>
    public class AssemblyStateMachineTests
    {
        static AssemblyStateMachine Airframe(AssemblyStateMachine m)
        {
            m.Apply(PartType.TailFin, true); m.Apply(PartType.TailFin, true); m.Apply(PartType.TailFin, true);
            m.Apply(PartType.WingFlap, true); m.Apply(PartType.WingFlap, true); m.Apply(PartType.WingFlap, true);
            m.Apply(PartType.NoseCone, true);
            return m;
        }

        [Test]
        public void ForwardPath()
        {
            var m = new AssemblyStateMachine();
            Assert.AreEqual(AssemblyState.BuildingAirframe, m.State);
            m.Apply(PartType.TailFin, true); m.Apply(PartType.TailFin, true); m.Apply(PartType.TailFin, true);
            m.Apply(PartType.WingFlap, true); m.Apply(PartType.WingFlap, true);
            m.Apply(PartType.NoseCone, true);
            Assert.AreEqual(AssemblyState.BuildingAirframe, m.State, "one flap short");
            Assert.IsTrue(m.Apply(PartType.WingFlap, true), "state changes on the last airframe part");
            Assert.AreEqual(AssemblyState.AirframeComplete, m.State);
            m.Apply(PartType.Motor, true);
            Assert.AreEqual(AssemblyState.MotorFitted, m.State);
            m.Apply(PartType.MotorCap, true);
            Assert.AreEqual(AssemblyState.PrototypeComplete, m.State);
            m.SetSubmitted(true);
            Assert.AreEqual(AssemblyState.Submitted, m.State);
        }

        [Test]
        public void BackwardMoves()
        {
            var m = Airframe(new AssemblyStateMachine());
            Assert.AreEqual(AssemblyState.AirframeComplete, m.State);
            m.Apply(PartType.TailFin, false);
            Assert.AreEqual(AssemblyState.BuildingAirframe, m.State, "pulling a fin in AirframeComplete returns to BuildingAirframe");
            m.Apply(PartType.TailFin, true);
            m.Apply(PartType.Motor, true);
            m.Apply(PartType.MotorCap, true);
            Assert.AreEqual(AssemblyState.PrototypeComplete, m.State);
            m.Apply(PartType.MotorCap, false);
            Assert.AreEqual(AssemblyState.MotorFitted, m.State, "removing the cap goes back to MotorFitted");
            m.Apply(PartType.Motor, false);
            Assert.AreEqual(AssemblyState.AirframeComplete, m.State, "removing the motor goes back to AirframeComplete");
            m.Apply(PartType.NoseCone, false);
            Assert.AreEqual(AssemblyState.BuildingAirframe, m.State);
        }

        [Test]
        public void Gating_EnforceOrderOn()
        {
            var m = new AssemblyStateMachine { enforceOrder = true };
            Assert.IsTrue(m.Allows(PartType.TailFin));
            Assert.IsFalse(m.Allows(PartType.Motor), "motor refused before the airframe is complete");
            Assert.IsFalse(m.Allows(PartType.MotorCap), "cap refused before the motor");
            Airframe(m);
            Assert.IsTrue(m.Allows(PartType.Motor));
            Assert.IsFalse(m.Allows(PartType.MotorCap));
            m.Apply(PartType.Motor, true);
            Assert.IsTrue(m.Allows(PartType.MotorCap));
            Assert.IsFalse(m.Allows(PartType.TailFin), "airframe points gated off once the motor is seated");
            m.SetSubmitted(true);
            Assert.IsFalse(m.Allows(PartType.MotorCap), "nothing while submitted");
        }

        [Test]
        public void Gating_EnforceOrderOff()
        {
            var m = new AssemblyStateMachine { enforceOrder = false };
            Assert.IsTrue(m.Allows(PartType.Motor), "motor accepted on a bare tube");
            Assert.IsFalse(m.Allows(PartType.MotorCap), "the cap still needs the motor");
            m.Apply(PartType.Motor, true);
            Assert.IsTrue(m.Allows(PartType.MotorCap));
            Assert.IsTrue(m.Allows(PartType.TailFin), "airframe parts still accepted after the motor");
        }

        [Test]
        public void RemovalExceptions()
        {
            var m = Airframe(new AssemblyStateMachine { enforceOrder = true });
            Assert.IsTrue(m.CanRemove(PartType.TailFin));
            m.Apply(PartType.Motor, true);
            Assert.IsFalse(m.CanRemove(PartType.TailFin), "airframe parts cannot be removed once the motor is seated");
            Assert.IsFalse(m.CanRemove(PartType.NoseCone));
            Assert.IsTrue(m.CanRemove(PartType.Motor));
            m.Apply(PartType.MotorCap, true);
            Assert.IsFalse(m.CanRemove(PartType.Motor), "the motor cannot be removed while the cap is seated");
            Assert.IsTrue(m.CanRemove(PartType.MotorCap), "an unlocked cap can come off (lock is the twist's business)");
            Assert.IsTrue(m.CanRemove(PartType.BodyTube), "the stand releases the finished prototype");
            m.Apply(PartType.MotorCap, false);
            Assert.IsFalse(m.CanRemove(PartType.BodyTube));
            m.SetSubmitted(true);
            Assert.IsFalse(m.CanRemove(PartType.MotorCap), "frozen once submitted");
        }

        [Test]
        public void RemovalExceptions_EnforceOrderOff_KeepsPhysicalRulesOnly()
        {
            var m = new AssemblyStateMachine { enforceOrder = false };
            m.Apply(PartType.Motor, true);
            Assert.IsTrue(m.CanRemove(PartType.TailFin), "order rule relaxed");
            m.Apply(PartType.MotorCap, true);
            Assert.IsFalse(m.CanRemove(PartType.Motor), "the cap still blocks the motor");
        }

        [Test]
        public void Reset_ClearsEverything()
        {
            var m = Airframe(new AssemblyStateMachine());
            m.Apply(PartType.Motor, true); m.SetSubmitted(true);
            m.Reset();
            Assert.AreEqual(AssemblyState.BuildingAirframe, m.State);
            Assert.AreEqual(0, m.finsAttached);
            Assert.IsFalse(m.submitted);
        }
    }
}
