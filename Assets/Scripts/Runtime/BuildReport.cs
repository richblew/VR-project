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
        public int wrongFlapCount;              // flaps in the wrong place or upside down: one veers off course, more break up

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
                report.wrongFlapCount++;
                failed.Add("WingFlap_" + f.flapId);
            }
            report.FlapError = anyFlapWrong;
            if (!capLocked) report.outcome = LaunchOutcome.MotorRetentionLoss;
            else report.outcome = anyFlapWrong ? LaunchOutcome.UnstableFlight : LaunchOutcome.Success;
            report.failedComponents = failed.ToArray();
            report.FailedCount = report.failedComponents.Length;
            return report;
        }

        /// <summary>A failed component as the player reads it: the kit's motor is a battery, so "MotorCap" is "Battery cap", "WingFlap_2" "Wing flap 2".</summary>
        public static string DisplayName(string component)
        {
            if (component == "MotorCap") return "Battery cap";
            if (component == "Motor") return "Battery";
            if (component != null && component.StartsWith("WingFlap_")) return "Wing flap " + component.Substring("WingFlap_".Length);
            return component;
        }

        public static string[] DisplayNames(string[] components) => Array.ConvertAll(components ?? Array.Empty<string>(), DisplayName);

        /// <summary>What the flight did, for the result screens' second line: how an unstable flight failed, or where the battery went.</summary>
        public string FlightDetail
        {
            get
            {
                switch (outcome)
                {
                    case LaunchOutcome.UnstableFlight: return wrongFlapCount <= 1 ? "VEERED OFF COURSE AND CRASHED" : "BROKE UP IN FLIGHT";
                    case LaunchOutcome.MotorRetentionLoss: return "BATTERY BLEW OUT ON THE PAD";
                    default: return "NOMINAL FLIGHT";
                }
            }
        }
    }
}
