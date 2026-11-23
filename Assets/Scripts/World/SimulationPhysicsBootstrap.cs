using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Ensures base physics collision matrix layers stay active at startup across Unity 6 minor versions
    /// (guards against Unity 6000.6 to 6000.3 DynamicsManager YAML deserialization resetting Layer 0/1 masks).
    /// </summary>
    public static class SimulationPhysicsBootstrap
    {
        private static readonly int[] GameplayLayers = { 0, 2 };

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void EnsureCollisionMatrixIntegrity()
        {
            for (int i = 0; i < GameplayLayers.Length; i++)
            {
                int a = GameplayLayers[i];
                for (int j = i; j < GameplayLayers.Length; j++)
                {
                    int b = GameplayLayers[j];
                    if (Physics.GetIgnoreLayerCollision(a, b))
                        Physics.IgnoreLayerCollision(a, b, false);
                }
            }
        }
    }
}
