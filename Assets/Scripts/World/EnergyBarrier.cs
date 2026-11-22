using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Force-field barrier that blocks props or traversal until all linked WeightedPressurePlates activate.
    /// Animates a crisp vertical scale-down dissolve when powered open.
    /// </summary>
    public sealed class EnergyBarrier : MonoBehaviour
    {
        public WeightedPressurePlate[] requiredPlates;
        public Collider barrierCollider;
        public Transform barrierMeshTransform;
        public Renderer barrierRenderer;

        private bool isOpen;
        private float fullHeight = 1f;
        private float baseY;
        private float openBlend;

        public bool IsOpen => isOpen;

        private void Awake()
        {
            if (barrierMeshTransform != null)
            {
                fullHeight = barrierMeshTransform.localScale.y;
                baseY = barrierMeshTransform.localPosition.y;
            }
        }

        private void OnEnable()
        {
            if (requiredPlates == null) return;
            for (int i = 0; i < requiredPlates.Length; i++)
            {
                if (requiredPlates[i] != null)
                    requiredPlates[i].OnActivatedChanged += HandleAnyPlateChanged;
            }
        }

        private void OnDisable()
        {
            if (requiredPlates == null) return;
            for (int i = 0; i < requiredPlates.Length; i++)
            {
                if (requiredPlates[i] != null)
                    requiredPlates[i].OnActivatedChanged -= HandleAnyPlateChanged;
            }
        }

        private void Start()
        {
            EvaluateState(true);
        }

        private void HandleAnyPlateChanged(bool _) => EvaluateState(false);

        private void EvaluateState(bool instant)
        {
            bool allActive = requiredPlates != null && requiredPlates.Length > 0;
            if (allActive)
            {
                for (int i = 0; i < requiredPlates.Length; i++)
                {
                    if (requiredPlates[i] == null || !requiredPlates[i].IsActivated)
                    {
                        allActive = false;
                        break;
                    }
                }
            }

            isOpen = allActive;
            if (barrierCollider != null) barrierCollider.enabled = !isOpen;
            if (instant)
            {
                openBlend = isOpen ? 1f : 0f;
                ApplyVisual(openBlend);
            }
            else if (isOpen)
            {
                SimulationVFX.Burst(transform.position, PaletteSettings.Teal, 14, 2.2f);
            }
        }

        private void Update()
        {
            float target = isOpen ? 1f : 0f;
            if (Mathf.Approximately(openBlend, target)) return;
            openBlend = Mathf.MoveTowards(openBlend, target, Time.deltaTime * 5.2f);
            ApplyVisual(openBlend);
        }

        private void ApplyVisual(float t)
        {
            if (barrierMeshTransform == null) return;
            float ratio = Mathf.Lerp(1f, 0.02f, Mathf.SmoothStep(0f, 1f, t));
            Vector3 s = barrierMeshTransform.localScale;
            barrierMeshTransform.localScale = new Vector3(s.x, fullHeight * ratio, s.z);
            Vector3 p = barrierMeshTransform.localPosition;
            p.y = baseY - fullHeight * 0.5f * (1f - ratio);
            barrierMeshTransform.localPosition = p;
            if (barrierRenderer != null) barrierRenderer.enabled = t < 0.98f;
        }
    }
}
