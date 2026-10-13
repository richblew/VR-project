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
        [System.Serializable]
        public class Hand
        {
            public string side = "Left";              // "Left" or "Right" - used to find the bones
            public Transform controller;              // auto-found: "Left Controller" / "Right Controller"
            public Transform anchor;                  // auto-found: J_Left / J_Right
            public Vector3 positionOffset;            // in controller space (metres)
            public Vector3 rotationOffset;            // in controller space (degrees)
            public InputActionReference grip;         // float action, e.g. "XRI Left Interaction/Select Value" (enabled by XRI's Input Action Manager)
            public float curlSign = 1f;               // flip to -1 if this hand curls backwards

            [System.NonSerialized] public Transform[][] bones;      // [finger][joint]
            [System.NonSerialized] public Quaternion[][] rest;
            [System.NonSerialized] public XRBaseInteractor[] interactors;
            [System.NonSerialized] public float value;
        }

        public Hand left = new Hand { side = "Left", curlSign = 1f };
        public Hand right = new Hand { side = "Right", curlSign = -1f };

        [Header("Curl (applied to joints 1-3 of every finger; joint 4 is the tip and stays still)")]
        public Vector3 fingerAxis = new Vector3(0, 0, 1);       // local axis the finger joints bend around
        public Vector3 fingerAngles = new Vector3(65, 80, 55);  // degrees at full grip, joints 1, 2, 3
        public Vector3 thumbAxis = new Vector3(0, 0, 1);
        public Vector3 thumbAngles = new Vector3(20, 35, 35);
        public float speed = 12f;                               // open/close speed (units of grip per second)

        [Header("Testing")]
        public bool useTestValue;
        [Range(0f, 1f)] public float testGrip;

        static readonly string[] FingerNames = { "Thumb", "Index", "Middle", "Ring", "Pinky" };

        void Awake()
        {
            var anim = GetComponent<Animator>();
            if (anim != null) anim.enabled = false;
            Setup(left);
            Setup(right);
        }

        void Setup(Hand h)
        {
            if (h.anchor == null) h.anchor = FindDeep(transform, "J_" + h.side);
            if (h.controller == null)
            {
                var go = GameObject.Find(h.side + " Controller");
                if (go != null) h.controller = go.transform;
            }
            if (h.anchor == null) { Debug.LogWarning($"VRHandRigDriver: bone J_{h.side} not found", this); return; }

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
            h.interactors = h.controller != null
                ? h.controller.GetComponentsInChildren<XRBaseInteractor>(true)
                : new XRBaseInteractor[0];
            if (h.controller == null) Debug.LogWarning($"VRHandRigDriver: '{h.side} Controller' not found - assign it", this);
        }

        void LateUpdate()
        {
            Tick(left);
            Tick(right);
        }

        void Tick(Hand h)
        {
            if (h.anchor == null || h.bones == null) return;

            // 1. follow the controller
            if (h.controller != null)
            {
                h.anchor.SetPositionAndRotation(
                    h.controller.TransformPoint(h.positionOffset),
                    h.controller.rotation * Quaternion.Euler(h.rotationOffset));
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
                Vector3 axis = (thumb ? thumbAxis : fingerAxis).normalized;
                Vector3 ang = thumb ? thumbAngles : fingerAngles;
                for (int j = 0; j < 3; j++)
                {
                    var b = h.bones[f][j];
                    if (b == null) continue;
                    b.localRotation = h.rest[f][j] * Quaternion.AngleAxis(ang[j] * h.value * h.curlSign, axis);
                }
            }
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
