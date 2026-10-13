using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit.Interactors;

namespace CablePuzzle
{
    /// <summary>
    /// Drives the "Free Pack - VR Hands (Rigged)" prefab (one skeleton, both hands under "Root").
    ///  - each hand's top bone (J_Left / J_Right) is moved onto the matching controller every frame
    ///  - fingers curl from the grip value; while an object is held, the hand closes to that
    ///    object's GrabPose.closure (or fully if it has none)
    /// The prefab's Animator is switched off, otherwise it would overwrite the bones.
    /// Put this on the root of ONE instance of the prefab (outside the controllers, e.g. under the Player).
    /// </summary>
    [DisallowMultipleComponent]
    public class VRHandRigDriver : MonoBehaviour
    {
        /// <summary>
        /// One thumb joint. At full grip it is rotated by angleA about axisA, then by angleB about axisB
        /// (both axes are in the joint's own local space). Two rotations let the base joint swing the
        /// thumb in two directions at once (e.g. across the palm AND around the side of an object).
        /// </summary>
        [System.Serializable]
        public class ThumbJoint
        {
            public Vector3 axisA = new Vector3(0, 0, 1);
            public float angleA = 30f;
            public Vector3 axisB = new Vector3(0, 1, 0);
            public float angleB = 0f;

            public ThumbJoint() { }
            public ThumbJoint(Vector3 axisA, float angleA, Vector3 axisB, float angleB)
            { this.axisA = axisA; this.angleA = angleA; this.axisB = axisB; this.angleB = angleB; }
        }

        [System.Serializable]
        public class Hand
        {
            public string side = "Left";              // "Left" or "Right" - used to find the bones
            public Transform controller;              // auto-found: "Left Controller" / "Right Controller"
            public Transform anchor;                  // auto-found: J_Left / J_Right
            [Tooltip("Optional. Move/rotate this object (a child of the controller) with Unity's gizmos to place the hand in ANY position and orientation. When set, the two offsets below are ignored. Use the component's context menu > 'Create Hand Targets'.")]
            public Transform target;
            public Vector3 positionOffset;            // in controller space (metres) - used only when no Target is set
            public Vector3 rotationOffset;            // in controller space (degrees) - used only when no Target is set
            public InputActionReference grip;         // float action, e.g. "XRI Left Interaction/Select Value" (enabled by XRI's Input Action Manager)
            public float curlSign = 1f;               // flip to -1 if this hand's FINGERS curl backwards (does not affect the thumb)

            [Tooltip("Thumb joints 1 (base), 2, 3. Each hand has its own settings. Joint 1 should do most of the work so the thumb swings in from its base.")]
            public ThumbJoint[] thumb =
            {
                new ThumbJoint(new Vector3(0, 0, 1), 40f, new Vector3(0, 1, 0), 25f),   // 1: base joint
                new ThumbJoint(new Vector3(0, 0, 1), 15f, new Vector3(0, 1, 0), 0f),    // 2
                new ThumbJoint(new Vector3(0, 0, 1), 20f, new Vector3(0, 1, 0), 0f),    // 3
            };

            [System.NonSerialized] public Transform palm;           // J_<side>_Hand: the point the hand is positioned/rotated around
            [System.NonSerialized] public Vector3 palmPosInAnchor;
            [System.NonSerialized] public Quaternion palmRotInAnchor;
            [System.NonSerialized] public Transform[][] bones;      // [finger][joint]
            [System.NonSerialized] public Quaternion[][] rest;
            [System.NonSerialized] public Vector3[][] autoAxes;   // curl axis in each bone's local space
            [System.NonSerialized] public XRBaseInteractor[] interactors;
            [System.NonSerialized] public float value;
        }

        public Hand left = new Hand { side = "Left", curlSign = -1f };
        public Hand right = new Hand { side = "Right", curlSign = 1f };

        [Tooltip("Position and rotate the hand around its palm/wrist bone (J_Left_Hand / J_Right_Hand) instead of the arm bone at the top of the rig. Turn off to get the old behaviour.")]
        public bool pivotAtPalm = true;

