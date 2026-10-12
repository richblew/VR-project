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

        public AssemblyState State
        {
            get
            {
                if (submitted) return AssemblyState.Submitted;
                if (capAttached) return AssemblyState.PrototypeComplete;
                if (motorAttached) return AssemblyState.MotorFitted;
                if (airframeComplete) return AssemblyState.AirframeComplete;
                return AssemblyState.BuildingAirframe;
            }
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
