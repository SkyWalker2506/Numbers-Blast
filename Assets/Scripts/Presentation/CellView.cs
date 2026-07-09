using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NumbersBlast.Core;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// A single board cell. Holds visual state only — no gameplay logic. Small self-contained
    /// tweens (pop, fade) are exposed for the animation controller to sequence.
    /// </summary>
    public sealed class CellView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private Image highlightOverlay;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private CanvasGroup canvasGroup;

        [Header("Sprites (optional; tinted by value color)")]
        [SerializeField] private Sprite filledSprite;
        [SerializeField] private Sprite emptySprite;

        [Header("Highlight Colors")]
        [SerializeField] private Color validColor = new Color(0.5f, 1f, 0.6f, 0.78f);
        [SerializeField] private Color invalidColor = new Color(1f, 0.35f, 0.35f, 0.7f);
        [SerializeField] private Color mergeColor = new Color(1f, 0.85f, 0.3f, 0.72f);
        [SerializeField] private Color lineClearColor = new Color(0.45f, 0.8f, 1f, 0.85f);

        private Color _emptyColor;
        private Coroutine _popRoutine;

        public RectTransform RectTransform => (RectTransform)transform;

        /// <summary>The filled-block sprite, so the merge animation's flying ghost blocks match the cells.</summary>
        public Sprite FilledSprite => filledSprite;

        public void Configure(Color emptyColor)
        {
            _emptyColor = emptyColor;
            SetEmpty();
            ClearHighlight();
            ResetVisual();
        }

        public void SetEmpty()
        {
            if (background != null)
            {
                if (emptySprite != null)
                {
                    background.sprite = emptySprite;
                    background.color = Color.white;
                }
                else
                {
                    background.color = _emptyColor;
                }
            }

            if (valueText != null)
            {
                valueText.text = string.Empty;
            }
        }

        public void SetValue(int value, Color color)
        {
            if (value <= 0)
            {
                SetEmpty();
                return;
            }

            if (background != null)
            {
                if (filledSprite != null)
                {
                    background.sprite = filledSprite;
                }

                background.color = color;
            }

            if (valueText != null)
            {
                valueText.text = value.ToString();
            }
        }

        public void SetHighlight(CellHighlightType type)
        {
            if (highlightOverlay == null)
            {
                return;
            }

            switch (type)
            {
                case CellHighlightType.None:
                    highlightOverlay.enabled = false;
                    break;
                case CellHighlightType.ValidPlacement:
                    Apply(validColor);
                    break;
                case CellHighlightType.InvalidPlacement:
                    Apply(invalidColor);
                    break;
                case CellHighlightType.MergeSource:
                    Apply(mergeColor);
                    break;
                case CellHighlightType.LineClear:
                    Apply(lineClearColor);
                    break;
            }
        }

        public void ClearHighlight()
        {
            if (highlightOverlay != null)
            {
                highlightOverlay.enabled = false;
            }
        }

        public void ResetVisual()
        {
            transform.localScale = Vector3.one;
            if (canvasGroup != null)
            {
                canvasGroup.alpha = 1f;
            }
        }

        public void PlayPop()
        {
            Punch(1.18f, 0.16f);
        }

        /// <summary>Stronger punch for merge targets — reads as a satisfying "merge landed".</summary>
        public void PlayPunch()
        {
            Punch(1.34f, 0.22f);
        }

        private void Punch(float peak, float duration)
        {
            if (!gameObject.activeInHierarchy)
            {
                return;
            }

            if (_popRoutine != null)
            {
                StopCoroutine(_popRoutine);
            }

            _popRoutine = StartCoroutine(PopRoutine(peak, duration));
        }

        public IEnumerator FadeOut(float duration)
        {
            if (canvasGroup == null)
            {
                yield break;
            }

            float start = canvasGroup.alpha;
            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / duration);
                canvasGroup.alpha = Mathf.Lerp(start, 0f, k);
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * 0.6f, k);
                yield return null;
            }

            canvasGroup.alpha = 0f;
        }

        private void Apply(Color color)
        {
            highlightOverlay.enabled = true;
            highlightOverlay.color = color;
        }

        private IEnumerator PopRoutine(float peakScale, float duration)
        {
            float half = duration * 0.5f;
            float t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                transform.localScale = Vector3.Lerp(Vector3.one, Vector3.one * peakScale, k);
                yield return null;
            }

            t = 0f;
            while (t < half)
            {
                t += Time.deltaTime;
                float k = Mathf.Clamp01(t / half);
                transform.localScale = Vector3.Lerp(Vector3.one * peakScale, Vector3.one, k);
                yield return null;
            }

            transform.localScale = Vector3.one;
            _popRoutine = null;
        }
    }
}
