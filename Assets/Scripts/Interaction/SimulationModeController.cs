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

        public SimulationMode CurrentMode => currentMode;
        public bool IsLimited => currentMode == SimulationMode.Limited;
        public bool IsFlexible => currentMode == SimulationMode.Flexible;

        public Color ModeColor => IsLimited ? PaletteSettings.ModeLimited : PaletteSettings.ModeFlexible;

        public string ModeLabel => IsLimited ? "LIMITED" : "FLEXIBLE";
        public string ModeSummary => IsLimited
            ? "Carry  /  90° Rotate  /  Forward Throw"
            : "Free-Aim Telekinesis  /  Free Elevate & Spin";

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
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private bool prevTabPressed;

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return;

            bool tabDown = keyboard.tabKey.isPressed;
            if (keyboard.tabKey.wasPressedThisFrame || (tabDown && !prevTabPressed))
                ToggleMode();
            prevTabPressed = tabDown;

            // Milestone hooks: Partition & Fusion
            if (keyboard.digit1Key.wasPressedThisFrame)
                TriggerPartitionHook();
            if (keyboard.digit2Key.wasPressedThisFrame)
                TriggerFusionHook();
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
            RectVisualRig.NotifyActionStretch(transform, 0.14f);

            OnModeChanged?.Invoke(currentMode);
            OnStatusBanner?.Invoke(ModeLabel + " MODE ACTIVE", tint);
        }

        public void NotifyBanner(string message, Color color)
        {
            OnStatusBanner?.Invoke(message, color);
        }

        /// <summary>Extension hook for Mahdi's Partition mechanic.</summary>
        public void TriggerPartitionHook()
        {
            InteractableObject target = ActiveOrHighlightedTarget();
            if (target != null && target.canPartition)
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Peach, 1.4f, 0.35f);
                OnStatusBanner?.Invoke("PARTITION [1] // MODULE LOCKED", PaletteSettings.Peach);
            }
            else
            {
                OnStatusBanner?.Invoke("PARTITION [1] // LOCKED", PaletteSettings.Peach);
            }
        }

        /// <summary>Extension hook for Farah's Fusion mechanic.</summary>
        public void TriggerFusionHook()
        {
            InteractableObject target = ActiveOrHighlightedTarget();
            if (target != null && target.canFuse)
            {
                SimulationVFX.Ring(target.transform.position, PaletteSettings.Green, 1.4f, 0.35f);
                OnStatusBanner?.Invoke("FUSION [2] // MODULE LOCKED", PaletteSettings.Green);
            }
            else
            {
                OnStatusBanner?.Invoke("FUSION [2] // LOCKED", PaletteSettings.Green);
            }
        }

        private InteractableObject ActiveOrHighlightedTarget()
        {
            if (carryController != null && carryController.IsCarrying)
                return carryController.CarriedObject;
            if (telekinesisController != null && telekinesisController.HasTether)
                return telekinesisController.TetheredObject;
            return interactor != null ? interactor.CurrentTarget : null;
        }
    }
}
