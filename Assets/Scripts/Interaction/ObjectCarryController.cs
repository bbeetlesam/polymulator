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
        public PlacementGhost placementGhost;

        private InteractableObject carried;
        private Transform originalParent;

        public bool IsCarrying => carried != null;
        public InteractableObject CarriedObject => carried;
        public string CarriedObjectName => carried != null ? carried.displayName : string.Empty;

        private void Awake()
        {
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
            if (carried != null || target == null || !target.CanBePickedUp || carryAnchor == null)
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
        /// Puts the carried object at the previewed spot in front of Rect in the
        /// same frame. There is no travel animation and Rect is never locked.
        /// </summary>
        public bool DropInFront()
        {
            if (carried == null) return false;

            InteractableObject dropping = carried;
            Vector3 destination = placementGhost != null
                ? placementGhost.GetPlacementPosition(dropping)
                : FallbackDropPosition(dropping);

            dropping.transform.SetParent(originalParent, true);
            originalParent = null;
            dropping.transform.position = destination;
            dropping.transform.rotation = Quaternion.identity;

            dropping.SetCollidersEnabled(true);

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
            if (placementGhost != null) placementGhost.Hide();
            return true;
        }

        private Vector3 FallbackDropPosition(InteractableObject target)
        {
            Vector3 halfExtents = Vector3.one * 0.5f;
            if (target != null && target.TryGetHalfExtents(out Vector3 extents)) halfExtents = extents;

            Vector3 position = transform.position + transform.forward * GridSettings.ToWorld(1.3f);
            position.y = halfExtents.y + GridSettings.ToWorld(0.02f);
            return WorldBounds.ClampInside(position, halfExtents.x, halfExtents.z);
        }

        /// <summary>Immediate release, used for forced cleanup rather than player input.</summary>
        public void Release()
        {
            if (carried == null) return;

            InteractableObject released = carried;
            carried = null;

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
        }

        /// <summary>
        /// Re-anchors the held object each frame, then keeps it inside the platform
        /// even though its colliders are disabled while carried.
        /// </summary>
        private void LateUpdate()
        {
            if (carried == null) return;

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
        }
    }
}
