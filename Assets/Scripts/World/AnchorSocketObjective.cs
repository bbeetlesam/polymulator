using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Detects when the Chamber Resonance Core settles into ANCHOR-01's receptacle,
    /// locking it cleanly into the cradle and raising OnObjectiveCompleted.
    /// </summary>
    public sealed class AnchorSocketObjective : MonoBehaviour
    {
        [Header("Objective Target")]
        public InteractableObject targetCore;
        public AnchorVisualRig visualRig;
        public Transform cradleSeatPoint;
        public Vector3 coreRespawnGridPos = new Vector3(0f, 2.55f, 11f);

        private bool completed;
        private InteractableObject awaitingReleaseCore;
        private float rejectCooldownUntil;
        private Vector3 lockStartPos;
        private Quaternion lockStartRot;
        private float lockBlend;

        public bool IsCompleted => completed;

        public event System.Action OnObjectiveCompleted;
        public event System.Action<string, Color> OnObjectiveFeedback;

        private void Update()
        {
            if (completed && targetCore != null)
            {
                if (lockBlend < 1f)
                {
                    lockBlend = Mathf.Min(1f, lockBlend + Time.deltaTime * 3.5f);
                    float eased = 1f - Mathf.Pow(1f - lockBlend, 3f);
                    targetCore.transform.position = Vector3.Lerp(lockStartPos, GetCradleSeatPosition(), eased);
                    targetCore.transform.rotation = Quaternion.Slerp(lockStartRot, Quaternion.Euler(45f, 45f, 0f), eased);
                }
                else
                {
                    targetCore.transform.position = GetCradleSeatPosition() +
                        Vector3.up * (Mathf.Sin(Time.time * 2.2f) * GridSettings.ToWorld(0.05f));
                    targetCore.transform.Rotate(Vector3.up, 52f * Time.deltaTime, Space.World);
                }
                return;
            }

            if (targetCore != null && targetCore.transform.position.y < GridSettings.ToWorld(-1.0f))
            {
                RespawnCoreSafely();
            }

            if (awaitingReleaseCore != null)
            {
                float seatDist = Vector3.Distance(awaitingReleaseCore.transform.position, GetCradleSeatPosition());
                bool releasedFromCarry = awaitingReleaseCore.Body != null && !awaitingReleaseCore.Body.isKinematic;
                TelekinesisController tk = Object.FindFirstObjectByType<TelekinesisController>();
                bool isTetheredNow = tk != null && tk.TetheredObject == awaitingReleaseCore;

                if ((releasedFromCarry && !isTetheredNow) || (isTetheredNow && seatDist <= GridSettings.ToWorld(0.75f)))
                {
                    if (isTetheredNow) tk.Release(false);
                    LockCore(awaitingReleaseCore);
                    awaitingReleaseCore = null;
                }
            }
        }

        private Vector3 GetCradleSeatPosition()
        {
            return cradleSeatPoint != null
                ? cradleSeatPoint.position
                : transform.position + Vector3.up * GridSettings.ToWorld(0.28f);
        }

        private void OnTriggerEnter(Collider other)
        {
            if (completed) return;
            InteractableObject obj = other.GetComponentInParent<InteractableObject>();
            if (obj == null) return;

            if (obj == targetCore)
            {
                TelekinesisController tk = Object.FindFirstObjectByType<TelekinesisController>();
                bool isTetheredNow = tk != null && tk.TetheredObject == obj;
                if (obj.Body != null && !obj.Body.isKinematic && !isTetheredNow)
                {
                    LockCore(obj);
                }
                else
                {
                    awaitingReleaseCore = obj;
                    OnObjectiveFeedback?.Invoke("ALIGNED WITH ANCHOR — RELEASE TO LOCK CORE-01", PaletteSettings.Green);
                }
                return;
            }

            if (obj.CanBePickedUp && Time.time >= rejectCooldownUntil)
            {
                rejectCooldownUntil = Time.time + 2.0f;
                SimulationAudio.PlayAnchorReject(transform.position);
                SimulationVFX.Ring(transform.position, PaletteSettings.Peach, 1.55f, 0.35f);
                OnObjectiveFeedback?.Invoke("ANCHOR REQUIRES CORE-01 (PLATFORM HIGH)", PaletteSettings.Peach);
            }
        }

        private void OnTriggerExit(Collider other)
        {
            InteractableObject obj = other.GetComponentInParent<InteractableObject>();
            if (obj != null && obj == awaitingReleaseCore)
                awaitingReleaseCore = null;
        }

        private void LockCore(InteractableObject core)
        {
            if (completed) return;
            completed = true;
            awaitingReleaseCore = null;

            core.canBePickedUp = false;
            core.canTelekinesis = false;
            core.SetHighlight(false, false);
            core.SetCollidersEnabled(false);

            Transform beacon = core.transform.Find("Core Beacon");
            if (beacon != null) beacon.gameObject.SetActive(false);

            Rigidbody body = core.Body;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.isKinematic = true;
                body.detectCollisions = false;
            }

            lockStartPos = core.transform.position;
            lockStartRot = core.transform.rotation;
            lockBlend = 0f;

            if (visualRig != null && visualRig.anchorLight != null)
            {
                visualRig.anchorLight.color = PaletteSettings.Green;
                visualRig.anchorLight.intensity = 2.6f;
            }

            SimulationAudio.PlayAnchorLock(transform.position);
            SimulationVFX.Burst(transform.position + Vector3.up * 0.4f, PaletteSettings.Green, 32, 3.8f);
            SimulationVFX.Ring(transform.position, PaletteSettings.Green, 2.8f, 0.55f);
            SimulationVisualBridge.TriggerFloorRipple(transform.position, PaletteSettings.Green, 11f, 1.55f, 0.95f);
            RectCameraController.AddImpulse(0.11f);
            OnObjectiveCompleted?.Invoke();
        }

        private void RespawnCoreSafely()
        {
            if (targetCore == null) return;
            Vector3 safePos = GridSettings.ToWorld(coreRespawnGridPos);
            targetCore.transform.position = safePos;
            targetCore.transform.rotation = Quaternion.identity;
            Rigidbody body = targetCore.Body;
            if (body != null)
            {
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
            }
            SimulationVFX.Ring(safePos, PaletteSettings.Yellow, 1.6f, 0.35f);
            OnObjectiveFeedback?.Invoke("CORE-01 RE-MATERIALIZED AT PLATFORM HIGH", PaletteSettings.Yellow);
        }
    }
}