        [Header("Curl (applied to joints 1-3 of every finger; joint 4 is the tip and stays still)")]
        [Tooltip("Work out the finger curl axis from the skeleton (the line across the knuckles). If on, 'Finger Axis' is ignored. Thumbs are set separately, per hand, under Left / Right > Thumb.")]
        public bool autoFingerAxis = true;
        public Vector3 fingerAxis = new Vector3(0, 0, 1);       // manual local axis the finger joints bend around (when Auto is off)
        public Vector3 fingerAngles = new Vector3(65, 80, 55);  // degrees at full grip, joints 1, 2, 3
        public float speed = 12f;                               // open/close speed (units of grip per second)

        [Header("Testing")]
        public bool useTestValue;
        [Range(0f, 1f)] public float testGrip;

        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        void Awake()
        {
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;

            // The hand mesh's culling bounds are tied to the rig's Root, which never moves - only the hand bones do.
            // Without this, the hands vanish whenever the Root's old position leaves the camera's view.
            foreach (var smr in GetComponentsInChildren<SkinnedMeshRenderer>(true))
                smr.updateWhenOffscreen = true;
            Setup(left);
            Setup(right);
        }

        void Setup(Hand h)
        {
            if (h.anchor == null) h.anchor = FindDeep(transform, "J_" + h.side);
            if (h.controller == null) h.controller = FindController(h.side + " Controller");
            if (h.anchor == null) { Debug.LogWarning($"VRHandRigDriver: bone J_{h.side} not found", this); return; }

            h.palm = FindDeep(h.anchor, "J_" + h.side + "_Hand");
            if (h.palm != null)
            {
                h.palmPosInAnchor = h.anchor.InverseTransformPoint(h.palm.position);
                h.palmRotInAnchor = Quaternion.Inverse(h.anchor.rotation) * h.palm.rotation;
            }

            h.bones = new Transform[FingerNames.Length][];
            h.rest = new Quaternion[FingerNames.Length][];
            for (int f = 0; f < FingerNames.Length; f++)
            {
                h.bones[f] = new Transform[3];
                h.rest[f] = new Quaternion[3];
                for (int j = 0; j < 3; j++)
                {
                    var b = FindDeep(h.anchor, $"J_{h.side}_Hand{FingerNames[f]}{j + 1}");
                    h.bones[f][j] = b;
                    h.rest[f][j] = b != null ? b.localRotation : Quaternion.identity;
                    if (b == null) Debug.LogWarning($"VRHandRigDriver: bone J_{h.side}_Hand{FingerNames[f]}{j + 1} not found", this);
                }
            }
            // Auto axis: fingers curl about the line across the knuckles (index base -> pinky base),
            // expressed in each bone's own local space (so it is right whatever the bone's orientation is).
            h.autoAxes = new Vector3[FingerNames.Length][];
            var idx = h.bones[1][0]; var pnk = h.bones[4][0];
            Vector3 knuckle = (idx != null && pnk != null) ? (pnk.position - idx.position).normalized : Vector3.forward;
            for (int f = 0; f < FingerNames.Length; f++)
            {
                h.autoAxes[f] = new Vector3[3];
                for (int j = 0; j < 3; j++)
                    h.autoAxes[f][j] = h.bones[f][j] != null ? h.bones[f][j].InverseTransformDirection(knuckle).normalized : Vector3.forward;
            }

            h.interactors = h.controller != null
                ? h.controller.GetComponentsInChildren<XRBaseInteractor>(true)
                : new XRBaseInteractor[0];
            if (h.controller == null)
                Debug.LogWarning($"VRHandRigDriver: '{h.side} Controller' not found - the {h.side.ToLower()} hand will NOT follow. Drag it into the Controller field.", this);
            else
                Debug.Log($"VRHandRigDriver: {h.side} hand follows '{PathOf(h.controller)}'", this);
        }

        void LateUpdate()
        {
            Tick(left);
            Tick(right);
        }

