using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.App;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// Single source of truth for turning a piece's live on-screen position into a board anchor and
    /// the matching cell highlight. Both the player's drag and the AI opponent go through here, so the
    /// glow is always driven by the piece's own position — never a separate, mode-specific code path.
    /// </summary>
    public static class PiecePlacementPreview
    {
        /// <summary>
        /// Resolves the board anchor from the piece's own reference cell (its live world position),
        /// so the highlighted footprint always lands directly under the piece — whatever lift, grab
        /// offset or idle motion is in effect. Returns false when the piece is off the board.
        /// </summary>
        public static bool TryResolveAnchor(PieceView piece, BoardView board, Camera cam, out Vector2Int anchor)
        {
            anchor = Vector2Int.zero;
            if (piece == null || !piece.HasReferenceCell)
            {
                return false;
            }

            Vector2 refScreen = RectTransformUtility.WorldToScreenPoint(cam, piece.ReferenceCellWorldCenter);
            if (board.TryScreenPointToAnchor(refScreen, cam, out Vector2Int refCell))
            {
                anchor = refCell - piece.ReferenceOffset;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Draws the board preview for a resolved anchor: the valid merge/clear highlight when the
        /// move is legal, otherwise the invalid footprint. Callers guard on anchor-change so this only
        /// runs when the target cell actually moves.
        /// </summary>
        public static void Show(BoardView board, GameSessionController session, PieceInstance piece, Vector2Int anchor)
        {
            MoveResult preview = session.PreviewMove(piece, anchor);
            if (preview.IsValid)
            {
                board.ShowMovePreview(preview);
            }
            else
            {
                board.ShowInvalidPreview(Footprint(piece, anchor));
            }
        }

        /// <summary>
        /// One continuous preview step — the single mechanism the player's drag and the AI opponent
        /// both run every frame while a piece is held: resolve the anchor from the piece's live
        /// position and, only when it changes, redraw the highlight (valid or invalid); clear it when
        /// the piece is off the board. There is no player/AI-specific glow path — both call this.
        /// Returns whether the piece is currently over the board; the resolved anchor comes out via
        /// <paramref name="anchor"/> (for the caller's drop logic).
        /// </summary>
        public static bool Tick(PieceView piece, BoardView board, GameSessionController session, Camera cam,
            ref Vector2Int lastAnchor, ref bool shown, out Vector2Int anchor)
        {
            if (TryResolveAnchor(piece, board, cam, out anchor))
            {
                if (!shown || anchor != lastAnchor)
                {
                    Show(board, session, piece.Piece, anchor);
                    lastAnchor = anchor;
                    shown = true;
                }

                return true;
            }

            if (shown)
            {
                board.ClearPreview();
                shown = false;
            }

            return false;
        }

        /// <summary>World-grid cells a piece would occupy at an anchor.</summary>
        public static List<Vector2Int> Footprint(PieceInstance piece, Vector2Int anchor)
        {
            var cells = new List<Vector2Int>(piece.Cells.Count);
            foreach (PieceCellData cell in piece.Cells)
            {
                cells.Add(anchor + cell.Offset);
            }

            return cells;
        }
    }
}
