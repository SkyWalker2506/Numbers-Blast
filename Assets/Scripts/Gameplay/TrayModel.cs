using System.Collections.Generic;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// Authoritative list of the pieces currently in the tray. The session owns this; the tray view
    /// only renders it. Keeps "which pieces are live" out of the view (the one MVC seam that was
    /// leaking) so <c>HasAnyValidMove</c> and opponent evaluation read the model instead of rebuilding
    /// a list from views every call.
    /// </summary>
    public sealed class TrayModel
    {
        private readonly List<PieceInstance> _pieces = new List<PieceInstance>();

        public IReadOnlyList<PieceInstance> Pieces => _pieces;
        public bool IsEmpty => _pieces.Count == 0;

        public void SetPieces(IReadOnlyList<PieceInstance> pieces)
        {
            _pieces.Clear();
            for (int i = 0; i < pieces.Count; i++)
            {
                _pieces.Add(pieces[i]);
            }
        }

        public void Remove(PieceInstance piece)
        {
            _pieces.Remove(piece);
        }

        public void Clear()
        {
            _pieces.Clear();
        }
    }
}
