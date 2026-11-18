using System.Collections;
using UnityEngine;

namespace Polymulator
{
    public class ObjectCarryController : MonoBehaviour
    {
        [Header("Carry Anchor (grid units)")]
        public Transform carryAnchor;
        [Tooltip("Height of the carried object's centre above Rect's feet. Rect is 1 grid unit tall, so 1.7 clears the head.")]
        public float fallbackCarryHeight = 1.7f;
        [Tooltip("Horizontal offset from Rect. Zero keeps the object directly overhead rather than in front.")]
        public float fallbackCarryForward = 0f;

        [Header("Drop")]
        [Tooltip("Seconds for the object to move from overhead to the placement point.")]
        [Min(0.05f)] public float dropDuration = 0.35f;
        public PlacementGhost placementGhost;

        private InteractableObject carried;
        private Transform originalParent;
        private RectController rectController;
        private bool isDropping;

        public bool IsCarrying => carried != null;
        public bool IsDropping => isDropping;
        public InteractableObject CarriedObject => carried;
        public string CarriedObjectName => carried != null ? carried.displayName : string.Empty;

        private void Awake()
        {
            rectController = GetComponent<RectController>();

            if (carryAnchor == null)
            {
                GameObject anchor = new GameObject("Carry Anchor");
                anchor.transform.SetParent(transform, false);
                anchor.transform.localPosition = new Vector3(
                    0f,
                    GridSettings.ToWorld(fallbackCarryHeight),
                    GridSettings.ToWorld(fallbackCarryForward));
                carryAnchor = anchor.transform;
            }

            if (placementGhost == null)
            {
                GameObject ghost = new GameObject("Placement Ghost");
                placementGhost = ghost.AddComponent<PlacementGhost>();
            }
            placementGhost.Initialize(transform);
        }

        public bool TryPickup(InteractableObject target)
        {
            if (isDropping || carried != null || target == null || !target.CanBePickedUp || carryAnchor == null)
                return false;

            Rigidbody targetBody = target.Body;
            if (targetBody == null) return false;

            carried = target;
            originalParent = target.transform.parent;

            targetBody.linearVelocity = Vector3.zero;
            targetBody.angularVelocity = Vector3.zero;
            targetBody.isKinematic = true;
            targetBody.detectCollisions = false;
            targetBody.interpolation = RigidbodyInterpolation.None;
            target.SetCollidersEnabled(false);
            target.SetHighlight(false, false);

            target.transform.SetParent(carryAnchor, true);
            target.transform.localPosition = Vector3.zero;
            target.transform.localRotation = Quaternion.identity;

            if (placementGhost != null) placementGhost.Show(target);
            return true;
        }

        /// <summary>
        /// Stops Rect and animates the carried object down to the indicated spot.
        /// Interaction stays disabled until the object is back in the simulation.
        /// </summary>
        public bool BeginDrop()
        {
            if (carried == null || isDropping) return false;

            isDropping = true;
            if (rectController != null) rectController.SetMovementLocked(true);
            if (placementGhost != null) placementGhost.Hide();

            StartCoroutine(DropRoutine(carried));
            return true;
        }

        private IEnumerator DropRoutine(InteractableObject dropping)
        {
            Vector3 destination = placementGhost != null
                ? placementGhost.GetPlacementPosition(dropping)
                : FallbackDropPosition(dropping);

            Rigidbody body = dropping.Body;
            Vector3 start = dropping.transform.position;
            Quaternion startRotation = dropping.transform.rotation;

            // Collisions are on during the move, so a prop already in the spot is
            // pushed by physics instead of being clipped through.
            dropping.SetCollidersEnabled(true);
            if (body != null) body.detectCollisions = true;

            float duration = Mathf.Max(0.05f, dropDuration);
            float travelDuration = duration * 0.55f;
            float lowerDuration = duration - travelDuration;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;

                // Move forward at head height first, then lower straight down. A
                // straight lerp would carry the object through Rect's body.
                Vector3 position;
                if (elapsed < travelDuration)
                {
                    float k = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, travelDuration));
                    float eased = 1f - Mathf.Pow(1f - k, 3f);
                    position = new Vector3(
                        Mathf.Lerp(start.x, destination.x, eased),
                        start.y,
                        Mathf.Lerp(start.z, destination.z, eased));
                }
                else
                {
                    float k = Mathf.Clamp01((elapsed - travelDuration) / Mathf.Max(0.01f, lowerDuration));
                    float eased = k * k * (3f - 2f * k);
                    Vector3 overhead = new Vector3(destination.x, start.y, destination.z);
                    position = Vector3.Lerp(overhead, destination, eased);
                }

                if (body != null && body.isKinematic) body.MovePosition(position);
                else dropping.transform.position = position;

                dropping.transform.rotation = Quaternion.Slerp(startRotation, Quaternion.identity,
                    Mathf.Clamp01(elapsed / duration));
                yield return null;
            }

            FinishDrop(dropping, destination);
        }

        private Vector3 FallbackDropPosition(InteractableObject target)
        {
            Vector3 halfExtents = Vector3.one * 0.5f;
            if (target != null && target.TryGetHalfExtents(out Vector3 extents)) halfExtents = extents;

            Vector3 position = transform.position + transform.forward * GridSettings.ToWorld(1.3f);
            position.y = halfExtents.y + GridSettings.ToWorld(0.02f);
            return WorldBounds.ClampInside(position, halfExtents.x, halfExtents.z);
        }

        private void FinishDrop(InteractableObject dropping, Vector3 destination)
        {
            dropping.transform.position = destination;
            dropping.transform.rotation = Quaternion.identity;
            dropping.transform.SetParent(originalParent, true);
            originalParent = null;

            Rigidbody body = dropping.Body;
            if (body != null)
            {
                body.isKinematic = false;
                body.detectCollisions = true;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            carried = null;
            isDropping = false;
            if (rectController != null) rectController.SetMovementLocked(false);
        }

        /// <summary>Immediate release, used for forced cleanup rather than player input.</summary>
        public void Release()
        {
            if (carried == null) return;

            InteractableObject released = carried;
            carried = null;
            isDropping = false;
            StopAllCoroutines();

            if (released.TryGetPlanarHalfExtents(out float halfExtentX, out float halfExtentZ))
                released.transform.position = WorldBounds.ClampInside(
                    released.transform.position, halfExtentX, halfExtentZ);

            released.transform.SetParent(originalParent, true);
            originalParent = null;
            released.SetCollidersEnabled(true);

            Rigidbody body = released.Body;
            if (body != null)
            {
                body.detectCollisions = true;
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = Vector3.zero;
                body.angularVelocity = Vector3.zero;
                body.WakeUp();
            }

            if (placementGhost != null) placementGhost.Hide();
            if (rectController != null) rectController.SetMovementLocked(false);
        }

        /// <summary>
        /// Re-anchors the held object each frame, then keeps it inside the platform
        /// even though its colliders are disabled while carried.
        /// </summary>
        private void LateUpdate()
        {
            if (carried == null || isDropping) return;

            carried.transform.localPosition = Vector3.zero;
            carried.transform.localRotation = Quaternion.identity;

            if (carried.TryGetPlanarHalfExtents(out float halfExtentX, out float halfExtentZ))
                carried.transform.position = WorldBounds.ClampInside(
                    carried.transform.position, halfExtentX, halfExtentZ);
        }

        private void OnDisable()
        {
            if (carried != null) Release();
            if (placementGhost != null) placementGhost.Hide();
            if (rectController != null) rectController.SetMovementLocked(false);
        }
    }
}
