using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The opening of the window wall that replaces the room's right wall: where it is and where the mullions go. Data only; the
    /// Editor menu VR Rocket > Rebuild Window Wall regenerates the wall mesh, colliders, glass and frame from it and from the room's
    /// wall (open room.unity additively first).
    /// </summary>
    public sealed class WindowOpening : MonoBehaviour
    {
        [Tooltip("Hierarchy path of the room's wall this replaces")]
        public string roomWallPath = "Environment/Floorplan/Right wall";
        [Tooltip("World z range of the opening")] public Vector2 zRange = new Vector2(-6.0f, -0.5f);
        [Tooltip("World y range of the opening (sill to head)")] public Vector2 yRange = new Vector2(0.9f, 3.3f);
        [Tooltip("World z of each mullion. Keep them off the sightline from the console to the pad (about z -2.2).")]
        public float[] mullionZ = { -4.6f, -3.2f };
    }
}
