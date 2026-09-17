using UnityEngine;
using hp55games.Mobile.Core.Config;

namespace hp55games.Blockout.Config
{
    [CreateAssetMenu(fileName = "BlockoutWell", menuName = "hp55games/Blockout/Well Config")]
    public sealed class BlockoutWellConfig : ScriptableObject, IConfigAsset
    {
        [SerializeField] private int _width = 5;
        [SerializeField] private int _height = 10;
        [SerializeField] private int _depth = 5;

        [Tooltip("Fraction of screen width left empty on each side of the well (e.g. 0.1 = 10% left + 10% right), constant across aspect ratios. Drives BlockoutWellCamera's horizontal FOV fit - a world-space padding can't give a constant on-screen percentage on its own, since the world-units-to-screen-fraction relationship depends on the FOV that padding itself feeds into.")]
        [SerializeField, Range(0f, 0.45f)] private float _horizontalPaddingScreenFraction = 0.1f;

        public int Width => _width;
        public int Height => _height;
        public int Depth => _depth;
        public float HorizontalPaddingScreenFraction => _horizontalPaddingScreenFraction;
    }
}
