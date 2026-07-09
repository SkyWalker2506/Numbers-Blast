using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// Clears full rows/columns after merges are resolved. Intersecting cells (row + column)
    /// are counted once via a HashSet, so their value is only scored a single time.
    /// </summary>
    public sealed class LineClearResolver
    {
        public LineClearResult Resolve(BoardModel board)
        {
            var cellsToClear = new HashSet<Vector2Int>();

            for (int y = 0; y < board.Height; y++)
            {
                if (IsRowFull(board, y))
                {
                    for (int x = 0; x < board.Width; x++)
                    {
                        cellsToClear.Add(new Vector2Int(x, y));
                    }
                }
            }

            for (int x = 0; x < board.Width; x++)
            {
                if (IsColumnFull(board, x))
                {
                    for (int y = 0; y < board.Height; y++)
                    {
                        cellsToClear.Add(new Vector2Int(x, y));
                    }
                }
            }

            int scoreGain = 0;
            foreach (Vector2Int pos in cellsToClear)
            {
                scoreGain += board.GetValue(pos);
            }

            var cleared = new List<Vector2Int>(cellsToClear);
            foreach (Vector2Int pos in cleared)
            {
                board.Clear(pos);
            }

            return new LineClearResult(cleared, scoreGain);
        }

        private static bool IsRowFull(BoardModel board, int y)
        {
            for (int x = 0; x < board.Width; x++)
            {
                if (!board.IsOccupied(new Vector2Int(x, y)))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsColumnFull(BoardModel board, int x)
        {
            for (int y = 0; y < board.Height; y++)
            {
                if (!board.IsOccupied(new Vector2Int(x, y)))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
