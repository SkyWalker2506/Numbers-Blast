using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// Resolves same-value merges deterministically. Merge uses 4-direction adjacency only.
    /// A merged cell keeps the group's start position as its target and continues to chain
    /// until its value no longer has a same-value neighbor.
    /// </summary>
    public sealed class MergeResolver
    {
        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        public List<MergeStep> Resolve(BoardModel board, IReadOnlyList<Vector2Int> startPositions)
        {
            var steps = new List<MergeStep>();

            foreach (Vector2Int start in startPositions)
            {
                if (!board.IsOccupied(start))
                {
                    continue;
                }

                Vector2Int current = start;

                while (board.IsOccupied(current))
                {
                    int value = board.GetValue(current);
                    List<Vector2Int> group = FindConnectedSameValue(board, current, value);

                    if (group.Count < 2)
                    {
                        break;
                    }

                    int resultValue = 0;
                    foreach (Vector2Int pos in group)
                    {
                        resultValue += board.GetValue(pos);
                    }

                    foreach (Vector2Int pos in group)
                    {
                        if (pos != current)
                        {
                            board.Clear(pos);
                        }
                    }

                    board.SetValue(current, resultValue);
                    steps.Add(new MergeStep(current, value, resultValue, group));
                }
            }

            return steps;
        }

        private static List<Vector2Int> FindConnectedSameValue(BoardModel board, Vector2Int start, int value)
        {
            var group = new List<Vector2Int>();
            var visited = new HashSet<Vector2Int> { start };
            var queue = new Queue<Vector2Int>();
            queue.Enqueue(start);

            while (queue.Count > 0)
            {
                Vector2Int cell = queue.Dequeue();
                group.Add(cell);

                foreach (Vector2Int dir in Directions)
                {
                    Vector2Int neighbor = cell + dir;
                    if (visited.Contains(neighbor))
                    {
                        continue;
                    }

                    if (board.IsOccupied(neighbor) && board.GetValue(neighbor) == value)
                    {
                        visited.Add(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }

            return group;
        }
    }
}
