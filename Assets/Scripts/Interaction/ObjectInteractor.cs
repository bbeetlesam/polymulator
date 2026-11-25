using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    public class ObjectInteractor : MonoBehaviour
    {
        [Header("Detection (grid units)")]
        [Tooltip("Interaction radius in Limited mode (logical grid units).")]
        public float interactionRange = 1.6f;
        [Tooltip("Detection range in Flexible telekinesis mode (logical grid units).")]
        public float flexibleRange = 7.5f;
        public float maxFacingAngle = 55f;
        public Transform interactionOrigin;
        public LayerMask detectionMask = ~0;

        [Header("References")]
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;
        public SimulationModeController modeController;

        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly List<InteractableObject> seenCandidates = new List<InteractableObject>(16);
        private InteractableObject currentTarget;
        private float worldInteractionRange;
        private float worldFlexibleRange;

        public InteractableObject CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;
        public bool TargetCanBePickedUp
        {
            get
            {
                if (currentTarget == null || !currentTarget.CanBePickedUp) return false;
                if (carryController != null && carryController.IsCarrying) return false;
                if (telekinesisController != null && telekinesisController.HasTether) return false;
                return true;
            }
        }
        public string TargetName => currentTarget != null ? currentTarget.displayName : string.Empty;
        public float WorldInteractionRange => IsFlexible ? worldFlexibleRange : worldInteractionRange;
        private bool IsFlexible => modeController != null && modeController.IsFlexible;

        private void Awake()
        {
            if (carryController == null) carryController = GetComponent<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = GetComponent<TelekinesisController>();
            if (modeController == null) modeController = GetComponent<SimulationModeController>();
            if (interactionOrigin == null) interactionOrigin = transform;
            worldInteractionRange = GridSettings.ToWorld(interactionRange);
            worldFlexibleRange = GridSettings.ToWorld(flexibleRange);
        }

        private void Update()
        {
            if (Time.timeScale <= 0f) return;

            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;

            bool pointerOverHud = SimulationHUDController.IsPointerOverInteractiveUI;
            bool interactPressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;
            bool leftClickPressed = !pointerOverHud && mouse != null && mouse.leftButton.wasPressedThisFrame;

            if (IsFlexible)
            {
                HandleFlexibleMode(interactPressed || leftClickPressed);
                return;
            }

            HandleLimitedMode(interactPressed, leftClickPressed, keyboard, mouse);
        }

        private void HandleLimitedMode(bool interactPressed, bool leftClickPressed, Keyboard keyboard, Mouse mouse)
        {
            if (carryController != null && carryController.IsCarrying)
            {
                SetTarget(null);

                if (keyboard != null && keyboard.rKey.wasPressedThisFrame)
                    carryController.TryRotateStep(90f);

                if ((keyboard != null && keyboard.fKey.wasPressedThisFrame) || leftClickPressed)
                {
                    carryController.TryThrow();
                    return;
                }

                float reachDelta = 0f;
                if (mouse != null)
                {
                    float scrollY = mouse.scroll.ReadValue().y;
                    if (Mathf.Abs(scrollY) > 0.01f) reachDelta += Mathf.Sign(scrollY) * 0.2f;
                }
                if (keyboard != null)
                {
                    if (keyboard.qKey.wasPressedThisFrame) reachDelta += 0.25f;
                    if (keyboard.zKey.wasPressedThisFrame) reachDelta -= 0.25f;
                }
                if (Mathf.Abs(reachDelta) > 0.001f)
                    carryController.AdjustDropReach(reachDelta);

                if (interactPressed) carryController.BeginDrop();
                return;
            }

            InteractableObject target = FindBestLimitedTarget();
            SetTarget(target);

            if (interactPressed && target != null && carryController != null)
            {
                if (target.CanBePickedUp)
                {
                    if (carryController.TryPickup(target)) SetTarget(null);
                }
                else
                {
                    SimulationVFX.Ring(target.transform.position, PaletteSettings.Peach, 1.5f, 0.35f);
                    modeController?.NotifyBanner(target.displayName + " IS STATIC ANCHOR", PaletteSettings.Peach);
                }
            }
        }

        private void HandleFlexibleMode(bool tetherPressed)
        {
            if (telekinesisController != null && telekinesisController.HasTether)
            {
                SetTarget(null);
                if (tetherPressed) telekinesisController.Release(false);
                return;
            }

            InteractableObject target = FindBestFlexibleTarget();
            SetTarget(target);

            if (tetherPressed && target != null && telekinesisController != null)
            {
                if (target.CanBePickedUp && target.canTelekinesis)
                {
                    if (telekinesisController.TryLockTarget(target)) SetTarget(null);
                }
                else
                {
                    SimulationVFX.Ring(target.transform.position, PaletteSettings.Peach, 1.5f, 0.35f);
                    modeController?.NotifyBanner(target.displayName + " CANNOT BE TETHERED", PaletteSettings.Peach);
                }
            }
        }

        private InteractableObject FindBestLimitedTarget()
        {
            if (interactionOrigin == null) return null;

            seenCandidates.Clear();
            int count = Physics.OverlapSphereNonAlloc(interactionOrigin.position, worldInteractionRange,
                overlapBuffer, detectionMask, QueryTriggerInteraction.Ignore);

            InteractableObject best = null;
            float bestScore = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider candidateCollider = overlapBuffer[i];
                if (candidateCollider == null) continue;

                InteractableObject candidate = candidateCollider.GetComponentInParent<InteractableObject>();
                if (candidate == null || !candidate.isActiveAndEnabled) continue;
                if (carryController != null && candidate == carryController.CarriedObject) continue;
                if (seenCandidates.Contains(candidate)) continue;
                seenCandidates.Add(candidate);

                Vector3 toCandidate = candidate.transform.position - transform.position;
                toCandidate.y = 0f;
                if (toCandidate.sqrMagnitude < 0.0001f) continue;

                float angle = Vector3.Angle(transform.forward, toCandidate.normalized);
                if (angle > maxFacingAngle) continue;

                Vector3 targetPoint = candidate.transform.position;
                if (!HasLineOfSight(interactionOrigin.position, targetPoint, candidate)) continue;

                float distance = Vector3.Distance(transform.position, targetPoint);
                float score = distance + angle * 0.02f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private InteractableObject FindBestFlexibleTarget()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (mouse != null && cam != null)
            {
                Ray ray = cam.ScreenPointToRay(mouse.position.ReadValue());
                if (Physics.Raycast(ray, out RaycastHit hit, 250f, detectionMask, QueryTriggerInteraction.Ignore))
                {
                    InteractableObject aimed = hit.collider.GetComponentInParent<InteractableObject>();
                    if (aimed != null && aimed.isActiveAndEnabled &&
                        Vector3.Distance(transform.position, aimed.transform.position) <= worldFlexibleRange)
                    {
                        return aimed;
                    }
                }
            }

            if (interactionOrigin == null) return null;

            seenCandidates.Clear();
            int count = Physics.OverlapSphereNonAlloc(interactionOrigin.position, worldFlexibleRange,
                overlapBuffer, detectionMask, QueryTriggerInteraction.Ignore);

            InteractableObject best = null;
            float bestScore = float.MaxValue;
            for (int i = 0; i < count; i++)
            {
                Collider col = overlapBuffer[i];
                if (col == null) continue;

                InteractableObject candidate = col.GetComponentInParent<InteractableObject>();
                if (candidate == null || !candidate.isActiveAndEnabled || !candidate.CanBePickedUp) continue;
                if (seenCandidates.Contains(candidate)) continue;
                seenCandidates.Add(candidate);

                Vector3 toCandidate = candidate.transform.position - transform.position;
                toCandidate.y = 0f;
                if (toCandidate.sqrMagnitude < 0.0001f) continue;

                float angle = Vector3.Angle(transform.forward, toCandidate.normalized);
                if (angle > 95f) continue;

                float distance = Vector3.Distance(transform.position, candidate.transform.position);
                float score = distance + angle * 0.025f;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = candidate;
                }
            }
            return best;
        }

        private bool HasLineOfSight(Vector3 origin, Vector3 targetPoint, InteractableObject candidate)
        {
            RaycastHit hit;
            if (!Physics.Linecast(origin, targetPoint, out hit, detectionMask, QueryTriggerInteraction.Ignore))
                return true;

            InteractableObject hitObject = hit.collider.GetComponentInParent<InteractableObject>();
            return hitObject == candidate;
        }

        private void SetTarget(InteractableObject nextTarget)
        {
            if (currentTarget == nextTarget) return;

            if (currentTarget != null) currentTarget.SetHighlight(false, false);
            currentTarget = nextTarget;
            if (currentTarget != null) currentTarget.SetHighlight(true, currentTarget.CanBePickedUp);
        }

        private void OnDisable()
        {
            if (currentTarget != null)
            {
                currentTarget.SetHighlight(false, false);
                currentTarget = null;
            }
        }
    }
}
