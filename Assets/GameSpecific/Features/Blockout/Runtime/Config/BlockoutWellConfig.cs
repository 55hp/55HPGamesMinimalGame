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

        [Tooltip("Added once to the gameplay camera's authored scene position at Start (BlockoutWellCamera) - not touched otherwise. Find values by playing from 00_Bootstrap, pausing, and nudging the camera Transform by hand; the delta from its scene-authored position is what goes here.")]
        [SerializeField] private Vector3 _cameraPositionOffset = Vector3.zero;

        public int Width => _width;
        public int Height => _height;
        public int Depth => _depth;
        public Vector3 CameraPositionOffset => _cameraPositionOffset;
    }
}
