using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    public class ObjectInteractor : MonoBehaviour
    {
        [Header("Detection")]
        [Tooltip("Interaction radius in logical grid units.")]
        public float interactionRange = 1.6f;
        public float maxFacingAngle = 55f;
        public Transform interactionOrigin;
        public LayerMask detectionMask = ~0;

        [Header("References")]
        public ObjectCarryController carryController;

        private readonly Collider[] overlapBuffer = new Collider[64];
        private readonly List<InteractableObject> seenCandidates = new List<InteractableObject>(16);
        private InteractableObject currentTarget;
        private float worldInteractionRange;

        public InteractableObject CurrentTarget => currentTarget;
        public bool HasTarget => currentTarget != null;
        public bool TargetCanBePickedUp =>
            currentTarget != null && currentTarget.CanBePickedUp &&
            (carryController == null || !carryController.IsCarrying);
        public string TargetName => currentTarget != null ? currentTarget.displayName : string.Empty;
        public float WorldInteractionRange => worldInteractionRange;

        private void Awake()
        {
            if (carryController == null) carryController = GetComponent<ObjectCarryController>();
            if (interactionOrigin == null) interactionOrigin = transform;
            worldInteractionRange = GridSettings.ToWorld(interactionRange);
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            bool interactPressed = keyboard != null && keyboard.eKey.wasPressedThisFrame;

            if (carryController != null && carryController.IsCarrying)
            {
                SetTarget(null);
                if (interactPressed) carryController.DropInFront();
                return;
            }

            InteractableObject target = FindBestTarget();
            SetTarget(target);

            if (interactPressed && target != null && target.CanBePickedUp && carryController != null)
            {
                if (carryController.TryPickup(target)) SetTarget(null);
            }
        }

        private InteractableObject FindBestTarget()
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
