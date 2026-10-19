using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The launch outside the window: a full-size vehicle on the launch mount, engine fire and smoke, a pad camera for the big
    /// screen, and the outcomes of SPEC 7.2 driven by <see cref="RocketFlightModel"/>. Built as a working version of the teammates'
    /// launch screen (SPEC 9); everything lives under the LaunchSite object in the scene so it can be replaced wholesale.
    /// <list type="bullet">
    /// <item>Ignition (T-3): the flame lights small at the nozzle and builds; pad smoke starts rolling out along the ground.</item>
    /// <item>Liftoff (T-0): full flame, a surge of pad smoke, and an exhaust trail left in the air behind the climbing rocket.</item>
    /// <item>A crash or break-up: the explosion (with its ground group on a crash), and the fins, flaps, nose and cap flung out
    /// as debris that falls trailing smoke.</item>
    /// </list>
    /// </summary>
    public sealed class LaunchSequence : MonoBehaviour
    {
        [Header("Pad")]
        [SerializeField] Transform m_PadOrigin;
        [SerializeField] GameObject m_Vehicle;
        [SerializeField, Tooltip("The motor mesh inside the vehicle, animated out of the base on a retention-loss failure.")] Transform m_VehicleMotor;
        [SerializeField, Tooltip("The motor cap mesh, which leaves with the motor.")] Transform m_VehicleCap;
        [SerializeField, Tooltip("World height of the ground around the launch mount: where an off-course rocket crashes.")] float m_GroundLevel = 0f;
        [Header("Effects")]
        [SerializeField, Tooltip("The flame core at the nozzle (local space, moves with the vehicle).")] ParticleSystem m_Flame;
        [SerializeField, Tooltip("Pad smoke: rolls out along the ground from under the launch mount.")] ParticleSystem m_Smoke;
        [SerializeField] ParticleSystem m_Sparks;
        [SerializeField] ParticleSystem m_PadSteam;
        [SerializeField, Tooltip("The bright glow at the nozzle.")] ParticleSystem m_NozzleGlow;
        [SerializeField, Tooltip("Smoke left in the air behind the climbing rocket (world space, emitted over distance).")] ParticleSystem m_ExhaustTrail;
        [SerializeField, Tooltip("Fire spraying out sideways from under the launch mount while the nozzle is close to it.")] ParticleSystem m_PadFire;
        [SerializeField] GameObject m_ExplosionPrefab;
        [SerializeField, Tooltip("Smoke trail added to each piece of debris.")] ParticleSystem m_DebrisTrail;
        [SerializeField, Tooltip("Vehicle meshes flung out as debris when it explodes.")] Renderer[] m_DebrisParts = Array.Empty<Renderer>();
        [Header("Staging")]
        [SerializeField, Tooltip("Flame strength when it lights at T-3 (0 to 1); it builds to Liftoff Flame Level by T-0.")] float m_IgnitionLevel = 0.3f;
        [SerializeField] float m_PreLiftoffLevel = 0.75f;
        [SerializeField, Tooltip("Seconds the flame takes to build from ignition level.")] float m_IgnitionRampSeconds = 2.6f;
        [SerializeField, Tooltip("Pad smoke particles per second just before liftoff.")] float m_PadSmokeIgnitionRate = 35f;
        [SerializeField, Tooltip("Pad smoke particles per second at liftoff.")] float m_PadSmokeLiftoffRate = 90f;
        [SerializeField, Tooltip("Pad smoke tapers to nothing as the rocket climbs to this altitude.")] float m_PadSmokeFadeAltitude = 60f;
        [SerializeField, Tooltip("Pad fire particles per second at full thrust on the mount.")] float m_PadFireRate = 200f;
        [SerializeField, Tooltip("Pad fire dies away as the nozzle climbs to this altitude.")] float m_PadFireFadeAltitude = 14f;
        [SerializeField, Tooltip("Seconds the flight lingers after an explosion before the result.")] float m_AfterExplosionSeconds = 2.5f;
        [SerializeField, Tooltip("A crash's fireball sits this far above the impact point (its Ground group stays on the ground).")] float m_CrashLift = 4.5f;
        [SerializeField, Tooltip("A crash happens further from the window than a break-up: its explosion is drawn this much bigger.")] float m_CrashScale = 1.5f;
        [Header("Audio")]
        [SerializeField] AudioSource m_EngineLoop;
        [SerializeField] AudioSource m_OneShot;
        [SerializeField] AudioClip m_IgnitionClip;
        [SerializeField] AudioClip m_ExplosionClip;
        [SerializeField] AudioClip m_ExplosionLowClip;
        [Header("Pad camera")]
        [SerializeField] Camera m_PadCamera;
        [SerializeField] Vector3 m_PadCameraOffset = new Vector3(55f, 6f, -35f);
        [Header("Flight tuning")]
        [SerializeField] float m_SuccessAltitude = 1200f;
        [SerializeField] float m_VisualScale = 1f;
        [SerializeField, Tooltip("Scale the vehicle starts from when it arrives on the pad and grows to full size: the model's 1/36, so it begins at prototype size.")]
        float m_ArrivalScale = 1f / 36f;
        [SerializeField, Tooltip("World direction an off-course rocket veers to (0 = +Z, 90 = +X): chosen so the crash is in the window's view.")]
        float m_OffCourseAzimuth = 70f;

        public RocketFlightModel model { get; } = new RocketFlightModel();
        public bool isRunning { get; private set; }
        public float altitude => model.altitude;
        public float speed => model.speed;
        /// <summary>The explosion of the last flight, while it is still playing.</summary>
        public GameObject activeExplosion => m_ActiveExplosion;
        /// <summary>Debris of the last flight, while it is still falling.</summary>
        public IReadOnlyList<GameObject> debris => m_Debris;
        /// <summary>Current flame strength, 0 (out) to 1 (full thrust).</summary>
        public float flameLevel { get; private set; }
        public ParticleSystem flame => m_Flame;
        public ParticleSystem padSmoke => m_Smoke;
        public ParticleSystem exhaustTrail => m_ExhaustTrail;
        public ParticleSystem padFire => m_PadFire;
        public GameObject vehicle => m_Vehicle;

        Vector3 m_MotorLocalPos, m_MotorLocalScale = Vector3.one;
        Quaternion m_MotorLocalRot;
        Vector3 m_CapLocalPos, m_CapLocalScale = Vector3.one;
        Quaternion m_CapLocalRot = Quaternion.identity;
        Transform m_MotorParent;
        Transform m_CapParent;
        Coroutine m_Flight;
        Coroutine m_Ignition;
        GameObject m_ActiveExplosion;
        readonly List<GameObject> m_Debris = new List<GameObject>();
        float m_FlameRate, m_FlameSize, m_FlameSpeed, m_GlowSize;

        public void Configure(Transform padOrigin, GameObject vehicle, Transform motor, Transform cap, ParticleSystem flame, ParticleSystem smoke, ParticleSystem sparks, ParticleSystem steam, GameObject explosionPrefab, AudioSource engine, AudioSource oneShot, AudioClip ignition, AudioClip explosion, AudioClip explosionLow, Camera padCamera)
        {
            m_PadOrigin = padOrigin; m_Vehicle = vehicle; m_VehicleMotor = motor; m_VehicleCap = cap;
            m_Flame = flame; m_Smoke = smoke; m_Sparks = sparks; m_PadSteam = steam; m_ExplosionPrefab = explosionPrefab;
            m_EngineLoop = engine; m_OneShot = oneShot; m_IgnitionClip = ignition; m_ExplosionClip = explosion; m_ExplosionLowClip = explosionLow; m_PadCamera = padCamera;
        }

        /// <summary>The effects added with the launch mount (nozzle glow, exhaust trail, debris).</summary>
        public void ConfigureEffects(ParticleSystem nozzleGlow, ParticleSystem exhaustTrail, ParticleSystem debrisTrail, Renderer[] debrisParts, float groundLevel)
        {
            m_NozzleGlow = nozzleGlow; m_ExhaustTrail = exhaustTrail; m_DebrisTrail = debrisTrail;
            m_DebrisParts = debrisParts ?? Array.Empty<Renderer>(); m_GroundLevel = groundLevel;
        }

        Vector3 m_FlameLocalPos;
        Quaternion m_FlameLocalRot;
        Vector3 m_VehicleScale = Vector3.one;
        Coroutine m_Grow;

        /// <summary>True while the vehicle is still growing from prototype size to full size on the pad.</summary>
        public bool isGrowing => m_Grow != null;
        /// <summary>The engine is lit: flame emitting or the engine sound playing.</summary>
        public bool isIgnited => (m_Flame != null && m_Flame.isEmitting) || (m_EngineLoop != null && m_EngineLoop.isPlaying);

        void Awake()
        {
            if (m_Vehicle != null) m_VehicleScale = m_Vehicle.transform.localScale;
            if (m_VehicleMotor != null) { m_MotorParent = m_VehicleMotor.parent; m_MotorLocalPos = m_VehicleMotor.localPosition; m_MotorLocalRot = m_VehicleMotor.localRotation; m_MotorLocalScale = m_VehicleMotor.localScale; }
            if (m_VehicleCap != null) { m_CapParent = m_VehicleCap.parent; m_CapLocalPos = m_VehicleCap.localPosition; m_CapLocalRot = m_VehicleCap.localRotation; m_CapLocalScale = m_VehicleCap.localScale; }
            CacheVehicle();   // each part's own point and scale, as built, before any flight can move them
            if (m_Flame != null)
            {
                m_FlameLocalPos = m_Flame.transform.localPosition; m_FlameLocalRot = m_Flame.transform.localRotation;
                m_FlameRate = m_Flame.emission.rateOverTimeMultiplier;
                m_FlameSize = m_Flame.main.startSizeMultiplier;
                m_FlameSpeed = m_Flame.main.startSpeedMultiplier;
            }
            if (m_NozzleGlow != null) m_GlowSize = m_NozzleGlow.main.startSizeMultiplier;
            if (m_DebrisTrail != null) m_DebrisTrail.gameObject.SetActive(false);
            StopEffects();
            if (m_PadCamera != null) m_PadCamera.enabled = false;
        }

        /// <summary>Empty pad: no vehicle, no effects, camera off.</summary>
        public void ResetPad()
        {
            if (m_Flight != null) { StopCoroutine(m_Flight); m_Flight = null; }
            if (m_Grow != null) { StopCoroutine(m_Grow); m_Grow = null; }
            isRunning = false;
            StopEffects();
            if (m_ActiveExplosion != null) Destroy(m_ActiveExplosion);
            ClearDebris();
            // A blown-out battery was taken off the 36x vehicle keeping its world size (its local scale became 36): put back its
            // whole local pose, scale included, or the next launch shows it 36 x 36 times its size.
            if (m_VehicleMotor != null && m_MotorParent != null) { m_VehicleMotor.SetParent(m_MotorParent, false); m_VehicleMotor.SetLocalPositionAndRotation(m_MotorLocalPos, m_MotorLocalRot); m_VehicleMotor.localScale = m_MotorLocalScale; }
            if (m_VehicleCap != null && m_CapParent != null) { m_VehicleCap.SetParent(m_CapParent, false); m_VehicleCap.SetLocalPositionAndRotation(m_CapLocalPos, m_CapLocalRot); m_VehicleCap.localScale = m_CapLocalScale; }
            if (m_Flame != null) { m_Flame.transform.localPosition = m_FlameLocalPos; m_Flame.transform.localRotation = m_FlameLocalRot; }
            if (m_Vehicle != null)
            {
                m_Vehicle.transform.SetPositionAndRotation(m_PadOrigin != null ? m_PadOrigin.position : m_Vehicle.transform.position, m_PadOrigin != null ? m_PadOrigin.rotation : Quaternion.identity);
                m_Vehicle.transform.localScale = m_VehicleScale;
                m_Vehicle.SetActive(false);
            }
            if (m_PadCamera != null) m_PadCamera.enabled = false;
            model.Reset();
        }

        /// <summary>
        /// The prototype arrives on the pad. With <paramref name="growSeconds"/> above zero it appears at prototype size and grows to
        /// full size over that time, so "scaling for launch" is something the player sees, through the window and on the pad feed.
        /// </summary>
        public void PlaceVehicleOnPad(float growSeconds = 0f)
        {
            ResetPad();
            ApplyBuild();
            if (m_Vehicle != null)
            {
                m_Vehicle.SetActive(true);
                if (growSeconds > 0f) m_Grow = StartCoroutine(Grow(growSeconds));
            }
            UpdatePadCamera(0f);
            if (m_PadCamera != null) m_PadCamera.enabled = true;
        }

        // ---- the vehicle is built the way the player built the prototype ----

        [Header("Matching the build")]
        [SerializeField, Tooltip("How far an unlocked cap sits out of the base, in the model rocket's metres (the assembly's Cap Unlocked Gap)")]
        float m_UnlockedCapGap = 0.002f;

        BuildReport m_Build;
        Dictionary<string, Transform> m_VehiclePoints;
        Dictionary<string, Transform> m_VehicleParts;
        Dictionary<Transform, Transform> m_DefaultPoint;
        Dictionary<Transform, Vector3> m_DefaultScale;

        /// <summary>
        /// The next vehicle on the pad is built from this report: each fin, flap, nose, battery and cap on the point the player put
        /// it on (a flap upside down where it went on upside down), parts the rocket did not have left off, an unlocked cap
        /// sitting out of the base. Null: the correctly built rocket.
        /// </summary>
        public void MatchBuild(BuildReport report) => m_Build = report;

        /// <summary>Which point a vehicle part is on now (its parent's name), and whether it is shown and flipped: for tests.</summary>
        public bool TryGetVehiclePart(string partName, out string pointName, out bool shown, out bool upsideDown)
        {
            CacheVehicle();
            pointName = null; shown = false; upsideDown = false;
            if (m_VehicleParts == null || !m_VehicleParts.TryGetValue(partName, out var t)) return false;
            pointName = t.parent != null ? t.parent.name : null;
            shown = t.gameObject.activeSelf;
            upsideDown = Vector3.Dot(t.up, t.parent != null ? t.parent.up : Vector3.up) < 0f;
            return true;
        }

        void CacheVehicle()
        {
            if (m_VehicleParts != null || m_Vehicle == null) return;
            Transform points = null;
            foreach (var t in m_Vehicle.GetComponentsInChildren<Transform>(true)) if (t.name == "AttachPoints") { points = t; break; }
            if (points == null) return;
            m_VehiclePoints = new Dictionary<string, Transform>();
            m_VehicleParts = new Dictionary<string, Transform>();
            m_DefaultPoint = new Dictionary<Transform, Transform>();
            m_DefaultScale = new Dictionary<Transform, Vector3>();
            foreach (Transform point in points)
            {
                m_VehiclePoints[point.name] = point;
                foreach (Transform part in point)
                {
                    m_VehicleParts[part.name] = part;
                    m_DefaultPoint[part] = point;
                    m_DefaultScale[part] = part.localScale;
                }
            }
        }

        static string VehiclePartName(BuiltPart p)
        {
            switch (p.type)
            {
                case PartType.TailFin: return "TailFin_" + p.partId;
                case PartType.WingFlap: return "WingFlap_" + p.partId;
                case PartType.NoseCone: return "NoseCone_" + Mathf.Max(1, p.partId);
                case PartType.Motor: return "Motor_" + Mathf.Max(1, p.partId);
                case PartType.MotorCap: return "MotorCap_" + Mathf.Max(1, p.partId);
                default: return null;
            }
        }

        void ApplyBuild()
        {
            CacheVehicle();
            if (m_VehicleParts == null) return;
            var parts = m_Build != null ? m_Build.parts : null;
            if (parts == null || parts.Length == 0)
            {
                // no report: every part on its own point, the right way up
                foreach (var kv in m_DefaultPoint) Place(kv.Key, kv.Value, false, 0f);
                return;
            }
            foreach (var kv in m_VehicleParts) kv.Value.gameObject.SetActive(false);
            foreach (var p in parts)
            {
                var name = VehiclePartName(p);
                if (name == null || !m_VehicleParts.TryGetValue(name, out var part) || !m_VehiclePoints.TryGetValue(p.pointName ?? "", out var point)) continue;
                var gap = p.type == PartType.MotorCap && !m_Build.capLocked ? m_UnlockedCapGap : 0f;
                Place(part, point, p.upsideDown, gap);
            }
        }

        void Place(Transform part, Transform point, bool upsideDown, float gapDown)
        {
            part.SetParent(point, false);
            if (m_DefaultScale != null && m_DefaultScale.TryGetValue(part, out var scale)) part.localScale = scale;   // never carry a scale over from an earlier flight
            part.localPosition = Vector3.down * gapDown;
            part.localRotation = upsideDown ? Quaternion.Euler(0f, 0f, 180f) : Quaternion.identity;
            part.gameObject.SetActive(true);
        }

        IEnumerator Grow(float seconds)
        {
            var t = m_Vehicle.transform;
            var from = m_VehicleScale * m_ArrivalScale;
            for (var elapsed = 0f; elapsed < seconds; elapsed += Time.deltaTime)
            {
                t.localScale = Vector3.LerpUnclamped(from, m_VehicleScale, Mathf.SmoothStep(0f, 1f, elapsed / seconds));
                yield return null;
            }
            t.localScale = m_VehicleScale;
            m_Grow = null;
        }

        /// <summary>Engine start, a few seconds before liftoff: the flame lights small at the nozzle and builds while the pad smoke starts.</summary>
        public void Ignite()
        {
            SetFlameLevel(m_IgnitionLevel);
            if (m_Flame != null) m_Flame.Play();
            if (m_NozzleGlow != null) m_NozzleGlow.Play();
            if (m_Sparks != null) m_Sparks.Play();
            if (m_PadSteam != null) m_PadSteam.Play();
            SetPadSmokeRate(0f);
            if (m_Smoke != null) m_Smoke.Play();
            SetPadFireRate(m_PadFireRate * m_IgnitionLevel);
            if (m_PadFire != null) m_PadFire.Play();
            if (m_OneShot != null && m_IgnitionClip != null) m_OneShot.PlayOneShot(m_IgnitionClip, 0.9f);
            if (m_EngineLoop != null && !m_EngineLoop.isPlaying) { m_EngineLoop.volume = 0.6f; m_EngineLoop.Play(); }
            if (m_Ignition != null) StopCoroutine(m_Ignition);
            m_Ignition = StartCoroutine(BuildUp());
        }

        IEnumerator BuildUp()
        {
            for (var t = 0f; t < m_IgnitionRampSeconds; t += Time.deltaTime)
            {
                var k = Mathf.SmoothStep(0f, 1f, t / m_IgnitionRampSeconds);
                SetFlameLevel(Mathf.Lerp(m_IgnitionLevel, m_PreLiftoffLevel, k));
                SetPadSmokeRate(m_PadSmokeIgnitionRate * k);
                SetPadFireRate(m_PadFireRate * flameLevel);
                if (m_EngineLoop != null) m_EngineLoop.volume = Mathf.Lerp(0.6f, 0.85f, k);
                yield return null;
            }
            SetFlameLevel(m_PreLiftoffLevel);
            SetPadSmokeRate(m_PadSmokeIgnitionRate);
            SetPadFireRate(m_PadFireRate * m_PreLiftoffLevel);
            m_Ignition = null;
        }

        /// <summary>Stops everything and leaves the vehicle on the pad (abort during countdown).</summary>
        public void AbortCountdown()
        {
            StopEffects();
        }

        /// <summary>Runs the flight for the given outcome; <paramref name="onFinished"/> fires when it is over.</summary>
        public void Liftoff(LaunchOutcome outcome, Action onFinished) => Liftoff(outcome, onFinished, 3);

        /// <summary>
        /// Runs the flight; <paramref name="wrongFlaps"/> (from the build report) sets how an unstable flight fails: one wrong flap
        /// veers off course and crashes, two or three break up in the air.
        /// </summary>
        public void Liftoff(LaunchOutcome outcome, Action onFinished, int wrongFlaps)
        {
            if (m_Flight != null) StopCoroutine(m_Flight);
            m_Flight = StartCoroutine(Flight(outcome, wrongFlaps, onFinished));
        }

        IEnumerator Flight(LaunchOutcome outcome, int wrongFlaps, Action onFinished)
        {
            if (m_Grow != null) { StopCoroutine(m_Grow); m_Grow = null; }
            if (m_Ignition != null) { StopCoroutine(m_Ignition); m_Ignition = null; }
            if (m_Vehicle != null) m_Vehicle.transform.localScale = m_VehicleScale;
            isRunning = true;
            var padPos = m_PadOrigin != null ? m_PadOrigin.position : Vector3.zero;
            var padRot = m_PadOrigin != null ? m_PadOrigin.rotation : Quaternion.identity;
            model.outcome = outcome;
            model.wrongFlaps = wrongFlaps;
            model.offCourseAzimuth = m_OffCourseAzimuth;
            model.groundBelowPad = Mathf.Max(0f, padPos.y - m_GroundLevel) / Mathf.Max(0.01f, m_VisualScale);
            model.Reset();
            // full thrust, a surge of smoke out from under the mount, and the trail starts once it climbs
            if (m_Flame != null && !m_Flame.isPlaying) m_Flame.Play();
            if (m_NozzleGlow != null && !m_NozzleGlow.isPlaying) m_NozzleGlow.Play();
            SetFlameLevel(1f);
            if (m_Smoke != null) { if (!m_Smoke.isPlaying) m_Smoke.Play(); m_Smoke.Emit(30); }
            SetPadSmokeRate(m_PadSmokeLiftoffRate);
            if (m_PadFire != null && !m_PadFire.isPlaying) m_PadFire.Play();
            SetPadFireRate(m_PadFireRate);
            if (m_ExhaustTrail != null && outcome != LaunchOutcome.MotorRetentionLoss) m_ExhaustTrail.Play();
            if (m_EngineLoop != null) { m_EngineLoop.volume = 1f; if (!m_EngineLoop.isPlaying) m_EngineLoop.Play(); }
            var exploded = false;
            var ejected = false;
            var lingerUntil = -1f;
            while (!model.IsFinished(m_SuccessAltitude) || (lingerUntil > 0f && Time.time < lingerUntil))
            {
                if (!model.hasExploded)
                {
                    model.Step(Mathf.Min(Time.deltaTime, 0.1f));   // a hitch must not throw the flight forward
                    PlaceVehicle(padPos, padRot);
                }
                if (outcome == LaunchOutcome.MotorRetentionLoss && model.motorEjected && m_VehicleMotor != null)
                {
                    if (!ejected)
                    {
                        ejected = true;
                        m_VehicleMotor.SetParent(null, true);
                        if (m_VehicleCap != null) m_VehicleCap.SetParent(m_VehicleMotor, true);
                        if (m_OneShot != null && m_ExplosionLowClip != null) m_OneShot.PlayOneShot(m_ExplosionLowClip, 0.8f);
                        if (m_Smoke != null) m_Smoke.Emit(40);   // a puff out of the base as the battery blows out
                    }
                    // the motor shoots out of the base, skids along the pad and tumbles; the flame follows it
                    var dir = padRot * new Vector3(0.6f, -0.25f, 0.75f).normalized;
                    var d = model.motorEjectDistance * m_VisualScale;
                    var ground = padPos + dir * d;
                    var floor = Mathf.Lerp(padPos.y, m_GroundLevel, Mathf.Clamp01(d / 4f));   // off the mount and down to the ground
                    ground.y = floor + Mathf.Max(0f, 2.5f * d / 20f - 0.2f * d * d / 20f) + 0.3f;
                    m_VehicleMotor.position = ground;
                    m_VehicleMotor.rotation = padRot * Quaternion.AngleAxis(model.time * 240f, Vector3.right) * Quaternion.AngleAxis(-70f, Vector3.right);
                    if (m_Flame != null)
                    {
                        m_Flame.transform.position = m_VehicleMotor.position;
                        m_Flame.transform.rotation = m_VehicleMotor.rotation * Quaternion.Euler(90f, 0f, 0f);
                        if (model.time > model.motorEjectTime + 2.5f && m_Flame.isPlaying) { m_Flame.Stop(); if (m_NozzleGlow != null) m_NozzleGlow.Stop(); }
                    }
                    if (m_EngineLoop != null) m_EngineLoop.volume = Mathf.Max(0f, 1f - (model.time - model.motorEjectTime) / 2.5f);
                    SetPadSmokeRate(0f);
                    SetPadFireRate(0f);
                }
                else if (!model.hasExploded)
                {
                    var up = model.altitude * m_VisualScale;
                    SetPadSmokeRate(m_PadSmokeLiftoffRate * Mathf.Clamp01(1f - up / m_PadSmokeFadeAltitude));
                    SetPadFireRate(model.burning ? m_PadFireRate * Mathf.Clamp01(1f - up / m_PadFireFadeAltitude) : 0f);
                }
                if (!model.burning && m_Flame != null && m_Flame.isPlaying && outcome != LaunchOutcome.MotorRetentionLoss)
                {
                    StopEngine();
                }
                if (model.hasExploded && !exploded)
                {
                    exploded = true;
                    Explode(ExplosionPoint(), model.hasCrashed);
                    lingerUntil = Time.time + m_AfterExplosionSeconds;
                }
                UpdatePadCamera(model.altitude * m_VisualScale);
                yield return null;
            }
            if (m_Smoke != null) m_Smoke.Stop();
            if (m_PadSteam != null) m_PadSteam.Stop();
            isRunning = false;
            m_Flight = null;
            onFinished?.Invoke();
        }

        void PlaceVehicle(Vector3 padPos, Quaternion padRot)
        {
            if (m_Vehicle == null) return;
            var offset = new Vector3(model.horizontalX, model.altitude, model.horizontalZ) * m_VisualScale;
            // tilt the body over towards the model's tilt direction (the way it is pushed), then the pad's own turn
            var az = model.tiltAzimuthDeg * Mathf.Deg2Rad;
            var towards = new Vector3(Mathf.Sin(az), 0f, Mathf.Cos(az));
            var tilt = Quaternion.AngleAxis(model.tiltDeg, Vector3.Cross(Vector3.up, towards));
            m_Vehicle.transform.SetPositionAndRotation(padPos + offset, tilt * padRot);
        }

        /// <summary>Where the explosion goes: the impact point on the ground after a crash, the middle of the body after a break-up.</summary>
        Vector3 ExplosionPoint()
        {
            if (m_Vehicle == null) return m_PadOrigin != null ? m_PadOrigin.position : transform.position;
            var t = m_Vehicle.transform;
            var len = model.length * m_VisualScale;
            if (model.hasCrashed)
            {
                var p = model.tiltDeg > 90f ? t.position + t.up * len : t.position;
                p.y = m_GroundLevel;
                return p;
            }
            return t.position + t.up * (len * 0.5f);
        }

        void Explode(Vector3 at, bool onGround)
        {
            if (m_ExplosionPrefab != null)
            {
                m_ActiveExplosion = Instantiate(m_ExplosionPrefab, onGround ? at + Vector3.up * m_CrashLift : at, Quaternion.identity);
                var ground = m_ActiveExplosion.transform.Find("Ground");
                if (ground != null)
                {
                    ground.gameObject.SetActive(onGround);
                    if (onGround) ground.localPosition = Vector3.down * (m_CrashLift / Mathf.Max(0.01f, m_CrashScale));   // the root is scaled
                }
                if (onGround) m_ActiveExplosion.transform.localScale = Vector3.one * m_CrashScale;
                foreach (var ps in m_ActiveExplosion.GetComponentsInChildren<ParticleSystem>()) ps.Play();
                Destroy(m_ActiveExplosion, 12f);
            }
            SpawnDebris(at, onGround);
            StopEngine();
            if (m_ExhaustTrail != null) m_ExhaustTrail.Stop();
            if (m_Vehicle != null) m_Vehicle.SetActive(false);
            if (m_OneShot != null)
            {
                // the one-shot source sits on the pad: an explosion away from it is heard from where it happened
                m_OneShot.transform.position = at;
                if (m_ExplosionClip != null) m_OneShot.PlayOneShot(m_ExplosionClip, 1f);
                if (m_ExplosionLowClip != null) m_OneShot.PlayOneShot(m_ExplosionLowClip, 1f);
            }
        }

        /// <summary>Copies of the vehicle's fins, flaps, nose and cap, thrown out from the explosion and left to fall trailing smoke.</summary>
        void SpawnDebris(Vector3 centre, bool onGround)
        {
            ClearDebris();
            foreach (var part in m_DebrisParts)
            {
                if (part == null || !part.gameObject.activeInHierarchy) continue;   // a part the rocket was built without
                var src = part.transform;
                var start = onGround ? centre + Vector3.up * 1.5f + UnityEngine.Random.insideUnitSphere * 2f : src.position;
                var piece = Instantiate(part.gameObject, start, src.rotation);
                piece.name = "Debris_" + part.transform.parent.name;
                piece.transform.localScale = src.lossyScale;
                piece.SetActive(true);
                if (piece.GetComponent<Collider>() == null) piece.AddComponent<BoxCollider>();
                var body = piece.AddComponent<Rigidbody>();
                body.mass = 40f;
                body.linearDamping = 0.15f;
                body.angularDamping = 0.1f;
                var outward = start - centre; outward.y = 0f;
                if (outward.sqrMagnitude < 0.01f) outward = UnityEngine.Random.onUnitSphere;
                outward.y = 0f;
                body.linearVelocity = outward.normalized * UnityEngine.Random.Range(8f, 18f) + Vector3.up * UnityEngine.Random.Range(onGround ? 10f : 4f, onGround ? 22f : 12f);
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 6f;
                if (m_DebrisTrail != null)
                {
                    var trail = Instantiate(m_DebrisTrail, piece.transform);
                    trail.transform.localPosition = Vector3.zero;
                    trail.transform.localScale = new Vector3(1f / Mathf.Max(1e-4f, src.lossyScale.x), 1f / Mathf.Max(1e-4f, src.lossyScale.y), 1f / Mathf.Max(1e-4f, src.lossyScale.z));
                    trail.gameObject.SetActive(true);
                    trail.Play();
                }
                m_Debris.Add(piece);
                Destroy(piece, 14f);
            }
        }

        void ClearDebris()
        {
            foreach (var d in m_Debris) if (d != null) Destroy(d);
            m_Debris.Clear();
        }

        void StopEngine()
        {
            if (m_Flame != null) m_Flame.Stop();
            if (m_NozzleGlow != null) m_NozzleGlow.Stop();
            if (m_Sparks != null) m_Sparks.Stop();
            if (m_ExhaustTrail != null) m_ExhaustTrail.Stop();
            if (m_PadFire != null) m_PadFire.Stop();
            if (m_EngineLoop != null) m_EngineLoop.Stop();
            flameLevel = 0f;
        }

        /// <summary>Flame strength 0 to 1: more and longer streaks and a bigger glow as it builds to full thrust.</summary>
        void SetFlameLevel(float level)
        {
            flameLevel = level;
            if (m_Flame != null)
            {
                var emission = m_Flame.emission; emission.rateOverTimeMultiplier = m_FlameRate * Mathf.Lerp(0.35f, 1f, level);
                var main = m_Flame.main;
                main.startSizeMultiplier = m_FlameSize * Mathf.Lerp(0.55f, 1f, level);
                main.startSpeedMultiplier = m_FlameSpeed * Mathf.Lerp(0.45f, 1f, level);
            }
            if (m_NozzleGlow != null) { var main = m_NozzleGlow.main; main.startSizeMultiplier = m_GlowSize * Mathf.Lerp(0.5f, 1f, level); }
        }

        void SetPadSmokeRate(float rate)
        {
            if (m_Smoke == null) return;
            var emission = m_Smoke.emission;
            emission.rateOverTimeMultiplier = rate;
        }

        void SetPadFireRate(float rate)
        {
            if (m_PadFire == null) return;
            var emission = m_PadFire.emission;
            emission.rateOverTimeMultiplier = rate;
        }

        void StopEffects()
        {
            if (m_Ignition != null) { StopCoroutine(m_Ignition); m_Ignition = null; }
            if (m_Flame != null) { m_Flame.Stop(); m_Flame.Clear(); }
            if (m_NozzleGlow != null) { m_NozzleGlow.Stop(); m_NozzleGlow.Clear(); }
            if (m_Smoke != null) { m_Smoke.Stop(); m_Smoke.Clear(); }
            if (m_Sparks != null) { m_Sparks.Stop(); m_Sparks.Clear(); }
            if (m_PadSteam != null) { m_PadSteam.Stop(); m_PadSteam.Clear(); }
            if (m_ExhaustTrail != null) { m_ExhaustTrail.Stop(); m_ExhaustTrail.Clear(); }
            if (m_PadFire != null) { m_PadFire.Stop(); m_PadFire.Clear(); }
            if (m_EngineLoop != null) m_EngineLoop.Stop();
            if (m_OneShot != null && m_PadOrigin != null) m_OneShot.transform.position = m_PadOrigin.position + Vector3.up * 2f;
            flameLevel = 0f;
        }

        void UpdatePadCamera(float altitude)
        {
            if (m_PadCamera == null || m_PadOrigin == null) return;
            // follow the vehicle; after an explosion stay on the fireball instead of snapping back to the empty pad
            var target = m_ActiveExplosion != null ? m_ActiveExplosion.transform.position
                : m_Vehicle != null && m_Vehicle.activeInHierarchy ? m_Vehicle.transform.position + m_Vehicle.transform.up * 6f
                : m_PadOrigin.position + Vector3.up * 6f;
            // the camera stays on its tripod and pans up; it widens its view as the rocket climbs or flies away
            m_PadCamera.transform.position = m_PadOrigin.position + m_PadOrigin.rotation * m_PadCameraOffset;
            m_PadCamera.transform.LookAt(target);
            var away = m_Vehicle != null ? new Vector2(model.horizontalX, model.horizontalZ).magnitude * m_VisualScale : 0f;
            m_PadCamera.fieldOfView = Mathf.Clamp(28f + (altitude + away) * 0.02f, 28f, 70f);
        }
    }
}
