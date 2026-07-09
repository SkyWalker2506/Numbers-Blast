using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Data;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// Visual representation of a PieceInstance in the tray / during drag. Lays out one
    /// PieceCellView per cell using the same cell size as the board so drag alignment matches.
    /// </summary>
    public sealed class PieceView : MonoBehaviour
    {
        [SerializeField] private RectTransform cellContainer;
        [SerializeField] private PieceCellView cellPrefab;
        [SerializeField] private CanvasGroup canvasGroup;

        public PieceInstance Piece { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        // A reference cell (the piece's first cell) lets the drag controller map the piece's actual
        // on-screen footprint to a board anchor, so the highlight always sits exactly under the piece.
        private RectTransform _referenceCell;
        private Vector2Int _referenceOffset;

        public bool HasReferenceCell => _referenceCell != null;
        public Vector2Int ReferenceOffset => _referenceOffset;
        public Vector3 ReferenceCellWorldCenter => _referenceCell.position;

        public void Build(PieceInstance piece, BoardConfig config)
        {
            Piece = piece;

            for (int i = cellContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cellContainer.GetChild(i).gameObject);
            }

            float cellSize = config.CellSize;

            // Center the piece's bounding box on the slot so multi-cell pieces sit centered.
            int minX = int.MaxValue, minY = int.MaxValue, maxX = int.MinValue, maxY = int.MinValue;
            foreach (PieceCellData cell in piece.Cells)
            {
                minX = Mathf.Min(minX, cell.Offset.x);
                maxX = Mathf.Max(maxX, cell.Offset.x);
                minY = Mathf.Min(minY, cell.Offset.y);
                maxY = Mathf.Max(maxY, cell.Offset.y);
            }
            float centerX = (minX + maxX + 1) * 0.5f * cellSize;
            float centerY = (minY + maxY + 1) * 0.5f * cellSize;
            cellContainer.anchoredPosition = new Vector2(-centerX, -centerY);

            _referenceCell = null;
            foreach (PieceCellData cell in piece.Cells)
            {
                PieceCellView view = Instantiate(cellPrefab, cellContainer);
                RectTransform rt = view.RectTransform;
                rt.anchorMin = Vector2.zero;
                rt.anchorMax = Vector2.zero;
                rt.pivot = new Vector2(0.5f, 0.5f);
                rt.sizeDelta = new Vector2(cellSize, cellSize);
                rt.anchoredPosition = new Vector2((cell.Offset.x + 0.5f) * cellSize, (cell.Offset.y + 0.5f) * cellSize);
                view.SetValue(cell.Value, config.GetColorForValue(cell.Value));

                if (_referenceCell == null)
                {
                    _referenceCell = rt;
                    _referenceOffset = cell.Offset;
                }
            }
        }

        public void SetScale(float scale)
        {
            transform.localScale = Vector3.one * scale;
        }

        public void SetRaycastTarget(bool enabled)
        {
            if (canvasGroup != null)
            {
                canvasGroup.blocksRaycasts = enabled;
            }
        }
    }
}
