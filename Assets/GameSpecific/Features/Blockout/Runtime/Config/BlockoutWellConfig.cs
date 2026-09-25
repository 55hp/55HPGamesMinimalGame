using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Config;

namespace hp55games.Blockout.Config
{
    [CreateAssetMenu(fileName = "BlockoutWell", menuName = "hp55games/Blockout/Well Config")]
    public sealed class BlockoutWellConfig : ScriptableObject, IConfigAsset
    {
        [Header("Well Dimensions")]
        [Tooltip("Width of the well grid, in cells.")]
        [SerializeField] private int _width = 5;
        [Tooltip("Height of the well grid, in cells.")]
        [SerializeField] private int _height = 10;
        [Tooltip("Depth of the well grid, in cells.")]
        [SerializeField] private int _depth = 5;

        [Header("Level Colors")]
        [Tooltip("One colour per well level (index 0 = bottom), shared by every skin: locked cubes take their level's colour, and the wireframe draws each level in it. Should have one entry per level (Height).")]
        [SerializeField] private Color[] _levelColors;

        public int Width => _width;
        public int Height => _height;
        public int Depth => _depth;
        public IReadOnlyList<Color> LevelColors => _levelColors;

        // Colour of the given level, clamped into the array: cells can briefly sit above the top
        // level (before the game-over check), and those get the top level's colour rather than
        // an exception. White if no colours are authored.
        public Color GetLevelColor(int level)
        {
            if (_levelColors == null || _levelColors.Length == 0) return Color.white;
            return _levelColors[Mathf.Clamp(level, 0, _levelColors.Length - 1)];
        }
    }
}
