using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Weighted chamber switch activated when resting props (or Rect if heavy enough)
    /// exceed requiredMass. Swaps shared materials cleanly to preserve URP SRP Batcher compatibility.
    /// </summary>
    public sealed class WeightedPressurePlate : MonoBehaviour
    {
        [Header("Calibration")]
        [Min(0.1f)] public float requiredMass = 1.8f;

        [Header("Visual Rig")]
        public Transform movingDeck;
        public Renderer[] stateRenderers;
        public Renderer[] weightPipRenderers;
        public Material idleAccentMaterial;
        public Material partialAccentMaterial;
        public Material activeAccentMaterial;
        public Light plateLight;

        private float baseDeckY;
        private bool isActivated;
        private float currentMass;
        private readonly HashSet<Rigidbody> bodiesOnPlate = new HashSet<Rigidbody>();
        private readonly List<Rigidbody> staleBodies = new List<Rigidbody>(8);

        public bool IsActivated => isActivated;
        public float CurrentMass => currentMass;

        public event System.Action<bool> OnActivatedChanged;

        private void Awake()
        {
            if (movingDeck != null)
                baseDeckY = movingDeck.localPosition.y;
            ApplyStateVisuals(false, true);
        }

        private void FixedUpdate()
        {
            staleBodies.Clear();
            float total = 0f;
            foreach (Rigidbody rb in bodiesOnPlate)
            {
                if (rb == null || !rb.gameObject.activeInHierarchy || rb.isKinematic)
                {
                    staleBodies.Add(rb);
                    continue;
                }
                total += rb.mass;
            }
            for (int i = 0; i < staleBodies.Count; i++)
                bodiesOnPlate.Remove(staleBodies[i]);

            currentMass = total;
            UpdateWeightPips();

            bool nextActive = currentMass >= requiredMass;
            if (nextActive != isActivated)
            {
                isActivated = nextActive;
                ApplyStateVisuals(isActivated, false);
                OnActivatedChanged?.Invoke(isActivated);
            }
        }

        private void UpdateWeightPips()
        {
            if (weightPipRenderers == null || weightPipRenderers.Length == 0) return;
            float ratio = Mathf.Clamp01(currentMass / Mathf.Max(0.1f, requiredMass));
            for (int i = 0; i < weightPipRenderers.Length; i++)
            {
                Renderer r = weightPipRenderers[i];
                if (r == null) continue;
                float threshold = (i + 1) / (float)weightPipRenderers.Length - 0.35f;
                Material m = isActivated
                    ? activeAccentMaterial
                    : (ratio >= threshold && partialAccentMaterial != null ? partialAccentMaterial : idleAccentMaterial);
                if (m != null && r.sharedMaterial != m) r.sharedMaterial = m;
            }
        }

        private void Update()
        {
            if (movingDeck == null) return;
            float offset = isActivated ? GridSettings.ToWorld(-0.045f) : 0f;
            Vector3 pos = movingDeck.localPosition;
            pos.y = Mathf.Lerp(pos.y, baseDeckY + offset, 1f - Mathf.Exp(-16f * Time.deltaTime));
            movingDeck.localPosition = pos;
        }

        public void UnregisterBody(Rigidbody rb)
        {
            if (rb != null) bodiesOnPlate.Remove(rb);
        }

        private void OnTriggerEnter(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null) bodiesOnPlate.Add(rb);
        }

        private void OnTriggerExit(Collider other)
        {
            Rigidbody rb = other.attachedRigidbody;
            if (rb != null) bodiesOnPlate.Remove(rb);
        }

        private void ApplyStateVisuals(bool active, bool silent)
        {
            Material targetMat = active ? activeAccentMaterial : idleAccentMaterial;
            if (stateRenderers != null && targetMat != null)
            {
                for (int i = 0; i < stateRenderers.Length; i++)
                {
                    if (stateRenderers[i] != null)
                        stateRenderers[i].sharedMaterial = targetMat;
                }
            }

            if (plateLight != null)
            {
                plateLight.color = active ? PaletteSettings.Green : PaletteSettings.Peach;
                plateLight.intensity = active ? 2.1f : 1.1f;
            }

            if (!silent)
            {
                SimulationAudio.PlayPlateToggle(transform.position, active);
                SimulationVFX.Ring(transform.position + Vector3.up * 0.08f,
                    active ? PaletteSettings.Green : PaletteSettings.Peach, 1.9f, 0.35f);
                SimulationVisualBridge.TriggerFloorRipple(transform.position,
                    active ? PaletteSettings.Green : PaletteSettings.Peach,
                    active ? 7.2f : 4.5f, active ? 1.25f : 0.7f, 0.72f);
            }
        }
    }
}
