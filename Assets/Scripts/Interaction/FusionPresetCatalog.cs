using System;
using UnityEngine;

namespace Polymulator
{
    public enum FusionShapeKind
    {
        Box = 0,
        Cylinder = 1,
        Sphere = 2,
        Compound = 3
    }

    public enum FusionJoineryStyle
    {
        ClampRails = 0,
        TwinTurbineBridge = 1,
        AxleFlangeNozzle = 2,
        MagneticClawSocket = 3,
        DumbbellAxle = 4,
        EmitterCrown = 5,
        ModularBracket = 6
    }

    [Serializable]
    public sealed class FusionPresetDefinition
    {
        [Header("Identity")]
        public string presetId = "box_box";
        public string horizontalName = "BALOK-GANDA";
        public string verticalTopName = "PILAR-GANDA";

        [Header("Shape Pair (order-independent)")]
        public FusionShapeKind shapeA = FusionShapeKind.Box;
        public FusionShapeKind shapeB = FusionShapeKind.Box;

        [Header("Geometry & Joinery")]
        public FusionJoineryStyle joineryStyle = FusionJoineryStyle.ClampRails;
        [Tooltip("Contact spacing in grid units for horizontal attachments (negative = flush embed, positive = bridge gap).")]
        public float horizontalGapGrid = -0.02f;
        [Tooltip("Contact spacing in grid units for vertical top attachments.")]
        public float verticalGapGrid = -0.02f;
        [Tooltip("When true and attaching a cylinder horizontally onto a box or sphere, tilts the cylinder out along the face normal like an axle/nozzle.")]
        public bool orientHeldCylinderAlongSide;
        public Color accentTint = new Color(0.58039f, 0.88627f, 0.83529f, 1f);

        public string GetDisplayName(bool isVerticalTop)
        {
            return isVerticalTop ? verticalTopName : horizontalName;
        }

        public float GetGapGrid(bool isVerticalTop)
        {
            return isVerticalTop ? verticalGapGrid : horizontalGapGrid;
        }

        public bool Matches(FusionShapeKind a, FusionShapeKind b)
        {
            return (shapeA == a && shapeB == b) || (shapeA == b && shapeB == a);
        }
    }

    /// <summary>
    /// Designer-editable ScriptableObject catalog of POLYMULATOR Fusion shape presets.
    /// Maps every combination of base shapes (Balok, Tabung, Bola, Rakitan) and attachment
    /// orientation (horizontal side vs vertical top) to a unique named mechanical assembly,
    /// spacing rule, cylinder orientation, and Catppuccin Mocha joinery style.
    /// </summary>
    [CreateAssetMenu(fileName = "FusionPresetCatalog", menuName = "POLYMULATOR/Fusion Preset Catalog")]
    public sealed class FusionPresetCatalog : ScriptableObject
    {
        public const string ResourceName = "FusionPresetCatalog";

        [SerializeField] private FusionPresetDefinition[] presets = Array.Empty<FusionPresetDefinition>();

        private static FusionPresetCatalog cachedAsset;
        private static FusionPresetCatalog runtimeFallback;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetCaches()
        {
            cachedAsset = null;
            if (runtimeFallback != null)
            {
                Destroy(runtimeFallback);
                runtimeFallback = null;
            }
        }

        public static FusionPresetCatalog Active
        {
            get
            {
                if (cachedAsset == null)
                    cachedAsset = Resources.Load<FusionPresetCatalog>(ResourceName);
                if (cachedAsset != null && cachedAsset.presets != null && cachedAsset.presets.Length > 0)
                    return cachedAsset;

                if (runtimeFallback == null)
                {
                    runtimeFallback = CreateInstance<FusionPresetCatalog>();
                    runtimeFallback.hideFlags = HideFlags.HideAndDontSave;
                    runtimeFallback.PopulateDefaultPresets();
                }
                return runtimeFallback;
            }
        }

        public FusionPresetDefinition[] Presets => presets;

