using UnityEngine;

namespace Polymulator
{
    /// <summary>
    /// Per-level metadata. Stored as a ScriptableObject in Assets/Resources/Levels so
    /// the LevelMenu can read a level's title without loading the level scene. The
    /// asset name must match the level scene name: Level{chapter}.{level}.
    /// </summary>
    [CreateAssetMenu(fileName = "LevelInfo", menuName = "POLYMULATOR/Level Info")]
    public sealed class LevelInfo : ScriptableObject
    {
        [SerializeField] private string levelTitle = string.Empty;

        public string Title => levelTitle;
    }
}
