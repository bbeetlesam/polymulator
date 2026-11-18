using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Central colour definition for POLYMULATOR. Materials, lighting and UI all
    /// read from this asset so the palette can be changed in one place.
    /// The serialized defaults below double as the runtime fallback when no
    /// PaletteSettings asset has been created yet.
    /// </summary>
    [CreateAssetMenu(fileName = "PaletteSettings", menuName = "POLYMULATOR/Palette Settings")]
    public sealed class PaletteSettings : ScriptableObject
    {
        public const string ResourceName = "PaletteSettings";

        [Header("Environment")]
        [SerializeField] private Color mantle = Hex(0x11, 0x11, 0x1B);
        [SerializeField] private Color baseSurface = Hex(0x1E, 0x1E, 0x2E);
        [SerializeField] private Color crust = Hex(0x18, 0x18, 0x25);

        [Header("Typography")]
        [SerializeField] private Color textPrimary = Hex(0xCD, 0xD6, 0xF4);
        [SerializeField] private Color textSecondary = Hex(0xBA, 0xC2, 0xDE);
        [SerializeField] private Color textSubtext = Hex(0xA6, 0xAD, 0xC8);

        [Header("Accents")]
        [SerializeField] private Color sapphire = Hex(0x74, 0xC7, 0xEC);
        [SerializeField] private Color green = Hex(0xA6, 0xE3, 0xA1);
        [SerializeField] private Color red = Hex(0xF3, 0x8B, 0xA8);
        [SerializeField] private Color peach = Hex(0xFA, 0xB3, 0x87);
        [SerializeField] private Color lavender = Hex(0xB4, 0xBE, 0xFE);

        [Header("Interactive Object Palette")]
        [SerializeField] private Color rosewater = Hex(0xF5, 0xE0, 0xDC);
        [SerializeField] private Color flamingo = Hex(0xF2, 0xCD, 0xCD);
        [SerializeField] private Color pink = Hex(0xF5, 0xC2, 0xE7);
        [SerializeField] private Color mauve = Hex(0xCB, 0xA6, 0xF7);
        [SerializeField] private Color maroon = Hex(0xEB, 0xA0, 0xAC);
        [SerializeField] private Color yellow = Hex(0xF9, 0xE2, 0xAF);
        [SerializeField] private Color teal = Hex(0x94, 0xE2, 0xD5);
        [SerializeField] private Color sky = Hex(0x89, 0xDC, 0xEB);
        [SerializeField] private Color blue = Hex(0x89, 0xB4, 0xFA);

        [Header("Overlays")]
        [SerializeField] private Color overlay0 = Hex(0x93, 0x99, 0xB2);
        [SerializeField] private Color overlay1 = Hex(0x7F, 0x84, 0x9C);
        [SerializeField] private Color overlay2 = Hex(0x6C, 0x70, 0x86);

        private static PaletteSettings cachedInstance;
        private static PaletteSettings fallbackInstance;

        /// <summary>The project palette asset, or an in-memory default if none exists.</summary>
        public static PaletteSettings Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<PaletteSettings>(ResourceName);
                if (cachedInstance != null) return cachedInstance;

                if (fallbackInstance == null)
                    fallbackInstance = CreateInstance<PaletteSettings>();
                return fallbackInstance;
            }
        }

        public static Color Mantle => Instance.mantle;
        public static Color BaseSurface => Instance.baseSurface;
        public static Color Crust => Instance.crust;

        public static Color TextPrimary => Instance.textPrimary;
        public static Color TextSecondary => Instance.textSecondary;
        public static Color TextSubtext => Instance.textSubtext;

        public static Color Sapphire => Instance.sapphire;
        public static Color Green => Instance.green;
        public static Color Red => Instance.red;
        public static Color Peach => Instance.peach;
        public static Color Lavender => Instance.lavender;

        public static Color Rosewater => Instance.rosewater;
        public static Color Flamingo => Instance.flamingo;
        public static Color Pink => Instance.pink;
        public static Color Mauve => Instance.mauve;
        public static Color Maroon => Instance.maroon;
        public static Color Yellow => Instance.yellow;
        public static Color Teal => Instance.teal;
        public static Color Sky => Instance.sky;
        public static Color Blue => Instance.blue;

        /// <summary>Names matching <see cref="InteractivePalette"/>, used for asset and material naming.</summary>
        public static readonly string[] InteractiveColorNames =
        {
            "Rosewater", "Flamingo", "Pink", "Mauve", "Red", "Maroon", "Peach",
            "Yellow", "Green", "Teal", "Sky", "Sapphire", "Blue", "Lavender"
        };

        /// <summary>The 14-colour palette used for interactive objects, in reference order.</summary>
        public static Color[] InteractivePalette
        {
            get
            {
                return new[]
                {
                    Rosewater, Flamingo, Pink, Mauve, Red, Maroon, Peach,
                    Yellow, Green, Teal, Sky, Sapphire, Blue, Lavender
                };
            }
        }

        public static Color Overlay0 => Instance.overlay0;
        public static Color Overlay1 => Instance.overlay1;
        public static Color Overlay2 => Instance.overlay2;

        public static void ClearCache()
        {
            cachedInstance = null;
            fallbackInstance = null;
        }

        private void OnValidate()
        {
            ClearCache();
        }

        private static Color Hex(byte r, byte g, byte b)
        {
            return new Color32(r, g, b, 255);
        }
    }
}
