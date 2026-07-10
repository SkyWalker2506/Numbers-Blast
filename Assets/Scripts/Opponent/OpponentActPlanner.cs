using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Opponent
{
    /// <summary>
    /// Pure planner: turns a chosen <see cref="OpponentMove"/> into a bounded, human-reading list of
    /// beats — think → 1–4 decoy "tries" with varied dwell → occasional misdrop → hesitate → drop.
    /// A seedable RNG makes the beat logic unit-testable; the presentation only plays what this returns.
    /// Absorbs the old decoy sampling (partial Fisher–Yates) and invalid-cell search.
    /// </summary>
    public sealed class OpponentActPlanner
    {
        private readonly int _minAttempts;
        private readonly int _maxAttempts;
        private readonly float _preMoveDelayMin;
        private readonly float _preMoveDelayMax;
        private readonly float _hoverHesitation;
        private readonly float _misdropChance;
        private readonly float _swapFakeoutChance;
        private readonly System.Random _rng;
        private readonly List<Vector2Int> _cellScratch = new List<Vector2Int>(64);
        private BoardModel _simBoard;   // reused scratch for the honest-decoy simulation

        public OpponentActPlanner(int minAttempts, int maxAttempts, float preMoveDelayMin, float preMoveDelayMax,
            float hoverHesitation, float misdropChance, float swapFakeoutChance = 0f, System.Random rng = null)
        {
            _minAttempts = minAttempts;
            _maxAttempts = maxAttempts;
            _preMoveDelayMin = preMoveDelayMin;
            _preMoveDelayMax = preMoveDelayMax;
            _hoverHesitation = hoverHesitation;
            _misdropChance = misdropChance;
            _swapFakeoutChance = swapFakeoutChance;
            _rng = rng ?? new System.Random();
        }

        private float RangeF(float min, float max) => min + (float)_rng.NextDouble() * (max - min);
        private int RangeI(int minInclusive, int maxExclusive) => _rng.Next(minInclusive, maxExclusive);
        private float Value01 => (float)_rng.NextDouble();

        /// <param name="thinkScale">Multiplies the "thinking" beats (initial select delay + final hover):
        /// &gt;1 when the best move was a close call, &lt;1 when one move clearly won. Default 1 = unscaled.</param>
        /// <param name="trayPieces">The full shared tray; enables the occasional swap-fakeout (pick up a
        /// DIFFERENT piece, consider a cell, put it back — the brief's "cancel" beat). Null disables it.</param>
        public List<OpponentBeat> Plan(BoardModel board, PlacementService placement, OpponentMove move,
            float thinkScale = 1f, IReadOnlyList<PieceInstance> trayPieces = null)
        {
            var beats = new List<OpponentBeat>();

            // The chosen move's own outcome, for the honest-decoy rule below.
            if (_simBoard == null || _simBoard.Width != board.Width || _simBoard.Height != board.Height)
            {
                _simBoard = new BoardModel(board.Width, board.Height);
            }
            _simBoard.CopyFrom(board);
            MoveResult chosen = placement.ApplyMove(_simBoard, move.Piece, move.Anchor);

            // Decisiveness follows CONFIDENCE, not dice: thinkScale < 1 means the evaluator found a
            // clearly best move (big top-2 gap), ~1.15 means a genuine close call. "torn" turns that
            // into 0 (obvious — beeline to the cell) .. 1 (deliberates over candidates).
            float torn = Mathf.InverseLerp(0.7f, 1.15f, thinkScale);

            // Reach for a DIFFERENT piece first only when genuinely thinking — considering the wrong
            // piece while an obvious move is on the board would read as blind, not human. The
            // considered cell obeys the same honesty rule: never one that previews better than the
            // real move.
            if (trayPieces != null && torn > 0.5f && Value01 < _swapFakeoutChance)
            {
                PieceInstance other = PickOtherPiece(trayPieces, move.Piece);
                if (other != null)
                {
                    Vector2Int? cell = FindHonestCellFor(board, placement, other, chosen);
                    if (cell.HasValue)
                    {
                        beats.Add(OpponentBeat.SwapFakeout(other, cell.Value, _hoverHesitation * RangeF(0.9f, 1.4f)));
                    }
                }
            }

            beats.Add(OpponentBeat.SelectDelay(RangeF(_preMoveDelayMin, _preMoveDelayMax) * thinkScale));

            // Decoy count scales with how torn the decision was: an obvious move gets 0 tries (grab
            // it and go), a close call always deliberates at least once. Jitter inside that band
            // keeps it organic without ever contradicting the situation.
            int ceiling = Mathf.Min(_maxAttempts, Mathf.RoundToInt(torn * _maxAttempts));
            int floorAttempts = Mathf.Clamp(torn > 0.75f ? 1 : _minAttempts, 0, ceiling);
            int attempts = RangeI(floorAttempts, ceiling + 1);
            List<Vector2Int> decoys = CollectTryCells(board, placement, move, attempts, chosen);

            if (attempts == 0)
            {
                // A decisive turn, on purpose: no decoys — grab the piece and take it straight to
                // its cell. Humans don't deliberate every single move.
            }
            else if (decoys.Count == 0)
            {
                // Decoys were WANTED but none exist (board almost full) — drift to a nearby cell so
                // it still reads as "thinking" rather than snapping straight to the target.
                var scan = new Vector2Int(
                    Mathf.Clamp(move.Anchor.x + RangeI(-2, 3), 0, board.Width - 1),
                    Mathf.Clamp(move.Anchor.y + RangeI(-1, 3), 0, board.Height - 1));
                beats.Add(OpponentBeat.TravelTo(scan));
                beats.Add(OpponentBeat.Hover(_hoverHesitation * RangeF(0.4f, 0.8f)));
            }
            else
            {
                foreach (Vector2Int decoy in decoys)
                {
                    beats.Add(OpponentBeat.TravelTo(decoy));

                    // Each try's character: mostly a quick glance or normal look, occasionally a ponder.
                    float roll = Value01;
                    float lookScale = roll < 0.4f ? RangeF(0.3f, 0.6f)
                                    : roll < 0.85f ? RangeF(0.7f, 1.15f)
                                    : RangeF(1.2f, 1.6f);
                    beats.Add(OpponentBeat.Hover(_hoverHesitation * lookScale));
                    beats.Add(OpponentBeat.Hover(RangeF(0.06f, 0.22f)));   // short dead-time before moving on
                    if (Value01 < 0.3f)
                    {
                        beats.Add(OpponentBeat.Hover(RangeF(0.12f, 0.3f)));   // occasional longer linger
                    }
                }
            }

            // Rarely, one attempt is a genuine invalid drop that bounces back before settling.
            if (decoys.Count > 0 && Value01 < _misdropChance)
            {
                Vector2Int? wrong = FindInvalidCell(board, placement, move);
                if (wrong.HasValue)
                {
                    beats.Add(OpponentBeat.Misdrop(wrong.Value));
                }
            }

            beats.Add(OpponentBeat.TravelTo(move.Anchor));
            beats.Add(OpponentBeat.Hover(_hoverHesitation * RangeF(0.7f, 1.1f) * thinkScale));
            beats.Add(OpponentBeat.FinalPlace(move.Anchor));
            return beats;
        }

        /// <summary>
        /// Up to <paramref name="count"/> distinct valid decoy cells (never the real anchor), sampled at
        /// random. Honest-decoy rule: a decoy is never a cell whose live preview would look BETTER than
        /// the move actually coming (a line clear or a merge the chosen move doesn't have) — hovering a
        /// glowing clear and then placing elsewhere reads as stupid, not human.
        /// </summary>
        private List<Vector2Int> CollectTryCells(BoardModel board, PlacementService placement, OpponentMove move, int count, MoveResult chosen)
        {
            _cellScratch.Clear();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var a = new Vector2Int(x, y);
                    if (a == move.Anchor || !placement.CanPlace(board, move.Piece, a))
                    {
                        continue;
                    }

                    _simBoard.CopyFrom(board);
                    MoveResult sim = placement.ApplyMove(_simBoard, move.Piece, a);
                    if ((sim.HasLineClear && !chosen.HasLineClear) || (sim.HasMerge && !chosen.HasMerge))
                    {
                        continue;   // looks better than the real move — don't fake it
                    }

                    _cellScratch.Add(a);
                }
            }

            int take = Mathf.Min(count, _cellScratch.Count);
            for (int i = 0; i < take; i++)   // partial Fisher–Yates
            {
                int j = _rng.Next(i, _cellScratch.Count);
                (_cellScratch[i], _cellScratch[j]) = (_cellScratch[j], _cellScratch[i]);
            }

            var result = new List<Vector2Int>(take);
            for (int i = 0; i < take; i++)
            {
                result.Add(_cellScratch[i]);
            }

            return result;
        }

        /// <summary>A random tray piece other than the one actually being played; null if there is none.</summary>
        private PieceInstance PickOtherPiece(IReadOnlyList<PieceInstance> trayPieces, PieceInstance playing)
        {
            int count = 0;
            for (int i = 0; i < trayPieces.Count; i++)
            {
                if (trayPieces[i] != null && trayPieces[i] != playing) count++;
            }

            if (count == 0)
            {
                return null;
            }

            int pick = RangeI(0, count);
            for (int i = 0; i < trayPieces.Count; i++)
            {
                if (trayPieces[i] != null && trayPieces[i] != playing && pick-- == 0)
                {
                    return trayPieces[i];
                }
            }

            return null;
        }

        /// <summary>
        /// A random valid cell for <paramref name="piece"/> whose simulated preview would NOT look
        /// better than the move actually coming (no clear/merge the chosen move lacks) — so briefly
        /// considering it and putting the piece back reads as human, not as passing up a good move.
        /// </summary>
        private Vector2Int? FindHonestCellFor(BoardModel board, PlacementService placement, PieceInstance piece, MoveResult chosen)
        {
            _cellScratch.Clear();
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var a = new Vector2Int(x, y);
                    if (!placement.CanPlace(board, piece, a))
                    {
                        continue;
                    }

                    _simBoard.CopyFrom(board);
                    MoveResult sim = placement.ApplyMove(_simBoard, piece, a);
                    if ((sim.HasLineClear && !chosen.HasLineClear) || (sim.HasMerge && !chosen.HasMerge))
                    {
                        continue;
                    }

                    _cellScratch.Add(a);
                }
            }

            if (_cellScratch.Count == 0)
            {
                return null;
            }

            return _cellScratch[RangeI(0, _cellScratch.Count)];
        }

        /// <summary>An already-occupied cell where this piece cannot legally go — a believable failed target.</summary>
        public static Vector2Int? FindInvalidCell(BoardModel board, PlacementService placement, OpponentMove move)
        {
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    var a = new Vector2Int(x, y);
                    if (board.IsOccupied(a) && !placement.CanPlace(board, move.Piece, a))
                    {
                        return a;
                    }
                }
            }

            return null;
        }
    }
}
