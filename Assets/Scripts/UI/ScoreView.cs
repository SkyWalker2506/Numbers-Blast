using UnityEngine;
using TMPro;
using NumbersBlast.Gameplay;

namespace NumbersBlast.UI
{
    public sealed class ScoreView : MonoBehaviour
    {
        [SerializeField] private TMP_Text label;
        [SerializeField] private TMP_Text valueText;

        private ScoreService _score;

        public void Bind(ScoreService score, string labelText = null)
        {
            if (_score != null)
            {
                _score.ScoreChanged -= OnScoreChanged;
            }

            _score = score;
            _score.ScoreChanged += OnScoreChanged;

            if (label != null && !string.IsNullOrEmpty(labelText))
            {
                label.text = labelText;
            }

            OnScoreChanged(_score.Score);
        }

        private void OnScoreChanged(int value)
        {
            if (valueText != null)
            {
                valueText.text = value.ToString();
            }
        }

        private void OnDestroy()
        {
            if (_score != null)
            {
                _score.ScoreChanged -= OnScoreChanged;
            }
        }
    }
}
