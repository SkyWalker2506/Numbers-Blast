using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NumbersBlast.Core;

namespace NumbersBlast.UI
{
    public sealed class TimerView : MonoBehaviour
    {
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Image fillBar;
        [SerializeField] private Color normalColor = new Color(0.4f, 0.8f, 1f);
        [Tooltip("Bar tint while the opponent's countdown is running, so the two turns read apart at a glance.")]
        [SerializeField] private Color opponentColor = new Color(1f, 0.66f, 0.32f);
        [SerializeField] private Color warningColor = new Color(1f, 0.4f, 0.35f);

        // Cache the last shown second so the text (and its string allocation) only updates
        // once per second instead of every frame the timer ticks.
        private int _lastShownSecond = -1;
        private bool _opponentOwned;

        /// <summary>
        /// Tints the countdown for whichever side's turn is running (the opponent's gets its own tint).
        /// </summary>
        public void SetOwner(PlayerSide side)
        {
            _opponentOwned = side == PlayerSide.Opponent;
        }

        public void SetVisible(bool visible)
        {
            gameObject.SetActive(visible);
        }

        public void SetTime(float remaining, float total)
        {
            int seconds = Mathf.CeilToInt(Mathf.Max(0f, remaining));
            if (seconds != _lastShownSecond)
            {
                _lastShownSecond = seconds;
                if (valueText != null)
                {
                    valueText.text = seconds.ToString();
                }
            }

            float ratio = total > 0f ? Mathf.Clamp01(remaining / total) : 0f;
            if (fillBar != null)
            {
                // Drive the bar by its RectTransform width (anchorMax.x), not Image.fillAmount:
                // the fill has no sprite, and fillAmount only renders when a sprite is present.
                RectTransform rt = fillBar.rectTransform;
                Vector2 max = rt.anchorMax;
                max.x = ratio;
                rt.anchorMax = max;
                fillBar.color = ratio < 0.3f ? warningColor : (_opponentOwned ? opponentColor : normalColor);
            }
        }
    }
}
