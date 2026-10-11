using System;
using System.Collections;
using UnityEngine;

namespace VRRocket
{
    /// <summary>
    /// The launch outside the window: a full-size vehicle on the pad, engine fire and smoke, a pad camera for the big screen,
    /// and the three outcomes of SPEC 7.2 driven by <see cref="RocketFlightModel"/>. Built as a working version of the teammates'
    /// launch screen (SPEC 9); everything lives under the LaunchPad object in the scene so it can be replaced wholesale.
    /// </summary>
    public sealed class LaunchSequence : MonoBehaviour
    {
        [Header("Pad")]
        [SerializeField] Transform m_PadOrigin;
        [SerializeField] GameObject m_Vehicle;
        [SerializeField, Tooltip("The motor mesh inside the vehicle, animated out of the base on a retention-loss failure.")] Transform m_VehicleMotor;
        [SerializeField, Tooltip("The motor cap mesh, which leaves with the motor.")] Transform m_VehicleCap;
        [Header("Effects")]
        [SerializeField] ParticleSystem m_Flame;
        [SerializeField] ParticleSystem m_Smoke;
        [SerializeField] ParticleSystem m_Sparks;
        [SerializeField] ParticleSystem m_PadSteam;
        [SerializeField] GameObject m_ExplosionPrefab;
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

        public RocketFlightModel model { get; } = new RocketFlightModel();
        public bool isRunning { get; private set; }
        public float altitude => model.altitude;
        public float speed => model.verticalSpeed;

        Vector3 m_MotorLocalPos;
        Quaternion m_MotorLocalRot;
        Vector3 m_CapLocalPos;
        Transform m_MotorParent;
        Transform m_CapParent;
        Coroutine m_Flight;
        GameObject m_ActiveExplosion;

        public void Configure(Transform padOrigin, GameObject vehicle, Transform motor, Transform cap, ParticleSystem flame, ParticleSystem smoke, ParticleSystem sparks, ParticleSystem steam, GameObject explosionPrefab, AudioSource engine, AudioSource oneShot, AudioClip ignition, AudioClip explosion, AudioClip explosionLow, Camera padCamera)
        {
            m_PadOrigin = padOrigin; m_Vehicle = vehicle; m_VehicleMotor = motor; m_VehicleCap = cap;
            m_Flame = flame; m_Smoke = smoke; m_Sparks = sparks; m_PadSteam = steam; m_ExplosionPrefab = explosionPrefab;
            m_EngineLoop = engine; m_OneShot = oneShot; m_IgnitionClip = ignition; m_ExplosionClip = explosion; m_ExplosionLowClip = explosionLow; m_PadCamera = padCamera;
        }

        Vector3 m_FlameLocalPos;
        Quaternion m_FlameLocalRot;

        void Awake()
        {
            if (m_VehicleMotor != null) { m_MotorParent = m_VehicleMotor.parent; m_MotorLocalPos = m_VehicleMotor.localPosition; m_MotorLocalRot = m_VehicleMotor.localRotation; }
            if (m_VehicleCap != null) { m_CapParent = m_VehicleCap.parent; m_CapLocalPos = m_VehicleCap.localPosition; }
            if (m_Flame != null) { m_FlameLocalPos = m_Flame.transform.localPosition; m_FlameLocalRot = m_Flame.transform.localRotation; }
            StopEffects();
            if (m_PadCamera != null) m_PadCamera.enabled = false;
        }

