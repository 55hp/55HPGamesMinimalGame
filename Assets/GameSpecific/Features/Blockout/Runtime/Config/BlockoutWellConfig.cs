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

        public int Width => _width;
        public int Height => _height;
        public int Depth => _depth;
    }
}
