using UnityEngine;

namespace Polymulator
{
    /// <summary>Emits mass-scaled collision SFX when an interactable rigidbody strikes geometry.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ImpactAudioEmitter : MonoBehaviour
    {
        private Rigidbody body;
        private InteractableObject interactable;
        private float spawnGraceUntil;
        private float rippleCooldownUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            interactable = GetComponent<InteractableObject>();
            spawnGraceUntil = Time.time + 0.45f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < spawnGraceUntil || body == null || body.isKinematic) return;
            float speed = collision.relativeVelocity.magnitude;
            SimulationAudio.PlayImpact(transform.position, speed, body.mass);

            if (speed >= 2.2f && Time.time >= rippleCooldownUntil)
            {
                rippleCooldownUntil = Time.time + 0.25f;
                Color c = interactable != null ? interactable.AssignedColor : PaletteSettings.Sapphire;
                float radius = Mathf.Clamp(2.8f + body.mass * 1.1f, 3.0f, 6.2f);
                float strength = Mathf.Clamp(0.45f + speed * 0.08f, 0.45f, 1.15f);
                SimulationVisualBridge.TriggerFloorRipple(transform.position, c, radius, strength, 0.55f);
            }
        }
    }
}
