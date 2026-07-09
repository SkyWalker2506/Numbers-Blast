using UnityEngine;

namespace NumbersBlast.Core
{
    /// <summary>
    /// One cell of a piece: a local grid offset and its numeric value.
    /// Immutable so pieces cannot mutate after creation.
    /// </summary>
    public readonly struct PieceCellData
    {
        public Vector2Int Offset { get; }
        public int Value { get; }

        public PieceCellData(Vector2Int offset, int value)
        {
            Offset = offset;
            Value = value;
        }
    }
}
