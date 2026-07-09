using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Gameplay;
using NumbersBlast.Opponent;

namespace NumbersBlast.Tests
{
    /// <summary>
    /// EditMode tests for the opponent's beat planner (decision layer, split out from the motion).
    /// Seedable RNG makes it deterministic; no assets or Editor APIs, runs headless.
    /// </summary>
    public sealed class OpponentActPlannerTests
    {
        private static PlacementService NewPlacement() => new PlacementService(new MergeResolver(), new LineClearResolver());

        private static PieceInstance OneCell(int value)
        {
            return new PieceInstance(new List<PieceCellData> { new PieceCellData(Vector2Int.zero, value) });
        }

        private static List<Vector2Int> Travels(List<OpponentBeat> beats)
        {
            var t = new List<Vector2Int>();
            foreach (OpponentBeat b in beats)
            {
                if (b.Type == OpponentBeatType.TravelTo)
                {
                    t.Add(b.Cell);
                }
            }

            return t;
        }

        [Test]
        public void Planner_Decoys_AreDistinctValidAndNeverTheAnchor()
        {
            var board = new BoardModel(8, 8);
            var placement = NewPlacement();
            var piece = OneCell(1);
            var move = new OpponentMove(piece, new Vector2Int(3, 3), 0f);
            var planner = new OpponentActPlanner(1, 4, 0.5f, 3f, 0.6f, 0f, 0f, new System.Random(123));

            List<OpponentBeat> beats = planner.Plan(board, placement, move);
            List<Vector2Int> travels = Travels(beats);

            // The last TravelTo is the real anchor; everything before it is a decoy.
            Assert.GreaterOrEqual(travels.Count, 2);
            Assert.AreEqual(move.Anchor, travels[travels.Count - 1]);

            var seen = new HashSet<Vector2Int>();
            for (int i = 0; i < travels.Count - 1; i++)
            {
                Vector2Int decoy = travels[i];
                Assert.AreNotEqual(move.Anchor, decoy, "decoy must never be the real anchor");
                Assert.IsTrue(placement.CanPlace(board, piece, decoy), "decoy must be a valid placement");
                Assert.IsTrue(seen.Add(decoy), "decoys must be distinct");
            }
        }

        [Test]
        public void Planner_Misdrop_TargetsAGenuinelyInvalidCell()
        {
            var board = new BoardModel(8, 8);
            board.SetValue(new Vector2Int(0, 0), 5);   // occupy a cell so a misdrop target exists
            var placement = NewPlacement();
            var piece = OneCell(1);
            var move = new OpponentMove(piece, new Vector2Int(3, 3), 0f);
            var planner = new OpponentActPlanner(1, 4, 0.5f, 3f, 0.6f, 1f, 0f, new System.Random(7));   // misdrop forced

            List<OpponentBeat> beats = planner.Plan(board, placement, move);

            OpponentBeat? misdrop = null;
            foreach (OpponentBeat b in beats)
            {
                if (b.Type == OpponentBeatType.Misdrop)
                {
                    misdrop = b;
                }
            }

            Assert.IsTrue(misdrop.HasValue, "misdrop chance 1.0 with an occupied cell should emit a misdrop beat");
            Assert.IsTrue(board.IsOccupied(misdrop.Value.Cell));
            Assert.IsFalse(placement.CanPlace(board, piece, misdrop.Value.Cell));
        }

        [Test]
        public void Planner_BeatCount_IsBoundedAndEndsWithFinalPlace()
        {
            var board = new BoardModel(8, 8);
            var placement = NewPlacement();
            var piece = OneCell(1);
            var move = new OpponentMove(piece, new Vector2Int(4, 4), 0f);
            var planner = new OpponentActPlanner(1, 4, 0.5f, 3f, 0.6f, 0f, 0f, new System.Random(99));

            List<OpponentBeat> beats = planner.Plan(board, placement, move);

            Assert.GreaterOrEqual(beats.Count, 5);
            Assert.LessOrEqual(beats.Count, 25);              // SelectDelay + ≤4 tries×4 + misdrop + final 3
            Assert.AreEqual(OpponentBeatType.SelectDelay, beats[0].Type);
            Assert.AreEqual(OpponentBeatType.FinalPlace, beats[beats.Count - 1].Type);
            Assert.AreEqual(move.Anchor, beats[beats.Count - 1].Cell);
        }
        [Test]
        public void Planner_SwapFakeout_GrabsAnotherPieceThenStillPlaysTheRealAnchor()
        {
            var board = new BoardModel(8, 8);
            var placement = NewPlacement();
            var piece = OneCell(1);
            var other = OneCell(2);
            var move = new OpponentMove(piece, new Vector2Int(3, 3), 0f);
            var planner = new OpponentActPlanner(1, 2, 0.5f, 1f, 0.5f, 0f, 1f, new System.Random(42));   // swap forced

            List<OpponentBeat> beats = planner.Plan(board, placement, move, 1f,
                new List<PieceInstance> { piece, other });

            Assert.AreEqual(OpponentBeatType.SwapFakeout, beats[0].Type,
                "swap chance 1.0 with a second tray piece must open with the fake pickup");
            Assert.AreSame(other, beats[0].Piece, "the fakeout must grab a DIFFERENT piece than the one played");
            Assert.IsTrue(placement.CanPlace(board, beats[0].Piece, beats[0].Cell),
                "the considered cell must be a valid placement for that piece");
            Assert.AreEqual(OpponentBeatType.FinalPlace, beats[beats.Count - 1].Type);
            Assert.AreEqual(move.Anchor, beats[beats.Count - 1].Cell);
        }
    }
}
