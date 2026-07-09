using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Opponent
{
    /// <summary>
    /// Chooses a believable (not purely random, not perfect) opponent move. Every candidate is
    /// evaluated on a scratch board using the same PlacementService.ApplyMove pipeline as the player,
    /// then a weighted-random pick among the top candidates keeps it human-like.
    ///
    /// In-match rubber-band: the width of that top-N selection tracks the current score gap, so the
    /// opponent loosens up when it's ahead and plays its single best move when it's behind. Matches
    /// stay close from the first move, and it never plays a purely optimal or purely random move.
    /// </summary>
    public sealed class OpponentMoveEvaluator
    {
        // Heuristic weights — previously a ScriptableObject (OpponentProfile); inlined here as the single
        // source of truth so there's no extra asset/wiring to keep in sync.
        private const float ScoreGainWeight = 10f;
        private const float MergeStepWeight = 5f;
        private const float ClearedCellWeight = 3f;
        private const float MergedValueWeight = 1f;
        private const float NoEffectPenalty = 4f;
        private static readonly int[] TopWeights = { 3, 2, 1 };

        // If the best candidate beats the runner-up by at least this much (roughly: it clears a line
        // or lands a big merge and the alternative doesn't), the move is OBVIOUS — a human never
        // passes on it, so the rubber-band must not either. Prevents "hovered the line clear, then
        // placed somewhere silly".
        private const float ObviousMoveGap = 25f;

        private readonly System.Random _random;
        private readonly int _rubberBandThreshold;

        /// <summary>
        /// Heuristic-score gap between the best and second-best candidate of the last <see cref="ChooseMove"/>
        /// (large = one clearly best move; small = a close call). The presentation reads this to hesitate
        /// longer when the decision is tight and place quickly when there's an obvious move.
        /// </summary>
        public float LastTopGap { get; private set; }

        public OpponentMoveEvaluator(int rubberBandThreshold = 15, int? seed = null)
        {
            _rubberBandThreshold = Mathf.Max(0, rubberBandThreshold);
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
        }

        /// <param name="scoreDelta">opponentScore − localScore at the moment of choosing; drives the rubber-band.</param>
        public OpponentMove ChooseMove(BoardModel board, PlacementService placement,
            IReadOnlyList<PieceInstance> pieces, int scoreDelta)
        {
            var scratch = new BoardModel(board.Width, board.Height);
            var candidates = new List<OpponentMove>();

            foreach (PieceInstance piece in pieces)
            {
                if (piece == null)
                {
                    continue;
                }

                for (int y = 0; y < board.Height; y++)
                {
                    for (int x = 0; x < board.Width; x++)
                    {
                        var anchor = new Vector2Int(x, y);
                        if (!placement.CanPlace(board, piece, anchor))
                        {
                            continue;
                        }

                        scratch.CopyFrom(board);
                        MoveResult result = placement.ApplyMove(scratch, piece, anchor);
                        candidates.Add(new OpponentMove(piece, anchor, Heuristic(result)));
                    }
                }
            }

            if (candidates.Count == 0)
            {
                LastTopGap = 0f;
                return null;
            }

            candidates.Sort((a, b) => b.Score.CompareTo(a.Score));
            LastTopGap = candidates.Count >= 2 ? candidates[0].Score - candidates[1].Score : float.MaxValue;
            int width = LastTopGap >= ObviousMoveGap ? 1 : SelectionWidth(scoreDelta);
            return WeightedPickFromTop(candidates, width);
        }

        /// <summary>
        /// Rubber-band: how many of the top candidates are in play.
        ///   ahead by more than the threshold  → 3 (loosens: real chance of a sub-optimal, human move)
        ///   within ±threshold                  → 2 (close match)
        ///   behind by more than the threshold  → 1 (plays its single best move to catch up)
        /// </summary>
        private int SelectionWidth(int scoreDelta)
        {
            if (scoreDelta > _rubberBandThreshold)
            {
                return 3;
            }

            if (scoreDelta < -_rubberBandThreshold)
            {
                return 1;
            }

            return 2;
        }

        private OpponentMove WeightedPickFromTop(List<OpponentMove> sorted, int maxTop)
        {
            int top = Mathf.Min(maxTop, Mathf.Min(TopWeights.Length, sorted.Count));
            if (top <= 1)
            {
                return sorted[0];   // behind, or only one option → always the best move
            }

            int totalWeight = 0;
            for (int i = 0; i < top; i++)
            {
                totalWeight += TopWeights[i];
            }

            int roll = _random.Next(totalWeight);
            int acc = 0;
            for (int i = 0; i < top; i++)
            {
                acc += TopWeights[i];
                if (roll < acc)
                {
                    return sorted[i];
                }
            }

            return sorted[0];
        }

        private float Heuristic(MoveResult result)
        {
            float mergedValue = 0f;
            foreach (MergeStep step in result.MergeSteps)
            {
                mergedValue += step.ResultValue;
            }

            float score = result.ScoreGain * ScoreGainWeight
                + result.MergeSteps.Count * MergeStepWeight
                + result.ClearedCells.Count * ClearedCellWeight
                + mergedValue * MergedValueWeight;

            if (!result.HasMerge && !result.HasLineClear)
            {
                score -= NoEffectPenalty;
            }

            return score;
        }
    }
}
