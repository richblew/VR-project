using NUnit.Framework;

namespace VRRocket.Tests
{
    /// <summary>SPEC.md 12.1: progress accumulates across grabs, never decreases, locks exactly at capLockAngle, resets on pull-off, cannot change after locking.</summary>
    public class CapTwistLogicTests
    {
        static CapTwistLogic New() => new CapTwistLogic(180f, 30f);

        [Test]
        public void AccumulatesAcrossGrabs_AndNeverDecreases()
        {
            var l = New();
            l.Advance(40f);
            l.Advance(-100f);          // turning back: nothing
            Assert.AreEqual(40f, l.progress, 1e-4f);
            l.Advance(25f);            // next grab
            Assert.AreEqual(65f, l.progress, 1e-4f);
            Assert.IsFalse(l.locked);
        }

        [Test]
        public void DetentsTickEveryDetentAngle_OnNewProgressOnly()
        {
            var l = New();
            var (d1, _) = l.Advance(100f);     // crosses 30, 60, 90
            Assert.AreEqual(3, d1);
            var (d2, _) = l.Advance(-50f);     // back: no ticks
            Assert.AreEqual(0, d2);
            var (d3, _) = l.Advance(29f);      // 100 -> 129: crosses 120
            Assert.AreEqual(1, d3);
            var (d4, _) = l.Advance(1f);       // 130: nothing new
            Assert.AreEqual(0, d4);
        }

        [Test]
        public void DetentCrossingCountsExactly()
        {
            var l = New();
            Assert.AreEqual(0, l.Advance(29.9f).detents);
            Assert.AreEqual(1, l.Advance(0.2f).detents);   // crosses 30
            Assert.AreEqual(2, l.Advance(60f).detents);    // 30.1 -> 90.1 crosses 60 and 90
        }

        [Test]
        public void LocksExactlyAtLockAngle_WithoutOvershoot()
        {
            var l = New();
            l.Advance(170f);
            var (detents, justLocked) = l.Advance(25f);    // would reach 195
            Assert.IsTrue(justLocked);
            Assert.IsTrue(l.locked);
            Assert.AreEqual(180f, l.progress, 1e-4f);
            Assert.AreEqual(0, detents, "the detent at 180 is replaced by the lock click");
        }

        [Test]
        public void LockReportedOnce_AndNothingChangesAfterwards()
        {
            var l = New();
            l.Advance(180f);
            Assert.IsTrue(l.locked);
            var (detents, justLocked) = l.Advance(90f);
            Assert.IsFalse(justLocked);
            Assert.AreEqual(0, detents);
            Assert.AreEqual(180f, l.progress, 1e-4f);
        }

        [Test]
        public void DetentsBeforeLockInOneStep_AreCounted()
        {
            var l = New();
            l.Advance(100f);                                // ticks at 30, 60, 90
            var (detents, justLocked) = l.Advance(80f);     // 120, 150 tick; 180 locks
            Assert.AreEqual(2, detents);
            Assert.IsTrue(justLocked);
        }

        [Test]
        public void ResetOnPullOff_ClearsProgressAndLock()
        {
            var l = New();
            l.Advance(75f);
            l.Reset();
            Assert.AreEqual(0f, l.progress);
            Assert.IsFalse(l.locked);
            Assert.AreEqual(1, l.Advance(30f).detents, "ticks start again from zero");
        }
    }
}
