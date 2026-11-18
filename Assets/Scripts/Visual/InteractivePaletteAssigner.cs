using System.Collections.Generic;
using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Assigns stable palette colours to interactive objects. Scene setup uses a single
    /// shuffled pass so nearby objects get distinct colours; objects created later draw
    /// from a reshuffled runtime deck.
    /// </summary>
    public static class InteractivePaletteAssigner
    {
        private static readonly List<Color> runtimeDeck = new List<Color>();
        private static int runtimeDeckIndex;

        /// <summary>
        /// Colours every <see cref="InteractableObject"/> in the loaded scenes from one
        /// shuffled palette pass. Optional materials are indexed like the palette.
        /// </summary>
        public static void AssignAll(IList<Color> palette, IList<Material> materials = null)
        {
            if (palette == null || palette.Count == 0) return;

            InteractableObject[] objects =
                Object.FindObjectsByType<InteractableObject>(FindObjectsSortMode.None);
            if (objects.Length == 0) return;

            List<int> order = new List<int>(palette.Count);
            for (int i = 0; i < palette.Count; i++) order.Add(i);
            Shuffle(order);

            for (int i = 0; i < objects.Length; i++)
            {
                int paletteIndex = order[i % order.Count];
                Material material = materials != null && paletteIndex < materials.Count
                    ? materials[paletteIndex]
                    : null;
                objects[i].SetAssignedColor(palette[paletteIndex], material);
            }
        }

        /// <summary>Next colour for an object that was created outside scene setup.</summary>
        public static Color NextColor()
        {
            if (runtimeDeckIndex >= runtimeDeck.Count) RefillRuntimeDeck();
            return runtimeDeck[runtimeDeckIndex++];
        }

        private static void RefillRuntimeDeck()
        {
            runtimeDeck.Clear();
            runtimeDeck.AddRange(PaletteSettings.InteractivePalette);
            Shuffle(runtimeDeck);
            runtimeDeckIndex = 0;
        }

        private static void Shuffle<T>(IList<T> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = Random.Range(0, i + 1);
                T value = list[i];
                list[i] = list[j];
                list[j] = value;
            }
        }
    }
}
