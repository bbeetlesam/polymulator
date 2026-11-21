using System.Collections;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Sequential floor energy conduit linking WeightedPressurePlate to EnergyBridge
    /// along orthogonal chamber grid channels. Uses shared material swaps for SRP Batcher safety.
    /// </summary>
    public sealed class ConduitCircuit : MonoBehaviour
    {
        public WeightedPressurePlate triggerPlate;
        public Renderer[] traceSegments;
        public Material dormantMaterial;
        public Material activeMaterial;
        [Min(0.01f)] public float segmentStepDelay = 0.065f;

        private Coroutine waveRoutine;

        private void OnEnable()
        {
            if (triggerPlate != null)
                triggerPlate.OnActivatedChanged += HandlePlateChanged;
        }

        private void OnDisable()
        {
            if (triggerPlate != null)
                triggerPlate.OnActivatedChanged -= HandlePlateChanged;
        }

        private void Start()
        {
            ApplyAll(triggerPlate != null && triggerPlate.IsActivated);
        }

        private void HandlePlateChanged(bool active)
        {
            if (waveRoutine != null) StopCoroutine(waveRoutine);
            waveRoutine = StartCoroutine(AnimateTrace(active));
        }

        private IEnumerator AnimateTrace(bool active)
        {
            if (traceSegments == null || traceSegments.Length == 0) yield break;
            Material targetMat = active ? activeMaterial : dormantMaterial;

            if (active)
            {
                for (int i = 0; i < traceSegments.Length; i++)
                {
                    if (traceSegments[i] != null && targetMat != null)
                        traceSegments[i].sharedMaterial = targetMat;
                    yield return new WaitForSeconds(segmentStepDelay);
                }
            }
            else
            {
                for (int i = traceSegments.Length - 1; i >= 0; i--)
                {
                    if (traceSegments[i] != null && targetMat != null)
                        traceSegments[i].sharedMaterial = targetMat;
                    yield return new WaitForSeconds(segmentStepDelay * 0.6f);
                }
            }
            waveRoutine = null;
        }

        private void ApplyAll(bool active)
        {
            if (traceSegments == null) return;
            Material targetMat = active ? activeMaterial : dormantMaterial;
            if (targetMat == null) return;
            for (int i = 0; i < traceSegments.Length; i++)
            {
                if (traceSegments[i] != null)
                    traceSegments[i].sharedMaterial = targetMat;
            }
        }
    }
}
