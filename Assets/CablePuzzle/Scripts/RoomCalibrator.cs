using System.Globalization;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;

namespace CablePuzzle
{
    /// <summary>
    /// Links the virtual room to the physical room.
    ///
    /// Physical: tape mark O and tape mark X on the floor, at two corners of the walkable area.
    /// Virtual:  Virtual_O and Virtual_X placed at the same two spots in the virtual room.
    ///
    /// Steps: (1) hold the controller on mark O and press the trigger, (2) the same on mark X,
    /// (3) stand in the middle of the walkable area and press the trigger.
    /// Step 3 tells the script which side of the O-X line the room is on, so the virtual walkable area
    /// can't end up mirrored onto the wrong side. The result moves the RoomAnchor and is saved.
    ///
    /// Start a calibration with the start key (default C, click the Game view first), the left controller's
    /// menu button, or automatically on the first run. Instructions appear in the headset and in the Console.
    /// </summary>
    public class RoomCalibrator : MonoBehaviour
    {
        [Header("Rig")]
        public Transform roomAnchor;                 // parent of the XR Origin (this is what gets moved)
        public Transform xrOrigin;                   // the XR Origin object (must sit at local zero under the anchor)
        public Transform head;                       // the XR camera; falls back to Camera.main

        [Header("Reference points (virtual twins of the tape marks)")]
        public Transform virtualPointA;              // mark O
        public Transform virtualPointB;              // mark X
        public WalkableZone zone;                    // optional, found automatically; its centre is used for step 3

        [Header("Input")]
        public Transform controllerTip;              // right controller
        public Vector3 tipOffset = Vector3.zero;     // offset from the controller to the point you hold over the mark
        public InputActionReference confirm;         // trigger; falls back to the right-hand trigger if empty
        public Key startKey = Key.C;                 // keyboard key that starts a calibration
        public bool startIfUncalibrated = true;

        [Header("Cables to reset after calibrating")]
        public VerletCable[] cables;

        const string PrefKey = "CablePuzzle.RoomCalibration.v2";
        const float MinMarkSpacing = 0.5f;
        const float MinCentreDistance = 0.3f;        // how far from the O-X line you must stand for step 3

        enum Step { Idle, WaitForA, WaitForB, WaitForCentre }

        Step step = Step.Idle;
        Vector3 pointA, pointB;
        Transform headT;
        InputAction fallbackConfirm;
        InputAction startAction;
        TextMeshPro prompt;
        float hidePromptAt;

        // Step 3 (stand in the middle) is only needed when the virtual centre is off the O-X line. If the marks' line
        // passes through the middle of the walkable area, the area looks the same from both sides, so it is skipped.
        bool UseCentre
        {
            get
            {
                if (zone == null || zone.corners == null || zone.corners.Length < 3 || headT == null) return false;
                return DistanceToLine(VirtualCentre(), virtualPointA.position, virtualPointB.position) >= MinCentreDistance;
            }
        }

        void OnEnable()
        {
            if (confirm != null && confirm.action != null)
                confirm.action.Enable();
            else
            {
                fallbackConfirm = new InputAction("CalibrateConfirm", InputActionType.Button, "<XRController>{RightHand}/trigger");
                fallbackConfirm.Enable();
            }

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

            if (zone == null) zone = FindFirstObjectByType<WalkableZone>();
            headT = head != null ? head : (Camera.main != null ? Camera.main.transform : null);
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

            int total = UseCentre ? 3 : 2;

            if (step == Step.WaitForA)
            {
                pointA = FloorPoint(controllerTip.TransformPoint(tipOffset));
                step = Step.WaitForB;
                Say($"Mark O recorded.\n\nCALIBRATION 2 of {total}\nHold the controller on floor mark X,\nthen press the trigger.");
            }
            else if (step == Step.WaitForB)
            {
                pointB = FloorPoint(controllerTip.TransformPoint(tipOffset));
                if ((pointB - pointA).magnitude < MinMarkSpacing)
                {
                    Say("The two marks are less than 0.5 m apart.\nStart again with the left menu button or " + startKey + ".", 6f);
                    step = Step.Idle;
                    return;
                }

                if (UseCentre)
                {
                    step = Step.WaitForCentre;
                    Say("Mark X recorded.\n\nCALIBRATION 3 of 3\nStand in the middle of the walkable area,\nthen press the trigger.");
                }
                else
                {
                    if (Apply(pointA, pointB, null)) { Save(pointA, pointB, null); Say("Calibration complete and saved.", 4f); }
                    step = Step.Idle;
                }
            }
            else // WaitForCentre
            {
                Vector3 centre = FloorPoint(headT.position);
                if (DistanceToLine(centre, pointA, pointB) < MinCentreDistance)
                {
                    Say("You are too close to the line between the marks.\nStand in the middle of the walkable area\nand press the trigger again.");
                    return;
                }
                if (Apply(pointA, pointB, centre)) { Save(pointA, pointB, centre); }
                step = Step.Idle;
            }
        }