        /// <summary>Empty pad: no vehicle, no effects, camera off.</summary>
        public void ResetPad()
        {
            if (m_Flight != null) { StopCoroutine(m_Flight); m_Flight = null; }
            isRunning = false;
            StopEffects();
            if (m_ActiveExplosion != null) Destroy(m_ActiveExplosion);
            if (m_VehicleMotor != null && m_MotorParent != null) { m_VehicleMotor.SetParent(m_MotorParent, false); m_VehicleMotor.localPosition = m_MotorLocalPos; m_VehicleMotor.localRotation = m_MotorLocalRot; }
            if (m_VehicleCap != null && m_CapParent != null) { m_VehicleCap.SetParent(m_CapParent, false); m_VehicleCap.localPosition = m_CapLocalPos; }
            if (m_Flame != null) { m_Flame.transform.localPosition = m_FlameLocalPos; m_Flame.transform.localRotation = m_FlameLocalRot; }
            if (m_Vehicle != null)
            {
                m_Vehicle.transform.SetPositionAndRotation(m_PadOrigin != null ? m_PadOrigin.position : m_Vehicle.transform.position, m_PadOrigin != null ? m_PadOrigin.rotation : Quaternion.identity);
                m_Vehicle.SetActive(false);
            }
            if (m_PadCamera != null) m_PadCamera.enabled = false;
            model.Reset();
        }

        /// <summary>The scaled prototype arrives on the pad.</summary>
        public void PlaceVehicleOnPad()
        {
            ResetPad();
            if (m_Vehicle != null) m_Vehicle.SetActive(true);
            UpdatePadCamera(0f);
            if (m_PadCamera != null) m_PadCamera.enabled = true;
        }

        /// <summary>Engine start, a few seconds before liftoff.</summary>
        public void Ignite()
        {
            if (m_Flame != null) m_Flame.Play();
            if (m_Sparks != null) m_Sparks.Play();
            if (m_PadSteam != null) m_PadSteam.Play();
            if (m_OneShot != null && m_IgnitionClip != null) m_OneShot.PlayOneShot(m_IgnitionClip, 0.9f);
            if (m_EngineLoop != null && !m_EngineLoop.isPlaying) { m_EngineLoop.volume = 0.6f; m_EngineLoop.Play(); }
        }

        /// <summary>Stops everything and leaves the vehicle on the pad (abort during countdown).</summary>
        public void AbortCountdown()
        {
            StopEffects();
        }

        /// <summary>Runs the flight for the given outcome; <paramref name="onFinished"/> fires when it is over.</summary>
        public void Liftoff(LaunchOutcome outcome, Action onFinished)
        {
            if (m_Flight != null) StopCoroutine(m_Flight);
            m_Flight = StartCoroutine(Flight(outcome, onFinished));
        }

