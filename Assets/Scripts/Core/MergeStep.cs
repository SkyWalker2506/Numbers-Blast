using System.Collections.Generic;
using UnityEngine;

namespace NumbersBlast.Core
{
    /// <summary>
    /// A single merge event during move resolution.
    /// <see cref="SourceCells"/> contains the full connected same-value group,
    /// including the <see cref="Target"/> cell that receives the merged value.
    /// </summary>
    public sealed class MergeStep
    {
        public Vector2Int Target { get; }
        public int OriginalValue { get; }
        public int ResultValue { get; }
        public IReadOnlyList<Vector2Int> SourceCells { get; }

        public MergeStep(Vector2Int target, int originalValue, int resultValue, IReadOnlyList<Vector2Int> sourceCells)
        {
            Target = target;
            OriginalValue = originalValue;
            ResultValue = resultValue;
            SourceCells = sourceCells;
        }
    }
}
