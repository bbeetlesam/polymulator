using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Central definition of the world sizing convention.
    /// Every logical dimension in the prototype is expressed in grid units and
    /// converted to world-space metres through this asset.
    /// </summary>
    [CreateAssetMenu(fileName = "GridSettings", menuName = "POLYMULATOR/Grid Settings")]
    public sealed class GridSettings : ScriptableObject
    {
        public const string ResourceName = "GridSettings";
        public const float DefaultGridUnitSize = 1f;

        [SerializeField, Min(0.01f)]
        [Tooltip("World-space size of one logical grid unit. A standard cube is 1 x 1 x 1 grid units.")]
        private float gridUnitSize = DefaultGridUnitSize;

        private static GridSettings cachedInstance;

        /// <summary>World-space size of one logical grid unit.</summary>
        public float GridUnitSize => Mathf.Max(0.01f, gridUnitSize);

        /// <summary>The project's grid settings asset, loaded from Resources.</summary>
        public static GridSettings Instance
        {
            get
            {
                if (cachedInstance == null)
                    cachedInstance = Resources.Load<GridSettings>(ResourceName);
                return cachedInstance;
            }
        }

        /// <summary>World-space size of one grid unit, with a safe fallback.</summary>
        public static float Unit => Instance != null ? Instance.GridUnitSize : DefaultGridUnitSize;

        /// <summary>Converts a logical length in grid units to world units.</summary>
        public static float ToWorld(float gridUnits) => gridUnits * Unit;

        /// <summary>Converts a logical position/size in grid units to world units.</summary>
        public static Vector3 ToWorld(Vector3 gridUnits) => gridUnits * Unit;

        /// <summary>Converts a world-space length to logical grid units.</summary>
        public static float ToGrid(float worldUnits) => worldUnits / Unit;

        /// <summary>Changes the configured grid size and clears the cached asset reference.</summary>
        public void SetGridUnitSize(float value)
        {
            gridUnitSize = Mathf.Max(0.01f, value);
            ClearCache();
        }

        public static void ClearCache()
        {
            cachedInstance = null;
        }

        private void OnValidate()
        {
            gridUnitSize = Mathf.Max(0.01f, gridUnitSize);
            ClearCache();
        }
    }
}
