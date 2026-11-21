using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    [RequireComponent(typeof(CharacterController))]
    public class RectController : MonoBehaviour
    {
        [Header("Movement (grid units)")]
        public float moveSpeed = 3.5f;
        public float acceleration = 18f;
        public float deceleration = 22f;
        public float turnSpeed = 12f;
        public float gravity = -24f;
        public float groundedStickForce = -2f;

        [Header("References")]
        public Transform cameraTransform;

        private CharacterController controller;
        private Vector3 planarVelocity;
        private float verticalVelocity;

        private float worldMoveSpeed;
        private float worldAcceleration;
        private float worldDeceleration;
        private float worldGravity;
        private float worldGroundedStickForce;

        private bool movementLocked;
        private float angularTurnRate;

        public float CurrentSpeed => new Vector3(planarVelocity.x, 0f, planarVelocity.z).magnitude;
        public float SpeedNormalized => worldMoveSpeed > 0.01f ? Mathf.Clamp01(CurrentSpeed / worldMoveSpeed) : 0f;
        public Vector3 PlanarVelocity => planarVelocity;
        public float AngularTurnRate => angularTurnRate;
        public bool IsGrounded => controller != null && controller.isGrounded;
        public bool MovementLocked => movementLocked;

        /// <summary>
        /// Halts horizontal movement immediately. Used while an object is being put
        /// down so the placement point stays fixed and no interaction happens mid-drop.
        /// </summary>
        public void SetMovementLocked(bool locked)
        {
            movementLocked = locked;
            if (locked) planarVelocity = Vector3.zero;
        }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            CacheWorldMovementValues();

            if (cameraTransform == null && Camera.main != null)
                cameraTransform = Camera.main.transform;
        }

        private void CacheWorldMovementValues()
        {
            worldMoveSpeed = GridSettings.ToWorld(moveSpeed);
            worldAcceleration = GridSettings.ToWorld(acceleration);
            worldDeceleration = GridSettings.ToWorld(deceleration);
            worldGravity = GridSettings.ToWorld(gravity);
            worldGroundedStickForce = GridSettings.ToWorld(groundedStickForce);
        }

        private void Update()
        {
            if (controller == null) return;

            Keyboard keyboard = Keyboard.current;
            Vector2 input = Vector2.zero;
            if (keyboard != null)
            {
                if (keyboard.wKey.isPressed || keyboard.upArrowKey.isPressed) input.y += 1f;
                if (keyboard.sKey.isPressed || keyboard.downArrowKey.isPressed) input.y -= 1f;
                if (keyboard.dKey.isPressed || keyboard.rightArrowKey.isPressed) input.x += 1f;
                if (keyboard.aKey.isPressed || keyboard.leftArrowKey.isPressed) input.x -= 1f;
            }
            if (input.sqrMagnitude > 1f) input.Normalize();
            if (movementLocked)
            {
                input = Vector2.zero;
                planarVelocity = Vector3.zero;
            }

            Vector3 cameraForward = cameraTransform != null
                ? Vector3.ProjectOnPlane(cameraTransform.forward, Vector3.up)
                : Vector3.forward;
            if (cameraForward.sqrMagnitude < 0.001f) cameraForward = Vector3.forward;
            cameraForward.Normalize();
            Vector3 cameraRight = Vector3.Cross(Vector3.up, cameraForward);

            Vector3 desiredDirection = cameraForward * input.y + cameraRight * input.x;
            Vector3 targetVelocity = desiredDirection * worldMoveSpeed;
            float rate = input.sqrMagnitude > 0.01f ? worldAcceleration : worldDeceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, rate * Time.deltaTime);

            if (controller.isGrounded)
            {
                if (verticalVelocity < 0f) verticalVelocity = worldGroundedStickForce;
            }
            else
            {
                verticalVelocity += worldGravity * Time.deltaTime;
            }

            Vector3 motion = planarVelocity;
            motion.y = verticalVelocity;
            controller.Move(motion * Time.deltaTime);

            Vector3 flatVelocity = new Vector3(planarVelocity.x, 0f, planarVelocity.z);
            if (flatVelocity.sqrMagnitude > 0.01f)
            {
                Quaternion prevRot = transform.rotation;
                Quaternion targetRotation = Quaternion.LookRotation(flatVelocity.normalized, Vector3.up);
                transform.rotation = Quaternion.Slerp(prevRot, targetRotation,
                    1f - Mathf.Exp(-turnSpeed * Time.deltaTime));
                float yawDelta = Mathf.DeltaAngle(prevRot.eulerAngles.y, transform.rotation.eulerAngles.y);
                angularTurnRate = Mathf.Lerp(angularTurnRate, yawDelta / Mathf.Max(0.001f, Time.deltaTime), 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
            else
            {
                angularTurnRate = Mathf.Lerp(angularTurnRate, 0f, 1f - Mathf.Exp(-10f * Time.deltaTime));
            }
        }

        private void OnValidate()
        {
            moveSpeed = Mathf.Max(0.1f, moveSpeed);
            acceleration = Mathf.Max(0.1f, acceleration);
            deceleration = Mathf.Max(0.1f, deceleration);
            turnSpeed = Mathf.Max(0.1f, turnSpeed);
            gravity = Mathf.Min(-0.1f, gravity);
        }
    }
}
