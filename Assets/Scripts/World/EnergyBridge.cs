using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Materializes dynamic bridge stair steps onto Platform High when linked WeightedPressurePlate activates.
    /// Keeps collider disabled when unpowered so Rect cannot bypass the heavy block gate early.
    /// </summary>
    public sealed class EnergyBridge : MonoBehaviour
    {
        [Header("Links")]
        public WeightedPressurePlate triggerPlate;
        public WeightedPressurePlate[] requiredPlates;
        public Collider[] stepColliders;
        public Transform[] animatedSegments;
        public Renderer[] hologramRenderers;
        public Material dormantMaterial;
        public Material activeMaterial;

        private bool isPowered;
        private float powerBlend;
        private float[] fullHeights;
        private float[] baseCenterYs;

        public bool IsPowered => isPowered;

        private void Awake()
        {
            if (animatedSegments == null) return;
            fullHeights = new float[animatedSegments.Length];
            baseCenterYs = new float[animatedSegments.Length];
            for (int i = 0; i < animatedSegments.Length; i++)
            {
                if (animatedSegments[i] == null) continue;
                fullHeights[i] = animatedSegments[i].localScale.y;
                baseCenterYs[i] = animatedSegments[i].localPosition.y;
            }
        }

        private void OnEnable()
        {
            SubscribePlates(true);
        }

        private void OnDisable()
        {
            SubscribePlates(false);
        }

        private void Start()
        {
            SetPowered(AreAllPlatesActive(), true);
        }

        private void HandlePlateChanged(bool _)
        {
            SetPowered(AreAllPlatesActive(), false);
        }

        private void SubscribePlates(bool subscribe)
        {
            if (requiredPlates != null && requiredPlates.Length > 0)
            {
                for (int i = 0; i < requiredPlates.Length; i++)
                {
                    if (requiredPlates[i] == null) continue;
                    if (subscribe) requiredPlates[i].OnActivatedChanged += HandlePlateChanged;
                    else requiredPlates[i].OnActivatedChanged -= HandlePlateChanged;
                }
            }
            if (triggerPlate != null)
            {
                if (subscribe) triggerPlate.OnActivatedChanged += HandlePlateChanged;
                else triggerPlate.OnActivatedChanged -= HandlePlateChanged;
            }
        }

        private bool AreAllPlatesActive()
        {
            if (requiredPlates != null && requiredPlates.Length > 0)
            {
                for (int i = 0; i < requiredPlates.Length; i++)
                {
                    if (requiredPlates[i] == null || !requiredPlates[i].IsActivated) return false;
                }
                return true;
            }
            return triggerPlate != null && triggerPlate.IsActivated;
        }

        public void SetPowered(bool powered, bool instant)
        {
            isPowered = powered;
            if (stepColliders != null)
            {
                for (int i = 0; i < stepColliders.Length; i++)
                {
                    if (stepColliders[i] != null)
                        stepColliders[i].enabled = powered;
                }
            }

            Material mat = powered ? activeMaterial : dormantMaterial;
            if (hologramRenderers != null && mat != null)
            {
                for (int i = 0; i < hologramRenderers.Length; i++)
                {
                    if (hologramRenderers[i] != null)
                        hologramRenderers[i].sharedMaterial = mat;
                }
            }

            if (instant)
            {
                powerBlend = powered ? 1f : 0f;
                ApplyScale(powerBlend);
            }
            else if (powered)
            {
                SimulationVFX.Burst(transform.position + Vector3.up * 0.4f, PaletteSettings.Teal, 18, 2.4f);
            }
        }

        private void Update()
        {
            float target = isPowered ? 1f : 0f;
            if (Mathf.Approximately(powerBlend, target)) return;

            powerBlend = Mathf.MoveTowards(powerBlend, target, Time.deltaTime * 4.5f);
            ApplyScale(powerBlend);
        }

        private void ApplyScale(float t)
        {
            if (animatedSegments == null || fullHeights == null) return;
            float ratio = Mathf.Lerp(0.06f, 1f, Mathf.SmoothStep(0f, 1f, t));
            for (int i = 0; i < animatedSegments.Length; i++)
            {
                Transform seg = animatedSegments[i];
                if (seg == null) continue;

                float fullH = fullHeights[i];
                float targetH = fullH * ratio;
                Vector3 s = seg.localScale;
                seg.localScale = new Vector3(s.x, targetH, s.z);

                Vector3 p = seg.localPosition;
                p.y = baseCenterYs[i] - fullH * 0.5f * (1f - ratio);
                seg.localPosition = p;
            }
        }
    }
}
