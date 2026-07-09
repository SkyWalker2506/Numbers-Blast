using UnityEngine;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Opponent
{
    public enum OpponentBeatType
    {
        SelectDelay,   // think, then pick the piece up
        TravelTo,      // move to Cell (the shared preview tracks the piece)
        Hover,         // hold near the current cell for Seconds
        Misdrop,       // go for the invalid Cell, show the red glow, then bounce back to the tray
        SwapFakeout,   // pick up a DIFFERENT tray piece, consider Cell for Seconds, put it back
        FinalPlace     // drop on the real anchor and commit
    }

    /// <summary>
    /// One planned beat of the opponent's turn — pure data produced by <see cref="OpponentActPlanner"/>
    /// and played by <c>OpponentPresentationController</c>'s motion primitives. Splitting the decision
    /// (which cells, how long, whether to misdrop/swap) from the motion makes the beat logic unit-testable.
    /// </summary>
    public readonly struct OpponentBeat
    {
        public readonly OpponentBeatType Type;
        public readonly Vector2Int Cell;
        public readonly float Seconds;
        /// <summary>Only for <see cref="OpponentBeatType.SwapFakeout"/>: the OTHER tray piece it briefly considers.</summary>
        public readonly PieceInstance Piece;

        private OpponentBeat(OpponentBeatType type, Vector2Int cell, float seconds, PieceInstance piece = null)
        {
            Type = type;
            Cell = cell;
            Seconds = seconds;
            Piece = piece;
        }

        public static OpponentBeat SelectDelay(float seconds) => new OpponentBeat(OpponentBeatType.SelectDelay, default, seconds);
        public static OpponentBeat TravelTo(Vector2Int cell) => new OpponentBeat(OpponentBeatType.TravelTo, cell, 0f);
        public static OpponentBeat Hover(float seconds) => new OpponentBeat(OpponentBeatType.Hover, default, seconds);
        public static OpponentBeat Misdrop(Vector2Int cell) => new OpponentBeat(OpponentBeatType.Misdrop, cell, 0f);
        public static OpponentBeat SwapFakeout(PieceInstance piece, Vector2Int cell, float hoverSeconds) => new OpponentBeat(OpponentBeatType.SwapFakeout, cell, hoverSeconds, piece);
        public static OpponentBeat FinalPlace(Vector2Int anchor) => new OpponentBeat(OpponentBeatType.FinalPlace, anchor, 0f);
    }
}
