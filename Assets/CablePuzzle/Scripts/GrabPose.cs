using UnityEngine;

namespace CablePuzzle
{
    /// <summary>
    /// Optional. Put on a grabbable object (plug, receptacle, any later model) to say how
    /// closed the fingers should be while it is held. 0 = open hand, 1 = full fist.
    /// Objects without this component make the hand close fully.
    /// </summary>
    public class GrabPose : MonoBehaviour
    {
        [Range(0f, 1f)] public float closure = 0.8f;
    }
}
