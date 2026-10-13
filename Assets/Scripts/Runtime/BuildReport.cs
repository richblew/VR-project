using System;
using System.Collections.Generic;

namespace VRRocket
{
    // SPEC.md section 7. Plain C#, no Unity dependencies, so it is unit-testable.

    public enum FlapPosition { Base, Midpoint }      // Base = a Low slot
    public enum FlapOrientation { Up, Down }
    public enum LaunchOutcome { Success, MotorRetentionLoss, UnstableFlight }

    [Serializable]
    public struct FlapPlacement
    {
        public int flapId;            // 1 to 3
        public string slotName;       // e.g. "FlapSlot_Low_2"
        public FlapPosition position;
        public FlapOrientation orientation;
        public bool IsCorrect => position == FlapPosition.Midpoint && orientation == FlapOrientation.Up;
    }

    [Serializable]
    public sealed class BuildReport
    {
        public FlapPlacement[] flapPlacement = Array.Empty<FlapPlacement>();   // always 3 entries once complete
        public bool capLocked;
        public bool FlapError;                  // any flap not correct
        public LaunchOutcome outcome;
        public string[] failedComponents = Array.Empty<string>();       // e.g. { "MotorCap", "WingFlap_2" }
        public int FailedCount;                 // failedComponents.Length

        /// <summary>Applies the outcome rule of SPEC.md section 7.2 and fills the derived fields.</summary>
        public static BuildReport Build(FlapPlacement[] flaps, bool capLocked)
        {
            if (flaps == null) throw new ArgumentNullException(nameof(flaps));
            var report = new BuildReport { flapPlacement = flaps, capLocked = capLocked };
            var failed = new List<string>();
            if (!capLocked) failed.Add("MotorCap");
            var anyFlapWrong = false;
            foreach (var f in flaps)
            {
                if (f.IsCorrect) continue;
                anyFlapWrong = true;
                failed.Add("WingFlap_" + f.flapId);
            }
            report.FlapError = anyFlapWrong;
            if (!capLocked) report.outcome = LaunchOutcome.MotorRetentionLoss;
            else report.outcome = anyFlapWrong ? LaunchOutcome.UnstableFlight : LaunchOutcome.Success;
            report.failedComponents = failed.ToArray();
            report.FailedCount = report.failedComponents.Length;
            return report;
        }
    }
}
