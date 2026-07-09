using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// The single central move-resolution pipeline. ApplyMove is the one source of truth used by
    /// preview (on a scratch board), real moves (on the real board) and opponent evaluation.
    /// It mutates the board it receives when the move is valid — hence the honest name "ApplyMove".
    /// </summary>
    public sealed class PlacementService
    {
        private readonly MergeResolver _mergeResolver;
        private readonly LineClearResolver _lineClearResolver;

        public PlacementService(MergeResolver mergeResolver, LineClearResolver lineClearResolver)
        {
            _mergeResolver = mergeResolver;
            _lineClearResolver = lineClearResolver;
        }

        public bool CanPlace(BoardModel board, PieceInstance piece, Vector2Int anchor)
        {
            foreach (PieceCellData cell in piece.Cells)
            {
                Vector2Int position = anchor + cell.Offset;
                if (!board.IsInside(position) || board.IsOccupied(position))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// Places the piece, resolves all merges, then clears full lines — in that order.
        /// On an invalid move the board is left untouched and an invalid result is returned.
        /// </summary>
        public MoveResult ApplyMove(BoardModel board, PieceInstance piece, Vector2Int anchor)
        {
            if (!CanPlace(board, piece, anchor))
            {
                return MoveResult.Invalid();
            }

            var placedCells = new List<Vector2Int>(piece.Cells.Count);
            foreach (PieceCellData cell in piece.Cells)
            {
                Vector2Int position = anchor + cell.Offset;
                board.SetValue(position, cell.Value);
                placedCells.Add(position);
            }

            List<MergeStep> mergeSteps = _mergeResolver.Resolve(board, placedCells);
            LineClearResult lineClear = _lineClearResolver.Resolve(board);

            return new MoveResult(true, placedCells, mergeSteps, lineClear.ClearedCells, lineClear.ScoreGain);
        }

        /// <summary>
        /// True if any of the given pieces can be placed anywhere on the board. Used for fail state.
        /// </summary>
        public bool HasAnyValidMove(BoardModel board, IReadOnlyList<PieceInstance> pieces)
        {
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
                        if (CanPlace(board, piece, new Vector2Int(x, y)))
                        {
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}
