using UnityEngine;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Opponent
{
    /// <summary>A candidate/chosen opponent move: which piece goes to which anchor, and its heuristic score.</summary>
    public sealed class OpponentMove
    {
        public PieceInstance Piece { get; }
        public Vector2Int Anchor { get; }
        public float Score { get; }

        public OpponentMove(PieceInstance piece, Vector2Int anchor, float score)
        {
            Piece = piece;
            Anchor = anchor;
            Score = score;
        }
    }
}
