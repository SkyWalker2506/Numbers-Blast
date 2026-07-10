using System;

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

        public void Reset()
        {
            _score = 0;
            ScoreChanged?.Invoke(_score);
        }
    }
}
