using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Animates the floating inner crystal core and socket lights of ANCHOR-01,
    /// reacting gently when Rect approaches with a manipulated prop.
    /// </summary>
    public sealed class AnchorVisualRig : MonoBehaviour
    {
        [Header("Core Motion")]
        public Transform targetCore;
        public Transform outerRing;
        public Light anchorLight;
        public float hoverAmplitude = 0.08f;
        public float hoverFrequency = 1.8f;
        public float spinSpeed = 32f;

        [Header("Proximity")]
        [Tooltip("Proximity distance in logical grid units to shift glow intensity.")]
        public float proximityRadiusGrid = 3.2f;

        private Vector3 coreRestLocalPos;
        private float coreBaseRecorded;
        private Transform playerTransform;
        private ObjectCarryController carryController;
        private TelekinesisController telekinesisController;
        private Color baseLightColor;
        private float baseLightIntensity;

        private void Start()
        {
            if (targetCore != null)
            {
                coreRestLocalPos = targetCore.localPosition;
                coreBaseRecorded = 1f;
            }

            if (anchorLight != null)
            {
                baseLightColor = anchorLight.color;
                baseLightIntensity = anchorLight.intensity;
            }
            else
            {
                baseLightColor = PaletteSettings.Teal;
                baseLightIntensity = 1.6f;
            }

            RectController rect = Object.FindFirstObjectByType<RectController>();
            if (rect != null)
            {
                playerTransform = rect.transform;
                carryController = rect.GetComponent<ObjectCarryController>();
                telekinesisController = rect.GetComponent<TelekinesisController>();
            }
        }

        private void Update()
        {
            float t = Time.time;

            if (targetCore != null)
            {
                if (coreBaseRecorded < 0.5f)
                {
                    coreRestLocalPos = targetCore.localPosition;
                    coreBaseRecorded = 1f;
                }

                float hover = Mathf.Sin(t * hoverFrequency) * GridSettings.ToWorld(hoverAmplitude);
                targetCore.localPosition = coreRestLocalPos + Vector3.up * hover;
                targetCore.Rotate(Vector3.up, spinSpeed * Time.deltaTime, Space.Self);
                targetCore.Rotate(Vector3.right, spinSpeed * 0.35f * Time.deltaTime, Space.Self);
            }

            if (outerRing != null)
            {
                outerRing.Rotate(Vector3.up, -spinSpeed * 0.45f * Time.deltaTime, Space.Self);
            }

            if (anchorLight != null)
            {
                bool activeHold = (carryController != null && carryController.IsCarrying) ||
                                  (telekinesisController != null && telekinesisController.HasTether);
                float near = 0f;
                if (playerTransform != null)
                {
                    float dist = Vector3.Distance(playerTransform.position, transform.position);
                    float range = GridSettings.ToWorld(proximityRadiusGrid);
                    if (dist < range)
                        near = 1f - Mathf.Clamp01(dist / Mathf.Max(0.01f, range));
                }

                float pulse = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
                Color goalColor = activeHold && near > 0.1f
                    ? Color.Lerp(baseLightColor, PaletteSettings.Green, near * 0.85f)
                    : baseLightColor;
                float goalIntensity = baseLightIntensity * (0.9f + 0.25f * pulse + near * 0.65f);

                anchorLight.color = Color.Lerp(anchorLight.color, goalColor, Time.deltaTime * 6f);
                anchorLight.intensity = Mathf.Lerp(anchorLight.intensity, goalIntensity, Time.deltaTime * 6f);
            }
        }
    }
}
