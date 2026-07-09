using System.Collections.Generic;
using NumbersBlast.Core;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// A concrete piece: shape offsets with their assigned values. Immutable.
    /// </summary>
    public sealed class PieceInstance
    {
        public IReadOnlyList<PieceCellData> Cells { get; }

        public PieceInstance(IReadOnlyList<PieceCellData> cells)
        {
            Cells = cells;
        }
    }
}
