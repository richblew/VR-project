namespace VRRocket
{
    public enum AssemblyState { BuildingAirframe, AirframeComplete, MotorFitted, PrototypeComplete, Submitted }

    /// <summary>
    /// The assembly states and gating of SPEC.md section 5.5, free of Unity dependencies so it can be unit-tested (12.1).
    /// Feed it what is attached; ask it the state, which points accept, and which parts may be removed.
    /// </summary>
    public sealed class AssemblyStateMachine
    {
        public bool enforceOrder = true;

        public int finsAttached { get; private set; }
        public int flapsAttached { get; private set; }
        public bool noseAttached { get; private set; }
        public bool motorAttached { get; private set; }
        public bool capAttached { get; private set; }
        public bool submitted { get; private set; }

        public bool airframeComplete => finsAttached >= 3 && flapsAttached >= 3 && noseAttached;

        /// <summary>
        /// The next thing the rocket needs. PrototypeComplete means all nine parts (3 fins, 3 flaps, the nose, the battery, the
        /// cap); with the free build order a battery and cap on an unfinished airframe is still BuildingAirframe.
        /// </summary>
        public AssemblyState State
        {
            get
            {
                if (submitted) return AssemblyState.Submitted;
                if (!airframeComplete) return AssemblyState.BuildingAirframe;
                if (capAttached && motorAttached) return AssemblyState.PrototypeComplete;
                if (motorAttached) return AssemblyState.MotorFitted;
                return AssemblyState.AirframeComplete;
            }
        }

        /// <summary>All nine parts are on the rocket (the cap may still be unlocked: that is a launch failure, not a missing part).</summary>
        public bool complete => airframeComplete && motorAttached && capAttached;

        /// <summary>What the rocket still needs, as the player reads it, one item per entry ("2 fins", "the nose cone"...).</summary>
        public System.Collections.Generic.List<string> MissingParts()
        {
            var list = new System.Collections.Generic.List<string>();
            var fins = 3 - finsAttached;
            var flaps = 3 - flapsAttached;
            if (fins > 0) list.Add(fins + (fins == 1 ? " fin" : " fins"));
            if (flaps > 0) list.Add(flaps + (flaps == 1 ? " flap" : " flaps"));
            if (!noseAttached) list.Add("the nose cone");
            if (!motorAttached) list.Add("the battery");
            if (!capAttached) list.Add("the battery cap");
            return list;
        }

        /// <summary>The missing parts in one line: "2 fins, the nose cone and the battery".</summary>
        public string DescribeMissing()
        {
            var list = MissingParts();
            if (list.Count == 0) return "nothing";
            if (list.Count == 1) return list[0];
            return string.Join(", ", list.GetRange(0, list.Count - 1)) + " and " + list[list.Count - 1];
        }

        /// <summary>Record a part seating or coming off. Returns true when the state changed.</summary>
        public bool Apply(PartType type, bool attached)
        {
            var before = State;
            switch (type)
            {
                case PartType.TailFin: finsAttached += attached ? 1 : -1; break;
                case PartType.WingFlap: flapsAttached += attached ? 1 : -1; break;
                case PartType.NoseCone: noseAttached = attached; break;
                case PartType.Motor: motorAttached = attached; break;
                case PartType.MotorCap: capAttached = attached; break;
            }
            if (finsAttached < 0) finsAttached = 0;
            if (flapsAttached < 0) flapsAttached = 0;
            return State != before;
        }

        public void SetSubmitted(bool value) => submitted = value;

        public void Reset()
        {
            finsAttached = 0; flapsAttached = 0; noseAttached = false; motorAttached = false; capAttached = false; submitted = false;
        }

        /// <summary>Does an attach point that accepts this part type currently accept anything? A refused point glows and snaps nothing.</summary>
        public bool Allows(PartType accepts)
        {
            if (submitted) return false;
            switch (accepts)
            {
                case PartType.TailFin:
                case PartType.WingFlap:
                case PartType.NoseCone:
                    return !enforceOrder || !motorAttached;
                case PartType.Motor:
                    return !enforceOrder || airframeComplete;
                case PartType.MotorCap:
                    return motorAttached;          // the only rule that survives with enforceOrder off
                case PartType.BodyTube:
                    return true;                   // the stand always takes the tube back
                default:
                    return false;
            }
        }

        /// <summary>May an attached part of this type be pulled off? The cap's lock is handled by the twist itself.</summary>
        public bool CanRemove(PartType type)
        {
            if (submitted) return false;
            switch (type)
            {
                case PartType.TailFin:
                case PartType.WingFlap:
                case PartType.NoseCone:
                    return !enforceOrder || !motorAttached;   // airframe parts stay once the motor is seated
                case PartType.Motor:
                    return !capAttached;                      // the motor cannot come out while the cap is seated
                case PartType.MotorCap:
                    return true;
                case PartType.BodyTube:
                    return State >= AssemblyState.PrototypeComplete;   // the stand releases the finished prototype
                default:
                    return false;
            }
        }
    }
}
