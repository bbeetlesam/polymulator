using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Metadata attached to each preserved geometric sub-piece welded inside a compound
    /// Fused InteractableObject. Enables accurate 2D compound floor outlines in PlacementGhost
    /// and clean seam un-fusing when Partition [1] is applied to a fused assembly.
    /// </summary>
    public sealed class FusedSubPart : MonoBehaviour
    {
        public string partName = "PART";
        public float partMass = 1f;
        public Color partColor = Color.white;
        public FusionShapeKind shapeKind = FusionShapeKind.Box;
        public bool isCircularShape;
        public Vector3 partLocalSize = Vector3.one;
        public int weldOrder;
    }
}
