using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Data;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// Creates random pieces from a PieceSetConfig. Values are drawn from the config's
    /// [MinValue, MaxValue] range, and two cells that are adjacent inside the same piece can never
    /// receive the same value (no self-merge on spawn).
    /// </summary>
    public sealed class PieceFactory
    {
        private const int MaxAttempts = 32;

        private static readonly Vector2Int[] Directions =
        {
            Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right
        };

        private readonly PieceSetConfig _config;
        private readonly System.Random _random;
        private readonly int _minValue;
        private readonly int _maxValue;

        public PieceFactory(PieceSetConfig config, int? seed = null)
        {
            _config = config;
            _random = seed.HasValue ? new System.Random(seed.Value) : new System.Random();
            _minValue = config.MinValue;
            _maxValue = config.MaxValue;
        }

        public PieceInstance CreateRandomPiece()
        {
            IReadOnlyList<PieceShapeDefinition> shapes = _config.Shapes;
            PieceShapeDefinition shape = shapes[_random.Next(shapes.Count)];
            return CreatePiece(shape.Offsets);
        }

        private PieceInstance CreatePiece(IReadOnlyList<Vector2Int> offsets)
        {
            for (int attempt = 0; attempt < MaxAttempts; attempt++)
            {
                if (TryAssignRandom(offsets, out List<PieceCellData> cells))
                {
                    return new PieceInstance(cells);
                }
            }

            // With several values and small shapes this branch is effectively unreachable,
            // but a deterministic greedy pass guarantees a valid piece anyway.
            return new PieceInstance(AssignGreedy(offsets));
        }

        private bool TryAssignRandom(IReadOnlyList<Vector2Int> offsets, out List<PieceCellData> cells)
        {
            cells = new List<PieceCellData>(offsets.Count);
            var assigned = new Dictionary<Vector2Int, int>();

            foreach (Vector2Int offset in offsets)
            {
                var candidates = new List<int>(_maxValue);
                for (int value = _minValue; value <= _maxValue; value++)
                {
                    if (!IsForbidden(assigned, offset, value))
                    {
                        candidates.Add(value);
                    }
                }

                if (candidates.Count == 0)
                {
                    return false;
                }

                int chosen = candidates[_random.Next(candidates.Count)];
                assigned[offset] = chosen;
                cells.Add(new PieceCellData(offset, chosen));
            }

            return true;
        }

        private List<PieceCellData> AssignGreedy(IReadOnlyList<Vector2Int> offsets)
        {
            var assigned = new Dictionary<Vector2Int, int>();
            var cells = new List<PieceCellData>(offsets.Count);

            foreach (Vector2Int offset in offsets)
            {
                int chosen = _minValue;
                for (int value = _minValue; value <= _maxValue; value++)
                {
                    if (!IsForbidden(assigned, offset, value))
                    {
                        chosen = value;
                        break;
                    }
                }

                assigned[offset] = chosen;
                cells.Add(new PieceCellData(offset, chosen));
            }

            return cells;
        }

        private static bool IsForbidden(Dictionary<Vector2Int, int> assigned, Vector2Int offset, int value)
        {
            foreach (Vector2Int dir in Directions)
            {
                if (assigned.TryGetValue(offset + dir, out int neighborValue) && neighborValue == value)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
