using System.Collections.Generic;
using UnityEngine;

namespace NumbersBlast.Core
{
    public sealed class LineClearResult
    {
        public IReadOnlyList<Vector2Int> ClearedCells { get; }
        public int ScoreGain { get; }

        public LineClearResult(IReadOnlyList<Vector2Int> clearedCells, int scoreGain)
        {
            ClearedCells = clearedCells;
            ScoreGain = scoreGain;
        }
    }
}
