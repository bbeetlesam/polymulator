using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Playable area limits matching the invisible World Boundary colliders created
    /// by PrototypeSceneBuilder. Used for carried props, whose own colliders are
    /// disabled while Rect holds them.
    /// </summary>
    public static class WorldBounds
    {
        // Boundary collider centres sit at +/-14.7 with 0.6 thickness, so the
        // inner faces are at +/-14.4 grid units.
        public const float InnerHalfExtentGrid = 14.4f;

        /// <summary>
        /// Keeps a shape with the given horizontal half-extents inside the platform.
        /// Margins are world-space distances from the shape's centre.
        /// </summary>
        public static Vector3 ClampInside(Vector3 worldPosition, float halfExtentX, float halfExtentZ)
        {
            float limitX = Mathf.Max(0f, GridSettings.ToWorld(InnerHalfExtentGrid) - Mathf.Max(0f, halfExtentX));
            float limitZ = Mathf.Max(0f, GridSettings.ToWorld(InnerHalfExtentGrid) - Mathf.Max(0f, halfExtentZ));

            worldPosition.x = Mathf.Clamp(worldPosition.x, -limitX, limitX);
            worldPosition.z = Mathf.Clamp(worldPosition.z, -limitZ, limitZ);
            return worldPosition;
        }
    }
}
