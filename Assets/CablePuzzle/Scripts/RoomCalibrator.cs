using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CablePuzzle
{
    /// <summary>
    /// Links the virtual room to the physical room with two floor marks.
    ///
    /// Physical: tape mark O and tape mark X on the floor.
    /// Virtual:  Virtual_O and Virtual_X placed at the same two spots in the virtual room.
    ///
    /// Calibrate: hold the controller on mark O and press the trigger, then on mark X and press the trigger.
    /// The result moves the RoomAnchor (the parent of the XR Origin) so the two rooms line up, and is saved.
    ///
    /// Start a calibration with the start key (default C, click the Game view first), the left controller's
    /// menu button, or automatically on the first run. Instructions appear in the headset and in the Console.
    /// </summary>
    public class RoomCalibrator : MonoBehaviour
    {
        [Header("Rig")]
        public Transform roomAnchor;                 // parent of the XR Origin (this is what gets moved)
        public Transform xrOrigin;                   // the XR Origin object (must sit at local zero under the anchor)
        public Transform head;                       // the XR camera (for the on-screen prompt); falls back to Camera.main

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
        InputAction startAction;
        TextMeshPro prompt;
        float hidePromptAt;

        void OnEnable()
        {
            if (confirm != null && confirm.action != null)
                confirm.action.Enable();
            else
            {
                fallbackConfirm = new InputAction("CalibrateConfirm", InputActionType.Button, "<XRController>{RightHand}/trigger");
                fallbackConfirm.Enable();
            }

            // Optional controller shortcut to start a calibration: the left controller's menu button.
            startAction = new InputAction("CalibrateStart", InputActionType.Button);
            startAction.AddBinding("<XRController>{LeftHand}/{MenuButton}");
            startAction.Enable();
        }

        void OnDisable()
        {
            if (fallbackConfirm != null) { fallbackConfirm.Disable(); fallbackConfirm.Dispose(); fallbackConfirm = null; }
            if (startAction != null) { startAction.Disable(); startAction.Dispose(); startAction = null; }
        }

        void Start()
        {
            if (!ValidateReferences()) { enabled = false; return; }
            CreatePrompt();

            if (TryLoad())
                Say("Room calibration loaded.\nTo recalibrate: press " + startKey + " on the keyboard\n(click the Game view first) or the left menu button.", 6f);
            else if (startIfUncalibrated)
                BeginCalibration();
        }

        void Update()
        {
            if (prompt != null && prompt.gameObject.activeSelf && Time.time > hidePromptAt)
                prompt.gameObject.SetActive(false);

            if (step == Step.Idle)
            {
                var kb = Keyboard.current;
                bool keyPressed = kb != null && kb[startKey].wasPressedThisFrame;
                bool menuPressed = startAction != null && startAction.WasPressedThisFrame();
                if (keyPressed || menuPressed) BeginCalibration();
                return;
            }

            if (!ConfirmPressed()) return;

            Vector3 p = CurrentTip();
            if (step == Step.WaitForA)
            {
                pointA = p;
                step = Step.WaitForB;
                Say("Mark O recorded.\n\nCALIBRATION 2 of 2\nHold the controller on floor mark X,\nthen press the trigger.");
            }
            else
            {
                if (Apply(pointA, p))
                {
                    Save(pointA, p);
                    Say("Calibration complete and saved.", 4f);
                }
                step = Step.Idle;
            }
        }

        /// <summary>Start (or restart) a calibration.</summary>
        public void BeginCalibration()
        {
            step = Step.WaitForA;
            Say("CALIBRATION 1 of 2\nHold the controller on floor mark O,\nthen press the trigger.");
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
                Say("The two marks are less than 0.5 m apart.\nCalibration ignored. Start again.", 6f);
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

        // ---------- on-screen prompt ----------

        void CreatePrompt()
        {
            Transform h = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
            if (h == null) return;

            var go = new GameObject("CalibrationPrompt");
            go.transform.SetParent(h, false);
            go.transform.localPosition = new Vector3(0f, -0.05f, 0.9f);

            prompt = go.AddComponent<TextMeshPro>();
            prompt.alignment = TextAlignmentOptions.Center;
            prompt.fontSize = 0.5f;
            prompt.color = Color.white;
            prompt.rectTransform.sizeDelta = new Vector2(1.6f, 0.6f);

            var overlay = Shader.Find("TextMeshPro/Distance Field Overlay");   // draws on top of the scene
            if (overlay != null) prompt.fontMaterial.shader = overlay;

            go.SetActive(false);
        }

        void Say(string message, float seconds = 0f)
        {
            Debug.Log("[RoomCalibrator] " + message.Replace("\n", " "));
            if (prompt == null) return;
            prompt.text = message;
            prompt.gameObject.SetActive(true);
            hidePromptAt = seconds > 0f ? Time.time + seconds : float.MaxValue;
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
