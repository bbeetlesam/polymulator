using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Ensures base physics collision matrix layers stay active at startup across Unity 6 minor versions
    /// (guards against Unity 6000.6 to 6000.3 DynamicsManager YAML deserialization resetting Layer 0/1 masks).
    /// </summary>
    public static class SimulationPhysicsBootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureCollisionMatrixIntegrity()
        {
            for (int a = 0; a < 32; a++)
            {
                if (string.IsNullOrEmpty(LayerMask.LayerToName(a))) continue;
                for (int b = a; b < 32; b++)
                {
                    if (string.IsNullOrEmpty(LayerMask.LayerToName(b))) continue;
                    if (Physics.GetIgnoreLayerCollision(a, b))
                        Physics.IgnoreLayerCollision(a, b, false);
                }
            }
        }
    }
}
