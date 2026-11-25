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
            return "Pilihan Akhir: Bawa CORE-01 ke Timur (Tanggul), Utara (Pemancar + Partition [1]), atau Barat (Brankas)";
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
                OnChoiceFeedback?.Invoke("Soket pilihan akhir memerlukan CORE-01.");
                return false;
            }

            // Archivist path honours Partition [1]: accept either a partitioned Core piece
            // OR an intact Core once at least one partitioned counterweight rests on the relay plate.
            if (path == EndingPath.Archivist && obj.partitionDepth <= 0)
            {
                bool plateActivated = archivistPartitionPlate != null && archivistPartitionPlate.IsActivated;
                if (!plateActivated)
                {
                    OnChoiceFeedback?.Invoke("Pemancar Warga: belah CORE-01 dengan [1] Partition atau aktifkan pelat pemancar di tengah.");
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

        private static EndingOutcome BuildOutcome(EndingPath path, int fragCount)
        {
            switch (path)
            {
                case EndingPath.Archivist:
                    return new EndingOutcome
                    {
                        path = path,
                        badge = "Akhir Cerita C  •  Arsip Terbuka",
                        title = "Suara Kota Tepi Mengudara",
                        summary = "Dengan Partition [1], inti energi terbagi: setengah menutup pintu air darurat, setengah lagi menyalakan Menara Pemancar warga.",
                        epilogueLines = new[]
                        {
                            "Vela memanjat pagar observasi saat sinyal radio lama menyala lagi. Bukan suara sistem SIM, melainkan rekaman suara warga yang dulu menanam pesan di ruang sunyi.",
                            "Danu tidak pernah kembali ke menara kendali, namun partitur pianonya berbunyi di seluruh pengeras suara Kota Tepi sebelum badai reda.",
                            "Kamu berdiri sebagai Rect di tepi dermaga: sistem simulasi perlahan terbuka, memberi ruang bagi kota untuk mengingat siapa dirinya."
                        },
                        fragmentCount = fragCount
                    };

                case EndingPath.Restoration:
                    return new EndingOutcome
                    {
                        path = path,
                        badge = "Akhir Cerita A  •  Tanggul Tertutup",
                        title = "Kota Selamat, Ingatan Terkubur",
                        summary = "Seluruh daya inti dialirkan penuh ke Tanggul Utara. Gelombang badai tertahan di luar dinding, tetapi seluruh bank memori bawah tanah ikut terendam.",
                        epilogueLines = new[]
                        {
                            "Air laut surut meninggalkan dermaga yang kering dan jalan pasar yang utuh. Mira membantu warga kembali menyalakan lampu rumah susun.",
                            "Di gedung arsip yang membeku sepi, layar SIM menampilkan status hijau. Tidak ada nama yang tersisa di daftar sejarah, hanya koordinat bangunan yang rapi.",
                            "Rect menatap laut dari balik tanggul, menyimpan satu nada piano yang entah milik siapa."
                        },
                        fragmentCount = fragCount
                    };

                default:
                    return new EndingOutcome
                    {
                        path = path,
                        badge = "Akhir Cerita B  •  Brankas Abadi",
                        title = "Ingatan Dijaga di Balik Kaca",
                        summary = "Inti energi dimasukkan ke Brankas Arsip Barat. Seluruh foto, surat, dan rekaman Kota Tepi terlindungi sempurna dari korosi badai.",
                        epilogueLines = new[]
                        {
                            "Di luar dinding arsip, ombak masih menghantam pondasi lama. Beberapa dermaga harus ditinggalkan sementara warga berpindah ke dataran tinggi.",
                            "Namun di dalam brankas, lampu kuning-hangat tetap menyala. Setiap fragmen yang kamu kumpulkan tersusun rapi agar generasi berikutnya masih tahu lagu kota ini.",
                            "Sistem berhenti menghitung skor; yang tersisa hanya penjaga sunyi bernama Rect."
                        },
                        fragmentCount = fragCount
                    };
            }
        }
    }
}
