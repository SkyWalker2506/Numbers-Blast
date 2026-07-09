using System.Collections.Generic;
using UnityEngine;

namespace NumbersBlast.Data
{
    [CreateAssetMenu(menuName = "Numbers Blast/Piece Set", fileName = "PieceSetConfig")]
    public sealed class PieceSetConfig : ScriptableObject
    {
        [SerializeField] private PieceShapeDefinition[] shapes;

        [Tooltip("Case uses 1-4. If widening this, keep at least as many values as the largest shape's " +
            "cell count, or PieceFactory's no-adjacent-equal-value guarantee becomes impossible to " +
            "satisfy for that shape (it falls back to a best-effort greedy assignment instead).")]
        [Header("Spawn value range (inclusive)")]
        [SerializeField] private int minValue = 1;
        [SerializeField] private int maxValue = 4;

        public IReadOnlyList<PieceShapeDefinition> Shapes => shapes;

        /// <summary>Lowest value a spawned cell can take. Designers can widen the range without code changes.</summary>
        public int MinValue => minValue;

        /// <summary>Highest value a spawned cell can take.</summary>
        public int MaxValue => maxValue;

        private void OnValidate()
        {
            minValue = Mathf.Clamp(minValue, 1, 9);
            maxValue = Mathf.Clamp(maxValue, minValue, 9);
        }
    }
}
