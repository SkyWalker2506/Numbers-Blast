using System;
using UnityEngine;

namespace NumbersBlast.Gameplay
{
    /// <summary>
    /// Holds a single score value. Part 1 uses one instance; Part 2 uses two (local + opponent).
    /// </summary>
    public sealed class ScoreService
    {
        private int _score;

        public int Score => _score;
        public event Action<int> ScoreChanged;

        public void Add(int amount)
        {
            if (amount <= 0)
            {
                return;
            }

            _score += amount;
            ScoreChanged?.Invoke(_score);
        }

        /// <summary>
        /// Applies a percentage penalty of the current score, floored, clamped to a minimum of 0.
        /// Example: 5% of 47 -> floor(2.35) = 2 removed.
        /// </summary>
        public void ApplyPenaltyPercent(float percent)
        {
            int penalty = Mathf.FloorToInt(_score * percent);
            _score = Mathf.Max(0, _score - penalty);
            ScoreChanged?.Invoke(_score);
        }

        public void Reset()
        {
            _score = 0;
            ScoreChanged?.Invoke(_score);
        }
    }
}
