using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Cinematic Main Menu 3D diorama camera that mirrors the UI progression:
    /// low near-player POV showcase on Title, downward camera descent tracking the falling
    /// current-level sphere into the Chapter Ribbon, subtle horizontal horizon glide while browsing,
    /// and a player-POV push-in plunge when entering a level portal.
    /// </summary>
    public sealed class MainMenuCameraOrbit : MonoBehaviour
    {
        public Vector3 focusPoint = new Vector3(-2.65f, 0.80f, 1.45f);
        public Vector3 rectFocusPoint = new Vector3(-0.7f, 0.65f, -0.5f);
        public float baseYaw = 30f;
        public float basePitch = 19f;
        public float orbitDistance = 9.8f;
        public float swayAmplitude = 3.2f;
        public float swaySpeed = 0.22f;

        private Transform showcaseRectTransform;
        private float ribbonBlend;
        private float targetRibbonBlend;
        private float smashDescent = -1f;
        private float portalDive;
        private float scrollBiasNormalized;
        private float impactShake;

        private void Awake()
        {
            GameObject bot = GameObject.Find("Showcase Rect");
            if (bot != null) showcaseRectTransform = bot.transform;
            if ((Mathf.Approximately(focusPoint.x, 0.4f) && Mathf.Approximately(focusPoint.z, 1.0f)) ||
                (Mathf.Approximately(focusPoint.x, -1.45f) && Mathf.Approximately(focusPoint.z, 1.95f)))
            {
                focusPoint = new Vector3(-2.65f, 0.80f, 1.45f);
                orbitDistance = 9.8f;
                baseYaw = 30f;
                basePitch = 19f;
            }
        }

        public void SetRibbonOpen(bool open)
        {
            targetRibbonBlend = open ? 1f : 0f;
            if (!open)
            {
                smashDescent = -1f;
                portalDive = 0f;
            }
        }

        /// <summary>Drives camera vertical descent & pitch tilt synced with the falling level sphere (0..1).</summary>
        public void SetSmashDescent(float normalizedProgress)
        {
            smashDescent = Mathf.Clamp01(normalizedProgress);
        }

        public void ClearSmashDescent()
        {
            smashDescent = -1f;
            targetRibbonBlend = 1f;
            ribbonBlend = 1f;
        }

        public void SetScrollFraction(float norm01)
        {
            scrollBiasNormalized = Mathf.Clamp01(norm01);
        }

        /// <summary>Drives POV plunge into Showcase Rect as the selected level orb expands into a portal (0..1).</summary>
        public void SetPortalDive(float normalizedProgress)
        {
            portalDive = Mathf.Clamp01(normalizedProgress);
        }

        public void TriggerImpactPunch(float strength)
        {
            if (!SimulationSettings.ReduceMotion)
                impactShake = Mathf.Clamp(impactShake + strength, 0f, 0.45f);
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;
            ribbonBlend = Mathf.MoveTowards(ribbonBlend, targetRibbonBlend, dt * 1.6f);
            float t = Time.unscaledTime;

            float sway = SimulationSettings.ReduceMotion ? 0f : Mathf.Sin(t * swaySpeed) * swayAmplitude;
            float verticalBreath = SimulationSettings.ReduceMotion ? 0f : Mathf.Cos(t * swaySpeed * 0.7f) * 1.4f;

            // Default Title -> Ribbon interpolation
            float activeYaw = baseYaw + sway + ribbonBlend * 14f + (scrollBiasNormalized - 0.5f) * ribbonBlend * 16f;
            float activePitch = basePitch + verticalBreath + ribbonBlend * 10f;
            float dist = orbitDistance + ribbonBlend * 3.1f;
            Vector3 activeFocus = focusPoint;

            // Phase 1: Smash Descent (camera starts slightly elevated above block impact and rides smoothly down into Ribbon view)
            if (smashDescent >= 0f && !SimulationSettings.ReduceMotion)
            {
                float d = smashDescent;
                float eased = 1f - Mathf.Pow(1f - d, 3f);
                // Smoothly ride downward from high impact framing (y+1.15, pitch 13) to ribbon deck framing (y-0.05, pitch 28)
                activeFocus.y += Mathf.Lerp(1.05f, 0f, eased);
                activePitch = Mathf.Lerp(basePitch - 4f, basePitch + 10f, eased);
                activeYaw = Mathf.Lerp(baseYaw, baseYaw + 14f, eased) + sway * 0.6f;
                dist = Mathf.Lerp(orbitDistance - 0.5f, orbitDistance + 3.1f, eased);
            }

            // Phase 2: Portal Entry (glide directly behind Showcase Rect facing forward toward Anchor)
            if (portalDive > 0.001f)
            {
                float p = SimulationSettings.ReduceMotion ? 1f : Mathf.SmoothStep(0f, 1f, portalDive);

                Vector3 rectBasePos = showcaseRectTransform != null ? showcaseRectTransform.position : rectFocusPoint;
                Vector3 rectFwd = showcaseRectTransform != null
                    ? Vector3.ProjectOnPlane(showcaseRectTransform.forward, Vector3.up)
                    : new Vector3(0.45f, 0f, 0.89f);
                if (rectFwd.sqrMagnitude < 0.001f) rectFwd = Vector3.forward;
                rectFwd.Normalize();

                float rearYaw = Mathf.Atan2(rectFwd.x, rectFwd.z) * Mathf.Rad2Deg;
                Vector3 rearFocus = rectBasePos + Vector3.up * 0.72f + rectFwd * 0.65f;

                activeFocus = Vector3.Lerp(activeFocus, rearFocus, p);
                dist = Mathf.Lerp(dist, 3.85f, p);
                activePitch = Mathf.Lerp(activePitch, 14f, p);
                activeYaw = Mathf.LerpAngle(activeYaw, rearYaw, p);
            }

            Vector3 shakeOffset = Vector3.zero;
            if (impactShake > 0.001f)
            {
                shakeOffset = new Vector3(
                    Mathf.Sin(t * 62f) * impactShake,
                    Mathf.Cos(t * 71f) * impactShake * 0.8f,
                    Mathf.Sin(t * 49f) * impactShake);
                impactShake = Mathf.MoveTowards(impactShake, 0f, dt * 1.6f);
            }

            Quaternion rot = Quaternion.Euler(activePitch, activeYaw, 0f);
            Vector3 offset = rot * (Vector3.back * dist);
            transform.SetPositionAndRotation(activeFocus + offset + shakeOffset, rot);
        }
    }
}