        /// <summary>Start (or restart) a calibration.</summary>
        public void BeginCalibration()
        {
            int total = UseCentre ? 3 : 2;
            step = Step.WaitForA;
            Say($"CALIBRATION 1 of {total}\nHold the controller on floor mark O,\nthen press the trigger.");
        }

        bool ConfirmPressed()
        {
            var action = (confirm != null && confirm.action != null) ? confirm.action : fallbackConfirm;
            return action != null && action.WasPressedThisFrame();
        }

        // World position -> tracking space (relative to the XR Origin), flattened to the floor.
        Vector3 FloorPoint(Vector3 world)
        {
            Vector3 local = xrOrigin.InverseTransformPoint(world);
            local.y = 0f;
            return local;
        }

        static float Cross(Vector3 d, Vector3 e) => d.z * e.x - d.x * e.z;   // y component of d x e

        static float DistanceToLine(Vector3 p, Vector3 a, Vector3 b)
        {
            Vector3 d = b - a; d.y = 0f;
            return d.sqrMagnitude < 1e-6f ? 0f : Mathf.Abs(Cross(d, p - a)) / d.magnitude;
        }

        Vector3 VirtualCentre()
        {
            Vector3 sum = Vector3.zero;
            foreach (var c in zone.corners) sum += c.position;
            Vector3 mean = sum / zone.corners.Length;
            mean.y = 0f;
            return mean;
        }

        bool Apply(Vector3 a, Vector3 b, Vector3? centre)
        {
            Vector3 vA = virtualPointA.position, vB = virtualPointB.position;
            vA.y = vB.y = virtualPointA.position.y;
            bool reversed = false;

            // Which side of the O-X line is the room on? If the physical room and the virtual walkable area are on
            // opposite sides, use the marks in reverse order, which puts the virtual area on the physical room's side.
            if (centre.HasValue && UseCentre)
            {
                Vector3 vC = VirtualCentre();
                Vector3 vFlat = new Vector3(vA.x, 0f, vA.z);
                float physicalSide = Cross(b - a, centre.Value - a);
                float virtualSide = Cross(new Vector3(vB.x - vA.x, 0f, vB.z - vA.z), vC - vFlat);
                if (physicalSide * virtualSide < 0f)
                {
                    (vA, vB) = (vB, vA);
                    reversed = true;
                }
            }

            Vector3 dl = b - a; dl.y = 0f;
            Vector3 dw = vB - vA; dw.y = 0f;

            float error = Mathf.Abs(dl.magnitude - dw.magnitude);
            if (error > 0.05f)
                Debug.LogWarning($"[RoomCalibrator] Measured spacing {dl.magnitude:F2} m but the virtual points are {dw.magnitude:F2} m apart " +
                                 $"({error * 100f:F0} cm difference). Check the tape marks and the Virtual_O / Virtual_X positions.");

            float yaw = Vector3.SignedAngle(dl, dw, Vector3.up);
            Quaternion rot = Quaternion.AngleAxis(yaw, Vector3.up);
            Vector3 pos = vA - rot * a;
            pos.y = virtualPointA.position.y;        // the virtual floor height comes from the reference point

            roomAnchor.SetPositionAndRotation(pos, rot);

            if (cables != null)
                foreach (var c in cables)
                    if (c != null) c.ResetRope();

            string msg = "Calibration complete and saved.";
            if (reversed) msg += "\n(The room is on the other side of the marks,\nso they were used in reverse order.)";
            Say(msg, 6f);
            return true;
        }

        static void Save(Vector3 a, Vector3 b, Vector3? c)
        {
            string s = string.Format(CultureInfo.InvariantCulture, "{0},{1},{2},{3}", a.x, a.z, b.x, b.z);
            if (c.HasValue) s += string.Format(CultureInfo.InvariantCulture, ",{0},{1}", c.Value.x, c.Value.z);
            PlayerPrefs.SetString(PrefKey, s);
            PlayerPrefs.Save();
        }

        bool TryLoad()
        {
            if (!PlayerPrefs.HasKey(PrefKey)) return false;
            var parts = PlayerPrefs.GetString(PrefKey).Split(',');
            if (parts.Length != 4 && parts.Length != 6) return false;
            float[] v = new float[parts.Length];
            for (int i = 0; i < parts.Length; i++)
                if (!float.TryParse(parts[i], NumberStyles.Float, CultureInfo.InvariantCulture, out v[i])) return false;
            // Stored as tracking-space points, so moving the virtual reference points later still works.
            Vector3? centre = parts.Length == 6 ? new Vector3(v[4], 0f, v[5]) : (Vector3?)null;
            return Apply(new Vector3(v[0], 0f, v[1]), new Vector3(v[2], 0f, v[3]), centre);
        }

        // ---------- on-screen prompt ----------

        void CreatePrompt()
        {
            if (headT == null) return;

            var go = new GameObject("CalibrationPrompt");
            go.transform.SetParent(headT, false);
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