        void Tick(Hand h)
        {
            if (h.anchor == null || h.bones == null) return;

            // 1. follow the controller (or the hand Target, if there is one)
            bool hasPose = true; Vector3 pos; Quaternion rot;
            if (h.target != null) { pos = h.target.position; rot = h.target.rotation; }
            else if (h.controller != null)
            {
                pos = h.controller.TransformPoint(h.positionOffset);
                rot = h.controller.rotation * Quaternion.Euler(h.rotationOffset);
            }
            else { hasPose = false; pos = default; rot = Quaternion.identity; }

            if (hasPose)
            {
                if (pivotAtPalm && h.palm != null)
                {
                    // place the rig's top bone so that the PALM bone ends up exactly at pos/rot
                    Quaternion aRot = rot * Quaternion.Inverse(h.palmRotInAnchor);
                    Vector3 aPos = pos - aRot * Vector3.Scale(h.palmPosInAnchor, h.anchor.lossyScale);
                    h.anchor.SetPositionAndRotation(aPos, aRot);
                }
                else h.anchor.SetPositionAndRotation(pos, rot);
            }

            // 2. work out how closed the hand should be
            float target = useTestValue ? testGrip
                         : (h.grip != null && h.grip.action != null ? h.grip.action.ReadValue<float>() : 0f);

            foreach (var i in h.interactors)
            {
                if (i == null || !i.hasSelection) continue;
                var held = i.firstInteractableSelected;
                var pose = held != null ? held.transform.GetComponentInParent<GrabPose>() : null;
                target = pose != null ? pose.closure : 1f;
                break;
            }

            h.value = Mathf.MoveTowards(h.value, target, speed * Time.deltaTime);

            // 3. curl the fingers
            for (int f = 0; f < h.bones.Length; f++)
            {
                bool thumb = f == 0;
                for (int j = 0; j < 3; j++)
                {
                    var b = h.bones[f][j];
                    if (b == null) continue;

                    if (thumb)
                    {
                        if (h.thumb == null || j >= h.thumb.Length) continue;
                        var tj = h.thumb[j];
                        b.localRotation = h.rest[f][j]
                                        * Turn(tj.axisA, tj.angleA * h.value)
                                        * Turn(tj.axisB, tj.angleB * h.value);
                    }
                    else
                    {
                        Vector3 axis = autoFingerAxis ? h.autoAxes[f][j] : fingerAxis.normalized;
                        b.localRotation = h.rest[f][j] * Turn(axis, fingerAngles[j] * h.value * h.curlSign);
                    }
                }
            }
        }

#if UNITY_EDITOR
        [ContextMenu("Create Hand Targets (move/rotate with gizmos)")]
        void CreateHandTargets()
        {
            CreateTarget(left);
            CreateTarget(right);
        }

        void CreateTarget(Hand h)
        {
            if (h.target != null) return;
            if (h.controller == null) h.controller = FindController(h.side + " Controller");
            if (h.controller == null)
            {
                Debug.LogWarning($"VRHandRigDriver: '{h.side} Controller' not found - assign the Controller field first.", this);
                return;
            }
            var go = new GameObject(h.side + " Hand Target");
            UnityEditor.Undo.RegisterCreatedObjectUndo(go, "Create hand target");
            go.transform.SetParent(h.controller, false);
            go.transform.localPosition = h.positionOffset;
            go.transform.localRotation = Quaternion.Euler(h.rotationOffset);
            UnityEditor.Undo.RecordObject(this, "Assign hand target");
            h.target = go.transform;
            UnityEditor.EditorUtility.SetDirty(this);
        }
#endif

        static Quaternion Turn(Vector3 axis, float degrees)
        {
            if (axis.sqrMagnitude < 1e-6f || Mathf.Approximately(degrees, 0f)) return Quaternion.identity;
            return Quaternion.AngleAxis(degrees, axis.normalized);
        }

        // Finds an object by name even if it is inactive, ignoring anything inside this hand rig.
        Transform FindController(string objectName)
        {
            Transform found = null;
            foreach (var tr in FindObjectsByType<Transform>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (tr.name != objectName || tr.IsChildOf(transform)) continue;
                if (found != null)
                {
                    Debug.LogWarning($"VRHandRigDriver: more than one '{objectName}' in the scene ('{PathOf(found)}' and '{PathOf(tr)}'). Using the first - assign the Controller field if that is wrong.", this);
                    continue;
                }
                found = tr;
            }
            return found;
        }

        static string PathOf(Transform t)
        {
            string s = t.name;
            while (t.parent != null) { t = t.parent; s = t.name + "/" + s; }
            return s;
        }

        static Transform FindDeep(Transform root, string name)
        {
            if (root.name == name) return root;
            for (int i = 0; i < root.childCount; i++)
            {
                var r = FindDeep(root.GetChild(i), name);
                if (r != null) return r;
            }
            return null;
        }
    }
}
