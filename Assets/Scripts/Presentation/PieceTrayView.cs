using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Data;
using NumbersBlast.App;
using NumbersBlast.Gameplay;
using NumbersBlast.Input;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// Holds the current tray pieces and their drag controllers. In Part 2 the tray is shared,
    /// so both player and opponent select from the same live pieces.
    /// </summary>
    public sealed class PieceTrayView : MonoBehaviour
    {
        /// <summary>Pieces are shown smaller in the tray and grow to board size on pickup.</summary>
        public const float TrayScale = 0.72f;

        [SerializeField] private RectTransform[] slots;
        [SerializeField] private PieceView piecePrefab;

        private readonly List<PieceView> _active = new List<PieceView>();
        private GameSessionController _session;
        private BoardView _boardView;
        private BoardConfig _config;

        public void Initialize(GameSessionController session, BoardView boardView, BoardConfig config)
        {
            _session = session;
            _boardView = boardView;
            _config = config;
        }

        /// <summary>Live view count — the session asserts this stays in sync with its authoritative TrayModel.</summary>
        public int ActiveCount => _active.Count;

        public void ShowPieces(IReadOnlyList<PieceInstance> pieces)
        {
            Clear();

            int count = Mathf.Min(pieces.Count, slots.Length);
            // Centre the pieces among the slots, so a single tutorial piece sits in the middle
            // rather than off to the left. A full 3-piece tray starts at slot 0 as before.
            int start = (slots.Length - count) / 2;
            for (int i = 0; i < count; i++)
            {
                RectTransform slot = slots[start + i];
                PieceView view = Instantiate(piecePrefab, slot);
                view.RectTransform.anchorMin = new Vector2(0.5f, 0.5f);
                view.RectTransform.anchorMax = new Vector2(0.5f, 0.5f);
                view.RectTransform.pivot = new Vector2(0.5f, 0.5f);
                view.RectTransform.anchoredPosition = Vector2.zero;
                view.Build(pieces[i], _config);
                view.SetScale(TrayScale);

                var drag = view.GetComponent<PieceDragController>();
                if (drag != null)
                {
                    drag.Initialize(_session, _boardView, view, slot);
                }

                _active.Add(view);
            }
        }

        /// <summary>Removes a placed piece's view from the tray.</summary>
        public void ConsumePiece(PieceView view)
        {
            if (view == null)
            {
                return;
            }

            _active.Remove(view);
            Destroy(view.gameObject);
            // Refill is driven by GameSessionController after the move resolves (once the tray is empty).
        }

        public PieceView FindViewFor(PieceInstance piece)
        {
            foreach (PieceView view in _active)
            {
                if (view.Piece == piece)
                {
                    return view;
                }
            }

            return null;
        }

        private void Clear()
        {
            foreach (PieceView view in _active)
            {
                if (view != null)
                {
                    Destroy(view.gameObject);
                }
            }

            _active.Clear();
        }
    }
}
