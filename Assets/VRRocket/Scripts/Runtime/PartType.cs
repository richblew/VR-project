namespace VRRocket
{
    /// <summary>The six component types of the rocket (SPEC.md section 4.1).</summary>
    public enum PartType
    {
        BodyTube,
        TailFin,
        WingFlap,
        NoseCone,
        Motor,
        MotorCap,
    }

    /// <summary>Where a part is in the guided attach mechanic (SPEC.md section 5.2).</summary>
    public enum PartState
    {
        Free,
        Guided,
        Attached,
    }
}
