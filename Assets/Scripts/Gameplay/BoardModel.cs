using UnityEngine;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// Pure board state. Stores occupancy/value in a flat int[] where 0 means empty.
    /// Knows nothing about Unity views/UI and is not a MonoBehaviour.
    /// </summary>
    public sealed class BoardModel
    {
        private readonly int[] _cells;

        public int Width { get; }
        public int Height { get; }

        public BoardModel(int width, int height)
        {
            Width = width;
            Height = height;
            _cells = new int[width * height];
        }

        public bool IsInside(Vector2Int position)
        {
            return position.x >= 0 && position.x < Width
                && position.y >= 0 && position.y < Height;
        }

        public bool IsOccupied(Vector2Int position)
        {
            return IsInside(position) && _cells[ToIndex(position)] != 0;
        }

        public int GetValue(Vector2Int position)
        {
            return IsInside(position) ? _cells[ToIndex(position)] : 0;
        }

        public void SetValue(Vector2Int position, int value)
        {
            if (IsInside(position))
            {
                _cells[ToIndex(position)] = value;
            }
        }

        public void Clear(Vector2Int position)
        {
            if (IsInside(position))
            {
                _cells[ToIndex(position)] = 0;
            }
        }

        public void ClearAll()
        {
            for (int i = 0; i < _cells.Length; i++)
            {
                _cells[i] = 0;
            }
        }

        /// <summary>
        /// Copies state from another board of identical size. Used by preview and opponent
        /// evaluation so the real board is never mutated during simulation.
        /// </summary>
        public void CopyFrom(BoardModel source)
        {
            if (source == null || source.Width != Width || source.Height != Height)
            {
                return;
            }

            System.Array.Copy(source._cells, _cells, _cells.Length);
        }

        private int ToIndex(Vector2Int position)
        {
            return position.y * Width + position.x;
        }
    }
}
