using NUnit.Framework;

namespace VRRocket.Tests
{
    public class BuildReportTests
    {
        static FlapPlacement Flap(int id, FlapPosition pos, FlapOrientation ori, string slot = "FlapSlot_Mid_1")
        {
            return new FlapPlacement { flapId = id, slotName = slot, position = pos, orientation = ori };
        }

        static FlapPlacement[] AllCorrect()
        {
            return new[]
            {
                Flap(1, FlapPosition.Midpoint, FlapOrientation.Up),
                Flap(2, FlapPosition.Midpoint, FlapOrientation.Up),
                Flap(3, FlapPosition.Midpoint, FlapOrientation.Up),
            };
        }

        [Test]
        public void LockedAndCorrect_IsSuccess()
        {
            var r = BuildReport.Build(AllCorrect(), capLocked: true);
            Assert.AreEqual(LaunchOutcome.Success, r.outcome);
            Assert.IsFalse(r.FlapError);
            Assert.AreEqual(0, r.FailedCount);
        }

        [Test]
        public void LockedAndFlapWrong_IsUnstableFlight()
        {
            var flaps = AllCorrect();
            flaps[1] = Flap(2, FlapPosition.Base, FlapOrientation.Up, "FlapSlot_Low_2");
            var r = BuildReport.Build(flaps, capLocked: true);
            Assert.AreEqual(LaunchOutcome.UnstableFlight, r.outcome);
            Assert.IsTrue(r.FlapError);
            CollectionAssert.AreEqual(new[] { "WingFlap_2" }, r.failedComponents);
        }

        [Test]
        public void UnlockedAndCorrect_IsMotorRetentionLoss()
        {
            var r = BuildReport.Build(AllCorrect(), capLocked: false);
            Assert.AreEqual(LaunchOutcome.MotorRetentionLoss, r.outcome);
            CollectionAssert.AreEqual(new[] { "MotorCap" }, r.failedComponents);
        }

        [Test]
        public void UnlockedAndFlapWrong_IsMotorRetentionLoss_AndListsEverything()
        {
            var flaps = AllCorrect();
            flaps[0] = Flap(1, FlapPosition.Base, FlapOrientation.Up, "FlapSlot_Low_1");
            flaps[2] = Flap(3, FlapPosition.Midpoint, FlapOrientation.Down, "FlapSlot_Mid_3");
            var r = BuildReport.Build(flaps, capLocked: false);
            Assert.AreEqual(LaunchOutcome.MotorRetentionLoss, r.outcome);
            Assert.IsTrue(r.FlapError);
            CollectionAssert.AreEqual(new[] { "MotorCap", "WingFlap_1", "WingFlap_3" }, r.failedComponents);
            Assert.AreEqual(3, r.FailedCount);
        }

        [Test]
        public void UpsideDownFlap_IsNotCorrect()
        {
            Assert.IsFalse(Flap(1, FlapPosition.Midpoint, FlapOrientation.Down).IsCorrect);
            Assert.IsFalse(Flap(1, FlapPosition.Base, FlapOrientation.Up).IsCorrect);
            Assert.IsTrue(Flap(1, FlapPosition.Midpoint, FlapOrientation.Up).IsCorrect);
        }
    }
}