        public void PopulateDefaultPresets()
        {
            presets = new[]
            {
                new FusionPresetDefinition
                {
                    presetId = "cylinder_cylinder",
                    horizontalName = "TURBIN-GANDA",
                    verticalTopName = "PISTON-MENARA",
                    shapeA = FusionShapeKind.Cylinder,
                    shapeB = FusionShapeKind.Cylinder,
                    joineryStyle = FusionJoineryStyle.TwinTurbineBridge,
                    horizontalGapGrid = 0.22f,
                    verticalGapGrid = 0.06f,
                    orientHeldCylinderAlongSide = false,
                    accentTint = PaletteSettings.Sapphire
                },
                new FusionPresetDefinition
                {
                    presetId = "cylinder_box",
                    horizontalName = "MODUL-POROS",
                    verticalTopName = "TURRET-SILINDER",
                    shapeA = FusionShapeKind.Cylinder,
                    shapeB = FusionShapeKind.Box,
                    joineryStyle = FusionJoineryStyle.AxleFlangeNozzle,
                    horizontalGapGrid = -0.02f,
                    verticalGapGrid = 0.02f,
                    orientHeldCylinderAlongSide = true,
                    accentTint = PaletteSettings.Peach
                },
                new FusionPresetDefinition
                {
                    presetId = "box_sphere",
                    horizontalName = "SOKET-ORB",
                    verticalTopName = "PEDESTAL-ORB",
                    shapeA = FusionShapeKind.Box,
                    shapeB = FusionShapeKind.Sphere,
                    joineryStyle = FusionJoineryStyle.MagneticClawSocket,
                    horizontalGapGrid = 0.12f,
                    verticalGapGrid = 0.14f,
                    orientHeldCylinderAlongSide = false,
                    accentTint = PaletteSettings.Mauve
                },
                new FusionPresetDefinition
                {
                    presetId = "sphere_sphere",
                    horizontalName = "BARBEL-ORB",
                    verticalTopName = "INTI-KEMBAR",
                    shapeA = FusionShapeKind.Sphere,
                    shapeB = FusionShapeKind.Sphere,
                    joineryStyle = FusionJoineryStyle.DumbbellAxle,
                    horizontalGapGrid = 0.30f,
                    verticalGapGrid = 0.22f,
                    orientHeldCylinderAlongSide = false,
                    accentTint = PaletteSettings.Teal
                },
                new FusionPresetDefinition
                {
                    presetId = "cylinder_sphere",
                    horizontalName = "PISTON-ORB",
                    verticalTopName = "MERCU-ORB",
                    shapeA = FusionShapeKind.Cylinder,
                    shapeB = FusionShapeKind.Sphere,
                    joineryStyle = FusionJoineryStyle.EmitterCrown,
                    horizontalGapGrid = 0.12f,
                    verticalGapGrid = 0.12f,
                    orientHeldCylinderAlongSide = true,
                    accentTint = PaletteSettings.Yellow
                },
                new FusionPresetDefinition
                {
                    presetId = "box_box",
                    horizontalName = "BALOK-GANDA",
                    verticalTopName = "PILAR-GANDA",
                    shapeA = FusionShapeKind.Box,
                    shapeB = FusionShapeKind.Box,
                    joineryStyle = FusionJoineryStyle.ClampRails,
                    horizontalGapGrid = -0.02f,
                    verticalGapGrid = -0.02f,
                    orientHeldCylinderAlongSide = false,
                    accentTint = PaletteSettings.Green
                },
                new FusionPresetDefinition
                {
                    presetId = "compound_any",
                    horizontalName = "RAKITAN-MODULAR",
                    verticalTopName = "MENARA-MODULAR",
                    shapeA = FusionShapeKind.Compound,
                    shapeB = FusionShapeKind.Compound,
                    joineryStyle = FusionJoineryStyle.ModularBracket,
                    horizontalGapGrid = -0.01f,
                    verticalGapGrid = -0.01f,
                    orientHeldCylinderAlongSide = false,
                    accentTint = PaletteSettings.Teal
                }
            };
        }

        public FusionPresetDefinition ResolvePreset(FusionShapeKind a, FusionShapeKind b)
        {
            if (presets == null || presets.Length == 0)
                PopulateDefaultPresets();

            // Exact pair match first
            for (int i = 0; i < presets.Length; i++)
            {
                if (presets[i] != null && presets[i].Matches(a, b))
                    return presets[i];
            }

            // If either side is already a multi-part compound, return modular compound preset
            if (a == FusionShapeKind.Compound || b == FusionShapeKind.Compound)
            {
                for (int i = 0; i < presets.Length; i++)
                {
                    if (presets[i] != null && presets[i].joineryStyle == FusionJoineryStyle.ModularBracket)
                        return presets[i];
                }
            }

            return presets.Length > 0 ? presets[0] : new FusionPresetDefinition();
        }

        public static FusionShapeKind ClassifyShape(InteractableObject obj)
        {
            if (obj == null) return FusionShapeKind.Box;

            if (obj.isFusedComposite && obj.GetComponentsInChildren<FusedSubPart>(true).Length >= 2)
                return FusionShapeKind.Compound;

            if (obj.GetComponent<SphereCollider>() != null)
                return FusionShapeKind.Sphere;

            string nameUpper = obj.displayName != null ? obj.displayName.ToUpperInvariant() : string.Empty;
            if (nameUpper.Contains("BOLA") || nameUpper.StartsWith("ORB") || nameUpper.Contains("SPHERE"))
                return FusionShapeKind.Sphere;

            if (obj.GetComponent<CapsuleCollider>() != null)
                return FusionShapeKind.Cylinder;

            Renderer[] baseRenderers = obj.GetBaseRenderers();
            if (baseRenderers != null)
            {
                for (int i = 0; i < baseRenderers.Length; i++)
                {
                    Renderer r = baseRenderers[i];
                    if (r == null) continue;
                    MeshFilter mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    string meshName = mf.sharedMesh.name != null ? mf.sharedMesh.name.ToLowerInvariant() : string.Empty;
                    if (meshName.Contains("sphere") || meshName.Contains("orb"))
                        return FusionShapeKind.Sphere;
                    if (meshName.Contains("cylinder") || meshName.Contains("octagon") || meshName.Contains("capsule"))
                        return FusionShapeKind.Cylinder;
                }
            }

            if (nameUpper.Contains("TABUNG") || nameUpper.StartsWith("CYL"))
                return FusionShapeKind.Cylinder;

            return FusionShapeKind.Box;
        }

        public static string GetShapeLabelIndonesian(FusionShapeKind kind)
        {
            return kind switch
            {
                FusionShapeKind.Box => "Balok",
                FusionShapeKind.Cylinder => "Tabung",
                FusionShapeKind.Sphere => "Bola",
                FusionShapeKind.Compound => "Rakitan",
                _ => "Balok"
            };
        }
    }
}
