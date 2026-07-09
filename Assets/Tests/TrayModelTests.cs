using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Tests
{
    /// <summary>
    /// EditMode tests for the authoritative tray model (the piece list the session owns, not the view).
    /// Self-contained: no assets or Editor APIs, runs headless.
    /// </summary>
    public sealed class TrayModelTests
    {
        private static PieceInstance Piece(int value)
        {
            return new PieceInstance(new List<PieceCellData> { new PieceCellData(Vector2Int.zero, value) });
        }

        private static bool ContainsRef(IReadOnlyList<PieceInstance> pieces, PieceInstance target)
        {
            for (int i = 0; i < pieces.Count; i++)
            {
                if (ReferenceEquals(pieces[i], target))
                {
                    return true;
                }
            }

            return false;
        }

        [Test]
        public void TrayModel_SetPieces_ReplacesAll()
        {
            var model = new TrayModel();
            model.SetPieces(new List<PieceInstance> { Piece(1), Piece(2) });
            Assert.AreEqual(2, model.Pieces.Count);

            var next = new List<PieceInstance> { Piece(3) };
            model.SetPieces(next);
            Assert.AreEqual(1, model.Pieces.Count);
            Assert.AreSame(next[0], model.Pieces[0]);
        }

        [Test]
        public void TrayModel_Remove_RemovesOnlySelectedPiece()
        {
            var a = Piece(1);
            var b = Piece(2);
            var c = Piece(3);
            var model = new TrayModel();
            model.SetPieces(new List<PieceInstance> { a, b, c });

            model.Remove(b);

            Assert.AreEqual(2, model.Pieces.Count);
            Assert.IsFalse(ContainsRef(model.Pieces, b));
            Assert.IsTrue(ContainsRef(model.Pieces, a));
            Assert.IsTrue(ContainsRef(model.Pieces, c));
        }

        [Test]
        public void TrayModel_IsEmpty_TracksCount()
        {
            var model = new TrayModel();
            Assert.IsTrue(model.IsEmpty);

            model.SetPieces(new List<PieceInstance> { Piece(1) });
            Assert.IsFalse(model.IsEmpty);

            model.Remove(model.Pieces[0]);
            Assert.IsTrue(model.IsEmpty);
        }
    }
}
