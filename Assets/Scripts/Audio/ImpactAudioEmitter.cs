using UnityEngine;

namespace Polymulator
{
    /// <summary>Emits mass-scaled collision SFX when an interactable rigidbody strikes geometry.</summary>
    [RequireComponent(typeof(Rigidbody))]
    public sealed class ImpactAudioEmitter : MonoBehaviour
    {
        private Rigidbody body;
        private float spawnGraceUntil;

        private void Awake()
        {
            body = GetComponent<Rigidbody>();
            spawnGraceUntil = Time.time + 0.45f;
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (Time.time < spawnGraceUntil || body == null || body.isKinematic) return;
            SimulationAudio.PlayImpact(transform.position, collision.relativeVelocity.magnitude, body.mass);
        }
    }
}
