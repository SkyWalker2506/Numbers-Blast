using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Data;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Tests
{
    /// <summary>
    /// EditMode tests for the deterministic move-resolution pipeline, matching the case's
    /// acceptance checklist plus the headline architectural guarantees (preview parity,
    /// no board mutation on invalid moves). Self-contained: no assets or Editor APIs required,
    /// so it runs headless / in CI.
    /// </summary>
    public sealed class MoveResolutionTests
    {
        private static PlacementService NewPlacement()
        {
            return new PlacementService(new MergeResolver(), new LineClearResolver());
        }

        private static PieceInstance SingleCell(int value)
        {
            return new PieceInstance(new List<PieceCellData> { new PieceCellData(Vector2Int.zero, value) });
        }

        private static void SetPrivate(object target, string field, object value)
        {
            target.GetType().GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(target, value);
        }

        // Builds a PieceSetConfig entirely in-code (a 2x2 square shape, values 1..4) so the
        // factory test needs no asset on disk.
        private static PieceSetConfig BuildConfig()
        {
            var shape = ScriptableObject.CreateInstance<PieceShapeDefinition>();
            SetPrivate(shape, "offsets", new[]
            {
                new Vector2Int(0, 0), new Vector2Int(1, 0),
                new Vector2Int(0, 1), new Vector2Int(1, 1)
            });

            var config = ScriptableObject.CreateInstance<PieceSetConfig>();
            SetPrivate(config, "shapes", new[] { shape });
            SetPrivate(config, "minValue", 1);
            SetPrivate(config, "maxValue", 4);
            return config;
        }

        [Test]
        public void CanPlace_RejectsOutsideAndOccupied()
        {
            var board = new BoardModel(8, 8);
            var placement = NewPlacement();
            var piece = SingleCell(1);

            Assert.IsTrue(placement.CanPlace(board, piece, new Vector2Int(0, 0)));
            Assert.IsFalse(placement.CanPlace(board, piece, new Vector2Int(-1, 0)));
            Assert.IsFalse(placement.CanPlace(board, piece, new Vector2Int(8, 0)));

            board.SetValue(new Vector2Int(3, 3), 2);
            Assert.IsFalse(placement.CanPlace(board, piece, new Vector2Int(3, 3)));
        }

        [Test]
        public void Merge_ThreeOnes_BecomesThree()
        {
            var board = new BoardModel(8, 8);
            board.SetValue(new Vector2Int(3, 5), 1);
            board.SetValue(new Vector2Int(3, 3), 1);

            var result = NewPlacement().ApplyMove(board, SingleCell(1), new Vector2Int(3, 4));

            Assert.IsTrue(result.IsValid);
            Assert.IsTrue(result.HasMerge);
            Assert.AreEqual(3, board.GetValue(new Vector2Int(3, 4)));
        }

        [Test]
        public void Merge_ChainReaction_OnesBecomeThreeThenSix()
        {
            var board = new BoardModel(8, 8);
            board.SetValue(new Vector2Int(3, 5), 1); // north
            board.SetValue(new Vector2Int(3, 3), 1); // south
            board.SetValue(new Vector2Int(4, 4), 3); // east: chains after the 1s become 3

            NewPlacement().ApplyMove(board, SingleCell(1), new Vector2Int(3, 4));

            Assert.AreEqual(6, board.GetValue(new Vector2Int(3, 4)));
            Assert.IsFalse(board.IsOccupied(new Vector2Int(4, 4)));
        }

        [Test]
        public void LineClear_Row_ScoreEqualsSumOfClearedValues()
        {
            var board = new BoardModel(8, 8);
            int expected = 0;
            for (int x = 0; x < 7; x++)
            {
                int value = (x % 2 == 0) ? 2 : 3;
                board.SetValue(new Vector2Int(x, 0), value);
                expected += value;
            }

            var result = NewPlacement().ApplyMove(board, SingleCell(4), new Vector2Int(7, 0));
            expected += 4;

            Assert.IsTrue(result.HasLineClear);
            Assert.AreEqual(expected, result.ScoreGain);
            Assert.IsFalse(board.IsOccupied(new Vector2Int(0, 0)));
        }

        [Test]
        public void LineClear_Column_Clears()
        {
            var board = new BoardModel(8, 8);
            int expected = 0;
            for (int y = 0; y < 7; y++)
            {
                int value = (y % 2 == 0) ? 2 : 3;
                board.SetValue(new Vector2Int(0, y), value);
                expected += value;
            }

            var result = NewPlacement().ApplyMove(board, SingleCell(4), new Vector2Int(0, 7));
            expected += 4;

            Assert.IsTrue(result.HasLineClear);
            Assert.AreEqual(expected, result.ScoreGain);
            Assert.IsFalse(board.IsOccupied(new Vector2Int(0, 0)));
        }

        [Test]
        public void LineClear_RowColumnIntersection_CountedOnce()
        {
            var board = new BoardModel(4, 4);
            board.SetValue(new Vector2Int(0, 3), 2);
            board.SetValue(new Vector2Int(1, 3), 3);
            board.SetValue(new Vector2Int(2, 3), 2);
            board.SetValue(new Vector2Int(3, 0), 3);
            board.SetValue(new Vector2Int(3, 1), 2);
            board.SetValue(new Vector2Int(3, 2), 3);

            var result = NewPlacement().ApplyMove(board, SingleCell(4), new Vector2Int(3, 3));

            int expected = (2 + 3 + 2) + (3 + 2 + 3) + 4; // intersection (3,3) counted once
            Assert.IsTrue(result.HasLineClear);
            Assert.AreEqual(expected, result.ScoreGain);
        }

        [Test]
        public void Preview_OnScratchBoard_DoesNotMutateRealBoard()
        {
            var real = new BoardModel(8, 8);
            real.SetValue(new Vector2Int(3, 5), 1);
            real.SetValue(new Vector2Int(3, 3), 1);

            var scratch = new BoardModel(8, 8);
            scratch.CopyFrom(real);

            var result = NewPlacement().ApplyMove(scratch, SingleCell(1), new Vector2Int(3, 4));

            // Scratch reflects the merge; the real board is untouched — this is the WYSIWYG guarantee.
            Assert.AreEqual(3, scratch.GetValue(new Vector2Int(3, 4)));
            Assert.IsFalse(real.IsOccupied(new Vector2Int(3, 4)));
            Assert.AreEqual(1, real.GetValue(new Vector2Int(3, 5)));
            Assert.AreEqual(1, real.GetValue(new Vector2Int(3, 3)));
            Assert.IsTrue(result.IsValid);
        }

        [Test]
        public void InvalidMove_LeavesBoardUntouched()
        {
            var board = new BoardModel(8, 8);
            board.SetValue(new Vector2Int(0, 0), 2);

            // Overlaps an occupied cell -> invalid, board must not change at all.
            var result = NewPlacement().ApplyMove(board, SingleCell(3), new Vector2Int(0, 0));

            Assert.IsFalse(result.IsValid);
            Assert.AreEqual(0, result.ScoreGain);
            Assert.AreEqual(2, board.GetValue(new Vector2Int(0, 0)));
        }

        [Test]
        public void ScoreService_PenaltyFloorsAndClampsToZero()
        {
            var score = new ScoreService();
            score.Add(47);
            score.ApplyPenaltyPercent(0.05f); // floor(47 * 0.05) = 2
            Assert.AreEqual(45, score.Score);

            var small = new ScoreService();
            small.Add(3);
            small.ApplyPenaltyPercent(0.05f); // floor(0.15) = 0
            Assert.AreEqual(3, small.Score);
        }

        [Test]
        public void HasAnyValidMove_FullBoard_ReturnsFalse()
        {
            var board = new BoardModel(3, 3);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    board.SetValue(new Vector2Int(x, y), 1);
                }
            }

            var pieces = new List<PieceInstance> { SingleCell(2), SingleCell(3) };
            Assert.IsFalse(NewPlacement().HasAnyValidMove(board, pieces));
        }

        [Test]
        public void HasAnyValidMove_OneFreeCell_ReturnsTrue()
        {
            var board = new BoardModel(3, 3);
            for (int y = 0; y < 3; y++)
            {
                for (int x = 0; x < 3; x++)
                {
                    board.SetValue(new Vector2Int(x, y), 1);
                }
            }

            board.SetValue(new Vector2Int(2, 2), 0); // one hole

            var pieces = new List<PieceInstance> { SingleCell(2) };
            Assert.IsTrue(NewPlacement().HasAnyValidMove(board, pieces));
        }

        [Test]
        public void HasAnyValidMove_MultiCellPiece_NeedsContiguousSpace()
        {
            // A 1x2 horizontal piece can't fit if only isolated single holes remain.
            var board = new BoardModel(3, 1);
            board.SetValue(new Vector2Int(1, 0), 1); // middle blocked -> holes at x=0 and x=2, not adjacent

            var horizontal = new PieceInstance(new List<PieceCellData>
            {
                new PieceCellData(new Vector2Int(0, 0), 2),
                new PieceCellData(new Vector2Int(1, 0), 3)
            });

            Assert.IsFalse(NewPlacement().HasAnyValidMove(board, new List<PieceInstance> { horizontal }));

            board.SetValue(new Vector2Int(1, 0), 0); // clear middle -> now the pair fits at x=0..1
            Assert.IsTrue(NewPlacement().HasAnyValidMove(board, new List<PieceInstance> { horizontal }));
        }

        [Test]
        public void HasAnyValidMove_NullPiecesInTray_AreSkipped()
        {
            var board = new BoardModel(2, 2);
            var pieces = new List<PieceInstance> { null, null };
            Assert.IsFalse(NewPlacement().HasAnyValidMove(board, pieces));

            pieces.Add(SingleCell(1));
            Assert.IsTrue(NewPlacement().HasAnyValidMove(board, pieces));
        }

        [Test]
        public void PieceFactory_NeverSpawnsAdjacentEqualValues()
        {
            var config = BuildConfig();
            var factory = new PieceFactory(config, 987654321);
            var directions = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };

            for (int iteration = 0; iteration < 300; iteration++)
            {
                PieceInstance piece = factory.CreateRandomPiece();
                var map = new Dictionary<Vector2Int, int>();
                foreach (PieceCellData cell in piece.Cells)
                {
                    map[cell.Offset] = cell.Value;
                }

                foreach (PieceCellData cell in piece.Cells)
                {
                    foreach (Vector2Int dir in directions)
                    {
                        if (map.TryGetValue(cell.Offset + dir, out int neighbourValue))
                        {
                            Assert.AreNotEqual(cell.Value, neighbourValue,
                                "Adjacent cells inside a spawned piece must not share a value");
                        }
                    }
                }
            }
        }
    }
}
