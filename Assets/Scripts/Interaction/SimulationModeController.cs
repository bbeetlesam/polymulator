using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Polymulator
{
    public enum SimulationMode
    {
        Limited = 0,
        Flexible = 1
    }

    /// <summary>
    /// Controls active POLYMULATOR manipulation mode (Limited vs Flexible) and exposes
    /// action events plus clean status hooks for upcoming Partition (Mahdi) and Fusion (Farah) abilities.
    /// </summary>
    public sealed class SimulationModeController : MonoBehaviour
    {
        public static SimulationModeController Instance { get; private set; }

        [Header("Mode")]
        [SerializeField] private SimulationMode currentMode = SimulationMode.Limited;

        [Header("References")]
        public ObjectCarryController carryController;
        public TelekinesisController telekinesisController;
        public ObjectInteractor interactor;
        public PartitionFusionController partitionFusionController;

        private bool levelAllowsPartition;
        private bool levelAllowsFusion;

        public SimulationMode CurrentMode => currentMode;
        public bool IsLimited => currentMode == SimulationMode.Limited;
        public bool IsFlexible => currentMode == SimulationMode.Flexible;
        public bool CanUsePartition => levelAllowsPartition || SimulationProgress.IsDemoUnlocked;
        public bool CanUseFusion => levelAllowsFusion || SimulationProgress.IsDemoUnlocked;

        public Color ModeColor => IsLimited ? PaletteSettings.ModeLimited : PaletteSettings.ModeFlexible;

        public string ModeLabel => IsLimited ? "LIMITED" : "FLEXIBLE";
        public string ModeSummary => IsLimited
            ? "Gerak Terarah  /  Putar 90°  /  Lempar"
            : "Gerak Bebas  /  Telekinesis";

        public event Action<SimulationMode> OnModeChanged;
        public event Action<string, Color> OnStatusBanner;

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(this);
                return;
            }
            Instance = this;

            if (carryController == null) carryController = GetComponent<ObjectCarryController>();
            if (telekinesisController == null) telekinesisController = GetComponent<TelekinesisController>();
            if (interactor == null) interactor = GetComponent<ObjectInteractor>();
            if (partitionFusionController == null)
            {
                partitionFusionController = GetComponent<PartitionFusionController>();
                if (partitionFusionController == null)
                    partitionFusionController = gameObject.AddComponent<PartitionFusionController>();
            }

            string sceneName = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            LevelInfo info = Resources.Load<LevelInfo>("Levels/" + sceneName);
            if (info != null)
            {
                levelAllowsPartition = info.AllowPartition;
                levelAllowsFusion = info.AllowFusion;
            }
            else if (sceneName == "LevelShowcase")
            {
                levelAllowsPartition = true;
                levelAllowsFusion = true;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool prevTabPressed;

        private void Update()
        {
            if (Time.timeScale <= 0f) return;
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool tabDown = keyboard.tabKey.isPressed;
            if (keyboard.tabKey.wasPressedThisFrame || (tabDown && !prevTabPressed))
                ToggleMode();
            prevTabPressed = tabDown;

            // Milestone hooks: Partition [1], Fusion [2], Cut-Axis [3]
            if (keyboard.digit1Key.wasPressedThisFrame)
                TriggerPartitionHook();
            if (keyboard.digit2Key.wasPressedThisFrame)
                TriggerFusionHook();
            if (keyboard.digit3Key.wasPressedThisFrame)
                TriggerCycleCutAxisHook();
        }

        public void ToggleMode()
        {
            SetMode(IsLimited ? SimulationMode.Flexible : SimulationMode.Limited);
        }

        public void SetMode(SimulationMode nextMode)
        {
            if (currentMode == nextMode) return;

            if (carryController != null && carryController.IsCarrying)
                carryController.Release();

            if (telekinesisController != null && telekinesisController.HasTether)
                telekinesisController.Release(false);

            currentMode = nextMode;
            Color tint = ModeColor;

            SimulationAudio.PlayModeSwitch(IsFlexible);
            SimulationVFX.Ring(transform.position, tint, 1.85f, 0.42f);
            SimulationVFX.Burst(transform.position + Vector3.up * GridSettings.ToWorld(0.6f), tint, 16, 2.2f);
            SimulationVisualBridge.TriggerFloorRipple(transform.position, tint, 4.8f, 0.95f, 0.58f);
            RectVisualRig.NotifyActionStretch(transform, 0.14f);

            OnModeChanged?.Invoke(currentMode);
            OnStatusBanner?.Invoke(ModeLabel + " MODE ACTIVE", tint);
        }

        public void NotifyBanner(string message, Color color)
        {
            OnStatusBanner?.Invoke(message, color);
        }

        /// <summary>Executes Partition [1] when unlocked in this chamber or Showcase/Demo mode.</summary>
        public void TriggerPartitionHook()
        {
            if (!CanUsePartition)
            {
                OnStatusBanner?.Invoke("PARTITION [1] belum aktif di chapter ini", PaletteSettings.Peach);
                return;
            }
            partitionFusionController?.TryPartition();
        }

        /// <summary>Executes Fusion [2] when unlocked in this chamber or Showcase/Demo mode.</summary>
        public void TriggerFusionHook()
        {
            if (!CanUseFusion)
            {
                OnStatusBanner?.Invoke("FUSION [2] belum aktif di chapter ini", PaletteSettings.Green);
                return;
            }
            partitionFusionController?.TryFusion();
        }

        /// <summary>Cycles Partition [1] cut-plane orientation ([3]) when unlocked.</summary>
        public void TriggerCycleCutAxisHook()
        {
            if (!CanUsePartition)
            {
                OnStatusBanner?.Invoke("PARTITION [1] belum aktif di chapter ini", PaletteSettings.Peach);
                return;
            }
            partitionFusionController?.CycleCutAxis();
        }
    }
}