        IEnumerator Flight(LaunchOutcome outcome, Action onFinished)
        {
            isRunning = true;
            model.outcome = outcome;
            model.Reset();
            if (m_Smoke != null) m_Smoke.Play();
            if (m_EngineLoop != null) m_EngineLoop.volume = 1f;
            var padPos = m_PadOrigin != null ? m_PadOrigin.position : Vector3.zero;
            var padRot = m_PadOrigin != null ? m_PadOrigin.rotation : Quaternion.identity;
            var exploded = false;
            var ejected = false;
            while (!model.IsFinished(m_SuccessAltitude))
            {
                model.Step(Time.deltaTime);
                if (m_Vehicle != null)
                {
                    var offset = new Vector3(model.horizontalX, model.altitude, model.horizontalZ) * m_VisualScale;
                    var tilt = Quaternion.AngleAxis(model.tiltAzimuthDeg, Vector3.up) * Quaternion.AngleAxis(model.tiltDeg, Vector3.forward);
                    m_Vehicle.transform.SetPositionAndRotation(padPos + offset, padRot * tilt);
                }
                if (outcome == LaunchOutcome.MotorRetentionLoss && model.motorEjected && m_VehicleMotor != null)
                {
                    if (!ejected)
                    {
                        ejected = true;
                        m_VehicleMotor.SetParent(null, true);
                        if (m_VehicleCap != null) m_VehicleCap.SetParent(m_VehicleMotor, true);
                        if (m_OneShot != null && m_ExplosionLowClip != null) m_OneShot.PlayOneShot(m_ExplosionLowClip, 0.8f);
                    }
                    // the motor shoots out of the base, skids along the pad and tumbles; the flame follows it
                    var dir = padRot * new Vector3(0.6f, -0.25f, 0.75f).normalized;
                    var d = model.motorEjectDistance * m_VisualScale;
                    var ground = padPos + dir * d;
                    ground.y = padPos.y + Mathf.Max(0f, 2.5f * d / 20f - 0.2f * d * d / 20f) + 0.3f;
                    m_VehicleMotor.position = ground;
                    m_VehicleMotor.rotation = padRot * Quaternion.AngleAxis(model.time * 240f, Vector3.right) * Quaternion.AngleAxis(-70f, Vector3.right);
                    if (m_Flame != null)
                    {
                        m_Flame.transform.position = m_VehicleMotor.position;
                        m_Flame.transform.rotation = m_VehicleMotor.rotation * Quaternion.Euler(90f, 0f, 0f);
                        if (model.time > model.motorEjectTime + 2.5f && m_Flame.isPlaying) m_Flame.Stop();
                    }
                    if (m_EngineLoop != null) m_EngineLoop.volume = Mathf.Max(0f, 1f - (model.time - model.motorEjectTime) / 2.5f);
                }
                if (!model.burning && m_Flame != null && m_Flame.isPlaying && outcome != LaunchOutcome.MotorRetentionLoss)
                {
                    m_Flame.Stop(); if (m_Sparks != null) m_Sparks.Stop();
                    if (m_EngineLoop != null) m_EngineLoop.Stop();
                }
                if (model.hasExploded && !exploded)
                {
                    exploded = true;
                    Explode(m_Vehicle != null ? m_Vehicle.transform.position : padPos);
                    if (m_Vehicle != null) m_Vehicle.SetActive(false);
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

        void Explode(Vector3 at)
        {
            if (m_ExplosionPrefab != null)
            {
                m_ActiveExplosion = Instantiate(m_ExplosionPrefab, at, Quaternion.identity);
                foreach (var ps in m_ActiveExplosion.GetComponentsInChildren<ParticleSystem>()) ps.Play();
                Destroy(m_ActiveExplosion, 12f);
            }
            if (m_Flame != null) m_Flame.Stop();
            if (m_Sparks != null) m_Sparks.Stop();
            if (m_EngineLoop != null) m_EngineLoop.Stop();
            if (m_OneShot != null)
            {
                if (m_ExplosionClip != null) m_OneShot.PlayOneShot(m_ExplosionClip, 1f);
                if (m_ExplosionLowClip != null) m_OneShot.PlayOneShot(m_ExplosionLowClip, 1f);
            }
        }

        void StopEffects()
        {
            if (m_Flame != null) { m_Flame.Stop(); m_Flame.Clear(); if (m_Vehicle != null && m_Flame.transform.parent != m_Vehicle.transform) { } }
            if (m_Smoke != null) { m_Smoke.Stop(); m_Smoke.Clear(); }
            if (m_Sparks != null) { m_Sparks.Stop(); m_Sparks.Clear(); }
            if (m_PadSteam != null) { m_PadSteam.Stop(); m_PadSteam.Clear(); }
            if (m_EngineLoop != null) m_EngineLoop.Stop();
        }

        void UpdatePadCamera(float altitude)
        {
            if (m_PadCamera == null || m_PadOrigin == null) return;
            // follow the vehicle; after an explosion stay on the fireball instead of snapping back to the empty pad
            var target = m_ActiveExplosion != null ? m_ActiveExplosion.transform.position
                : m_Vehicle != null && m_Vehicle.activeInHierarchy ? m_Vehicle.transform.position + Vector3.up * 6f
                : m_PadOrigin.position + Vector3.up * 6f;
            // the camera stays on its tripod and pans up; it widens its view as the rocket climbs
            m_PadCamera.transform.position = m_PadOrigin.position + m_PadOrigin.rotation * m_PadCameraOffset;
            m_PadCamera.transform.LookAt(target);
            m_PadCamera.fieldOfView = Mathf.Clamp(28f + altitude * 0.02f, 28f, 70f);
        }
    }
}
