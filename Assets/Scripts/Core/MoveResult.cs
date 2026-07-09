using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumbersBlast.Core
{
    /// <summary>
    /// Result of a single move resolution pass (place + merge + line clear).
    /// Produced by the one central pipeline in PlacementService.ApplyMove,
    /// and shared by preview, real moves and opponent evaluation.
    /// </summary>
    public sealed class MoveResult
    {
        private static readonly IReadOnlyList<Vector2Int> EmptyCells = Array.Empty<Vector2Int>();
        private static readonly IReadOnlyList<MergeStep> EmptySteps = Array.Empty<MergeStep>();

        public bool IsValid { get; }
        public IReadOnlyList<Vector2Int> PlacedCells { get; }
        public IReadOnlyList<MergeStep> MergeSteps { get; }
        public IReadOnlyList<Vector2Int> ClearedCells { get; }
        public int ScoreGain { get; }

        public bool HasMerge => MergeSteps.Count > 0;
        public bool HasLineClear => ClearedCells.Count > 0;

        public MoveResult(
            bool isValid,
            IReadOnlyList<Vector2Int> placedCells,
            IReadOnlyList<MergeStep> mergeSteps,
            IReadOnlyList<Vector2Int> clearedCells,
            int scoreGain)
        {
            IsValid = isValid;
            PlacedCells = placedCells ?? EmptyCells;
            MergeSteps = mergeSteps ?? EmptySteps;
            ClearedCells = clearedCells ?? EmptyCells;
            ScoreGain = scoreGain;
        }

        public static MoveResult Invalid()
        {
            return new MoveResult(false, EmptyCells, EmptySteps, EmptyCells, 0);
        }
    }
}
