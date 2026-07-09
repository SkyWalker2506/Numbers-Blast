using System.Collections.Generic;
using UnityEngine;

namespace NumbersBlast.Data
{
    /// <summary>
    /// Shape of a piece as a set of local grid offsets. Values are not stored here;
    /// they are assigned at spawn time by PieceFactory.
    /// </summary>
    [CreateAssetMenu(menuName = "Numbers Blast/Piece Shape", fileName = "PieceShape")]
    public sealed class PieceShapeDefinition : ScriptableObject
    {
        [SerializeField] private Vector2Int[] offsets;

        public IReadOnlyList<Vector2Int> Offsets => offsets;
    }
}
