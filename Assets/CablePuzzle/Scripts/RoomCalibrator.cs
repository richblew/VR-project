using System.Globalization;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CablePuzzle
{
    /// <summary>
    /// Links the virtual room to the physical room with two floor marks.
    ///
    /// Physical: tape mark O and tape mark X on the floor (X is the other end of the baseline).
    /// Virtual:  Virtual_O and Virtual_X placed at the same two spots in the virtual room.
    ///
    /// Calibrate: touch the controller to mark O and press the trigger, then to mark X and press the trigger.
    /// The result moves the RoomAnchor (the parent of the XR Origin) so the two rooms line up, and is saved.
    /// Start a calibration with the start key (default C) or automatically on first run.
    /// </summary>
    public class RoomCalibrator : MonoBehaviour
    {
        [Header("Rig")]
        public Transform roomAnchor;                 // parent of the XR Origin (this is what gets moved)
        public Transform xrOrigin;                   // the XR Origin object (must sit at local zero under the anchor)

        [Header("Reference points (virtual twins of the tape marks)")]
        public Transform virtualPointA;              // mark O
        public Transform virtualPointB;              // mark X

        [Header("Input")]
        public Transform controllerTip;              // right controller
        public Vector3 tipOffset = Vector3.zero;     // offset from the controller to the point you hold over the mark
        public InputActionReference confirm;         // trigger; falls back to the right-hand trigger if empty
        public Key startKey = Key.C;                 // keyboard key that starts a calibration
        public bool startIfUncalibrated = true;

        [Header("Cables to reset after calibrating")]
        public VerletCable[] cables;

        const string PrefKey = "CablePuzzle.RoomCalibration.v1";
        enum Step { Idle, WaitForA, WaitForB }

        Step step = Step.Idle;
        Vector3 pointA;
        InputAction fallbackConfirm;

        void OnEnable()
        {
            if (confirm != null && confirm.action != null)
                confirm.action.Enable();
            else
            {
                fallbackConfirm = new InputAction("CalibrateConfirm", InputActionType.Button, "<XRController>{RightHand}/trigger");
                fallbackConfirm.Enable();
            }
        }

        void OnDisable()
        {
            if (fallbackConfirm != null)
            {
                fallbackConfirm.Disable();
                fallbackConfirm.Dispose();
                fallbackConfirm = null;
            }
        }

        void Start()
        {
            if (!ValidateReferences()) { enabled = false; return; }

            if (TryLoad())
                Debug.Log("[RoomCalibrator] Loaded saved room calibration. Press " + startKey + " to recalibrate.");
            else if (startIfUncalibrated)
                BeginCalibration();
        }

        void Update()
        {
            if (step == Step.Idle)
            {
                var kb = Keyboard.current;
                if (kb != null && kb[startKey].wasPressedThisFrame) BeginCalibration();
                return;
            }

            if (!ConfirmPressed()) return;

            Vector3 p = CurrentTip();
            if (step == Step.WaitForA)
            {
                pointA = p;
                step = Step.WaitForB;
                Debug.Log("[RoomCalibrator] Mark O recorded. Now touch mark X with the controller and press the trigger.");
            }
            else
            {
                if (Apply(pointA, p))
                {
                    Save(pointA, p);
                    Debug.Log("[RoomCalibrator] Calibration complete and saved.");
                }
                step = Step.Idle;
            }
        }

        /// <summary>Start (or restart) a calibration.</summary>
        public void BeginCalibration()
        {
            step = Step.WaitForA;
            Debug.Log("[RoomCalibrator] Calibration started. Touch mark O with the controller and press the trigger.");
        }

        bool ConfirmPressed()
        {
            var action = (confirm != null && confirm.action != null) ? confirm.action : fallbackConfirm;
            return action != null && action.WasPressedThisFrame();
        }

        // Position in tracking space (relative to the XR Origin), flattened to the floor.
        Vector3 CurrentTip()
        {
            Vector3 world = controllerTip.TransformPoint(tipOffset);
            Vector3 local = xrOrigin.InverseTransformPoint(world);
            local.y = 0f;
            return local;
        }

        bool Apply(Vector3 a, Vector3 b)
        {
            Vector3 vA = virtualPointA.position, vB = virtualPointB.position;
            Vector3 dl = b - a; dl.y = 0f;
            Vector3 dw = vB - vA; dw.y = 0f;

            if (dl.magnitude < 0.5f)
            {
                Debug.LogWarning("[RoomCalibrator] The two marks are less than 0.5 m apart. Calibration ignored.");
                return false;
            }

            float error = Mathf.Abs(dl.magnitude - dw.magnitude);
            if (error > 0.05f)
                Debug.LogWarning($"[RoomCalibrator] Measured spacing {dl.magnitude:F2} m but the virtual points are {dw.magnitude:F2} m apart " +
                                 $"({error * 100f:F0} cm difference). Check the tape marks and the Virtual_O / Virtual_X positions.");

            float yaw = Vector3.SignedAngle(dl, dw, Vector3.up);
            Quaternion rot = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 pos = vA - rot * a;
            pos.y = vA.y;                            // the virtual floor height comes from the reference point

            roomAnchor.SetPositionAndRotation(pos, rot);

            if (cables != null)
                foreach (var c in cables)
                    if (c != null) c.ResetRope();
            return true;
        }

        static void Save(Vector3 a, Vector3 b)
        {
            string s = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", a.x, a.z, b.x, b.z);
            PlayerPrefs.SetString(PrefKey, s);
            PlayerPrefs.Save();
        }

        bool TryLoad()
        {
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            var parts = PlayerPrefs.GetString(PrefKey).Split(',');
            if (parts.Length != 4) return false;
            float[] v = new float[4];
            for (int i = 0; i < 4; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
            // Stored as tracking-space points, so moving the virtual reference points later still works.
            return Apply(new Vector3(v[0], 0f, v[1]), new Vector3(v[2], 0f, v[3]));
        }

        bool ValidateReferences()
        {
            if (roomAnchor == null || xrOrigin == null || virtualPointA == null || virtualPointB == null || controllerTip == null)
            {
                Debug.LogError("[RoomCalibrator] Missing references (RoomAnchor, XR Origin, Virtual_O, Virtual_X or the controller). Run Tools > Cable Puzzle > Set Up Room Link.");
                return false;
            }
            return true;
        }

        void OnDrawGizmos()
        {
            if (virtualPointA != null) { Gizmos.color = Color.green; Gizmos.DrawWireSphere(virtualPointA.position, 0.12f); }
            if (virtualPointB != null) { Gizmos.color = Color.cyan; Gizmos.DrawWireSphere(virtualPointB.position, 0.12f); }
            if (virtualPointA != null && virtualPointB != null) { Gizmos.color = Color.white; Gizmos.DrawLine(virtualPointA.position, virtualPointB.position); }
        }
    }
}
