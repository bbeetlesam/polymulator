using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Trigger zone attached to each physical choice pedestal in Level5.2.
    /// Kept in its own matching filename so Unity scene serialization resolves its MonoScript cleanly.
    /// </summary>
    public sealed class EndingSocketTrigger : MonoBehaviour
    {
        public EndingChoiceController controller;
        public EndingPath path;
        public Vector3 lockOffset = new Vector3(0f, 0.55f, 0f);

        private void OnTriggerEnter(Collider other)
        {
            TryProcess(other);
        }

        private void OnTriggerStay(Collider other)
        {
            TryProcess(other);
        }

        private void TryProcess(Collider other)
        {
            if (controller == null || controller.HasChosen) return;
            Vector3 lockPos = transform.position + GridSettings.ToWorld(lockOffset);
            controller.TryAcceptObject(path, other, lockPos, transform);
        }
    }
}
