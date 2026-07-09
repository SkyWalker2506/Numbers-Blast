using UnityEngine;
using NumbersBlast.Core;

namespace NumbersBlast.App
{
    /// <summary>
    /// Owns the small amount of persisted player meta (best score + tutorial-seen flag), so the
    /// orchestrator never touches <see cref="PlayerPrefs"/> directly. The best-score guard is a pure,
    /// testable method. An optional key prefix lets tests run against isolated keys.
    /// </summary>
    public sealed class PlayerProgress
    {
        private readonly string _bestScoreKey;
        private readonly string _tutorialSeenKey;

        public int BestScore { get; private set; }
        public bool HasSeenTutorial { get; private set; }

        /// <param name="keyPrefix">Prepended to the real keys; tests pass e.g. "test_" and clean up after.</param>
        public PlayerProgress(string keyPrefix = "")
        {
            _bestScoreKey = keyPrefix + "nb_best_score";
            _tutorialSeenKey = keyPrefix + "nb_tutorial_seen";
        }

        public void Load()
        {
            BestScore = PlayerPrefs.GetInt(_bestScoreKey, 0);
            HasSeenTutorial = PlayerPrefs.GetInt(_tutorialSeenKey, 0) == 1;
        }

        public void MarkTutorialSeen()
        {
            HasSeenTutorial = true;
            PlayerPrefs.SetInt(_tutorialSeenKey, 1);
            PlayerPrefs.Save();
        }

        /// <summary>
        /// Best score is a SOLO-only stat and never counts the tutorial's scripted demo clear. Persists
        /// and returns true only when this is a real solo score strictly above the current best.
        /// </summary>
        public bool TrySubmitBestScore(int score, GameMode mode, bool tutorialActive)
        {
            if (mode != GameMode.Solo || tutorialActive || score <= BestScore)
            {
                return false;
            }

            BestScore = score;
            PlayerPrefs.SetInt(_bestScoreKey, score);
            PlayerPrefs.Save();
            return true;
        }
    }
}
