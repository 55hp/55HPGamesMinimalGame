using System;
using System.Collections.Generic;
using UnityEngine;
using hp55games.Mobile.Core.Config;

namespace hp55games.Blockout.Config
{
    // Editor-authorable override list for which of the 12 canonical pieces BlockoutShapeSet
    // actually includes (README §0 rule 1/4 - the enabled flags live here, not hardcoded in
    // code). Any of the 12 not listed here defaults to enabled - see
    // BlockoutShapeSet.AllPieceNames for the exact names to use (Tetracube01..08,
    // Pentacube01..04, assigned by generation/selection order, not hand-picked letter shapes).
    [CreateAssetMenu(fileName = "BlockoutShapeSelection", menuName = "hp55games/Blockout/Shape Selection Config")]
    public sealed class BlockoutShapeSelectionConfig : ScriptableObject, IConfigAsset
    {
        [Serializable]
        public sealed class PieceToggle
        {
            [Tooltip("Must match one of BlockoutShapeSet.AllPieceNames exactly (e.g. Tetracube01, Pentacube03). A name that doesn't match any piece is ignored.")]
            public string pieceName;
            [Tooltip("Whether this piece is included in the game's piece pool.")]
            public bool enabled = true;
        }

        [Header("Piece Overrides")]
        [Tooltip("One entry per piece to exclude/re-include. A piece not listed here defaults to enabled.")]
        [SerializeField] private List<PieceToggle> _pieces = new();

        public IReadOnlyList<PieceToggle> Pieces => _pieces;
    }
}
