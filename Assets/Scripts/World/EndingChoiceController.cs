using System;
using UnityEngine;

namespace Polymulator
{
    public enum EndingPath
    {
        Restoration,
        Archivist,
        Preservation
    }

    [Serializable]
    public struct EndingOutcome
    {
        public EndingPath path;
        public string title;
        public string badge;
        public string summary;
        public string[] epilogueLines;
        public int fragmentCount;
    }

    /// <summary>
    /// Coordinates the three playable final choices in Level5.2 (Pilihan Akhir Kota Tepi).
    /// Supports Tanggul (Restoration), Menara Pemancar via Partition/Core (Archivist),
    /// and Brankas Arsip (Preservation).
    /// </summary>
    public sealed class EndingChoiceController : MonoBehaviour
    {
        public static EndingChoiceController Instance { get; private set; }

        [Header("Choice Sockets")]
        public Vector3 restorationPos;
        public Vector3 archivistPos;
        public Vector3 preservationPos;
        [Min(0.8f)] public float socketCaptureRadius = 1.45f;
        public WeightedPressurePlate archivistPartitionPlate;

        private bool endingTriggered;
        private EndingOutcome chosenOutcome;

        public bool HasChosen => endingTriggered;
        public EndingOutcome ChosenOutcome => chosenOutcome;

        public event Action<EndingOutcome> OnEndingChosen;
        public event Action<string> OnChoiceFeedback;

        private void Awake()
        {
            Instance = this;
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        public string GetPromptHint()
        {
            return L.T("ending.prompt_hint");
        }

        public bool TryAcceptObject(EndingPath path, Collider other, Vector3 lockWorldPos, Transform socketTransform)
        {
            if (endingTriggered || other == null) return false;

            InteractableObject obj = other.GetComponentInParent<InteractableObject>();
            if (obj == null) return false;
            if (obj.GetComponent<RectController>() != null) return false;

            bool isCore = obj.name.IndexOf("CORE-01", StringComparison.OrdinalIgnoreCase) >= 0;
            if (!isCore)
            {
                OnChoiceFeedback?.Invoke(L.T("ending.feedback.requires_core"));
                return false;
            }

            // Archivist path honours Partition [1]: accept either a partitioned Core piece
            // OR an intact Core once at least one partitioned counterweight rests on the relay plate.
            if (path == EndingPath.Archivist && obj.partitionDepth <= 0)
            {
                bool plateActivated = archivistPartitionPlate != null && archivistPartitionPlate.IsActivated;
                if (!plateActivated)
                {
                    OnChoiceFeedback?.Invoke(L.T("ending.feedback.archivist_requires_partition"));
                    return false;
                }
            }

            LockObjectIntoSocket(obj, lockWorldPos, socketTransform);
            TriggerOutcome(path, lockWorldPos);
            return true;
        }

        private static void LockObjectIntoSocket(InteractableObject obj, Vector3 lockWorldPos, Transform socketTransform)
        {
            ObjectCarryController carry = FindAnyObjectByType<ObjectCarryController>();
            if (carry != null && carry.CarriedObject == obj) carry.ReleaseAndReturn();

            TelekinesisController tk = FindAnyObjectByType<TelekinesisController>();
            if (tk != null && tk.TetheredObject == obj) tk.Release(false);

            if (obj.TryGetComponent<Rigidbody>(out var rb))
            {
                rb.linearVelocity = Vector3.zero;
                rb.angularVelocity = Vector3.zero;
                rb.isKinematic = true;
            }

            obj.canBePickedUp = false;
            obj.canTelekinesis = false;
            obj.canThrow = false;
            obj.canRotate = false;
            obj.canPartition = false;
            obj.canFuse = false;
            obj.transform.position = lockWorldPos;
            if (socketTransform != null) obj.transform.rotation = socketTransform.rotation;
        }

        private void TriggerOutcome(EndingPath path, Vector3 fxPos)
        {
            endingTriggered = true;

            Color fxColor = path switch
            {
                EndingPath.Archivist => PaletteSettings.Teal,
                EndingPath.Restoration => PaletteSettings.Sapphire,
                _ => PaletteSettings.Lavender
            };

            SimulationVFX.Burst(fxPos, fxColor, 26, 3.2f);
            SimulationVFX.Ring(fxPos, fxColor, 2.4f, 0.55f);
            SimulationVisualBridge.TriggerFloorRipple(fxPos, fxColor, 6.5f, 1.0f, 0.72f);

            SimulationAudio.PlayAnchorLock(fxPos);
            SimulationAudio.EnsureInstance()?.PlayAdiPianoMotif();

            int fragCount = SimulationProgress.GetCollectedFragmentCount();
            SimulationProgress.RecordEnding(path.ToString());

            chosenOutcome = BuildOutcome(path, fragCount);
            OnEndingChosen?.Invoke(chosenOutcome);
        }

        private static string Key(EndingPath path, string suffix)
        {
            return "ending." + path.ToString().ToLowerInvariant() + "." + suffix;
        }

        private static EndingOutcome BuildOutcome(EndingPath path, int fragCount)
        {
            return new EndingOutcome
            {
                path = path,
                badge = L.T(Key(path, "badge")),
                title = L.T(Key(path, "title")),
                summary = L.T(Key(path, "summary")),
                epilogueLines = new[]
                {
                    L.T(Key(path, "epilogue.0")),
                    L.T(Key(path, "epilogue.1")),
                    L.T(Key(path, "epilogue.2"))
                },
                fragmentCount = fragCount
            };
        }
    }
}
