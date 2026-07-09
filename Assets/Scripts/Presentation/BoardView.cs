using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Data;
using NumbersBlast.Gameplay;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// Builds and renders the UGUI board grid, maps screen points to grid anchors, and shows
    /// drag preview highlights. Coordinate math lives here (no separate input-mapper class).
    /// </summary>
    public sealed class BoardView : MonoBehaviour
    {
        [SerializeField] private RectTransform cellContainer;
        [SerializeField] private CellView cellPrefab;

        private BoardConfig _config;
        private CellView[] _cells;
        private int _width;
        private int _height;
        private float _cellSize;
        private readonly List<Vector2Int> _highlighted = new List<Vector2Int>();
        private readonly List<Vector2Int> _tutorialCells = new List<Vector2Int>();

        public void Build(BoardConfig config)
        {
            _config = config;
            _width = config.Width;
            _height = config.Height;
            _cellSize = config.CellSize;

            for (int i = cellContainer.childCount - 1; i >= 0; i--)
            {
                Destroy(cellContainer.GetChild(i).gameObject);
            }

            cellContainer.sizeDelta = new Vector2(_width * _cellSize, _height * _cellSize);
            _cells = new CellView[_width * _height];

            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    CellView cell = Instantiate(cellPrefab, cellContainer);
                    RectTransform rt = cell.RectTransform;
                    rt.anchorMin = Vector2.zero;
                    rt.anchorMax = Vector2.zero;
                    rt.pivot = new Vector2(0.5f, 0.5f);
                    rt.sizeDelta = new Vector2(_cellSize, _cellSize);
                    rt.anchoredPosition = new Vector2((x + 0.5f) * _cellSize, (y + 0.5f) * _cellSize);
                    cell.Configure(_config.EmptyCellColor);
                    _cells[y * _width + x] = cell;
                }
            }
        }

        public void Refresh(BoardModel board)
        {
            for (int y = 0; y < _height; y++)
            {
                for (int x = 0; x < _width; x++)
                {
                    var pos = new Vector2Int(x, y);
                    int value = board.GetValue(pos);
                    CellView cell = GetCell(pos);
                    // Reset alpha/scale in case the game-over fade wave left cells invisible before a replay.
                    cell.ResetVisual();
                    if (value <= 0)
                    {
                        cell.SetEmpty();
                    }
                    else
                    {
                        cell.SetValue(value, _config.GetColorForValue(value));
                    }
                }
            }
        }

        public CellView GetCell(Vector2Int pos)
        {
            if (pos.x < 0 || pos.x >= _width || pos.y < 0 || pos.y >= _height)
            {
                return null;
            }

            return _cells[pos.y * _width + pos.x];
        }

        public Vector3 GetCellWorldCenter(Vector2Int pos)
        {
            CellView cell = GetCell(pos);
            return cell != null ? cell.RectTransform.position : cellContainer.position;
        }

        /// <summary>
        /// Maps a screen point to a grid anchor. Returns false only when the point is far outside
        /// the board; validity of the resulting placement is decided by PlacementService.CanPlace.
        /// </summary>
        public bool TryScreenPointToAnchor(Vector2 screenPosition, Camera eventCamera, out Vector2Int anchor)
        {
            anchor = Vector2Int.zero;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(cellContainer, screenPosition, eventCamera, out Vector2 local))
            {
                return false;
            }

            // ScreenPointToLocalPointInRectangle returns the point relative to the container's pivot.
            // Cells are laid out from the container's bottom-left corner, so shift by rect.xMin/yMin
            // to make the anchor math pivot-agnostic.
            Rect rect = cellContainer.rect;
            float relX = local.x - rect.xMin;
            float relY = local.y - rect.yMin;

            int x = Mathf.FloorToInt(relX / _cellSize);
            int y = Mathf.FloorToInt(relY / _cellSize);
            anchor = new Vector2Int(x, y);

            const float margin = 1f;
            bool nearBoard = relX >= -_cellSize * margin && relX <= (_width + margin) * _cellSize
                && relY >= -_cellSize * margin && relY <= (_height + margin) * _cellSize;
            return nearBoard;
        }

        public void ShowMovePreview(MoveResult result)
        {
            ClearPreview();
            if (result == null || !result.IsValid)
            {
                return;
            }

            foreach (Vector2Int pos in result.PlacedCells)
            {
                Highlight(pos, CellHighlightType.ValidPlacement);
            }

            foreach (MergeStep step in result.MergeSteps)
            {
                foreach (Vector2Int pos in step.SourceCells)
                {
                    Highlight(pos, CellHighlightType.MergeSource);
                }
            }

            foreach (Vector2Int pos in result.ClearedCells)
            {
                Highlight(pos, CellHighlightType.LineClear);
            }
        }

        public void ShowInvalidPreview(IReadOnlyList<Vector2Int> attemptedCells)
        {
            ClearPreview();
            foreach (Vector2Int pos in attemptedCells)
            {
                Highlight(pos, CellHighlightType.InvalidPlacement);
            }
        }

        public void ClearPreview()
        {
            foreach (Vector2Int pos in _highlighted)
            {
                CellView cell = GetCell(pos);
                if (cell != null)
                {
                    cell.ClearHighlight();
                }
            }

            _highlighted.Clear();

            // Keep the tutorial's target cells lit after a drag preview is cleared.
            ApplyTutorialHighlight();
        }

        /// <summary>Persistently highlights the tutorial's target cell(s) so the player sees where to place.</summary>
        public void SetTutorialHighlight(IReadOnlyList<Vector2Int> cells)
        {
            ClearTutorialHighlight();
            if (cells != null)
            {
                foreach (Vector2Int pos in cells)
                {
                    _tutorialCells.Add(pos);
                }
            }

            ApplyTutorialHighlight();
        }

        public void ClearTutorialHighlight()
        {
            foreach (Vector2Int pos in _tutorialCells)
            {
                CellView cell = GetCell(pos);
                if (cell != null)
                {
                    cell.ClearHighlight();
                }
            }

            _tutorialCells.Clear();
        }

        private void ApplyTutorialHighlight()
        {
            foreach (Vector2Int pos in _tutorialCells)
            {
                CellView cell = GetCell(pos);
                if (cell != null)
                {
                    cell.SetHighlight(CellHighlightType.ValidPlacement);
                }
            }
        }

        private void Highlight(Vector2Int pos, CellHighlightType type)
        {
            CellView cell = GetCell(pos);
            if (cell == null)
            {
                return;
            }

            cell.SetHighlight(type);
            _highlighted.Add(pos);
        }
    }
}
