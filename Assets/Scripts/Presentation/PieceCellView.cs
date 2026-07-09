using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace NumbersBlast.Presentation
{
    /// <summary>
    /// One visual cell inside a tray/drag piece.
    /// </summary>
    public sealed class PieceCellView : MonoBehaviour
    {
        [SerializeField] private Image background;
        [SerializeField] private TMP_Text valueText;
        [SerializeField] private Sprite blockSprite;

        public RectTransform RectTransform => (RectTransform)transform;

        public void SetValue(int value, Color color)
        {
            if (background != null)
            {
                if (blockSprite != null)
                {
                    background.sprite = blockSprite;
                }

                background.color = color;
            }

            if (valueText != null)
            {
                valueText.text = value.ToString();
            }
        }
    }
}
