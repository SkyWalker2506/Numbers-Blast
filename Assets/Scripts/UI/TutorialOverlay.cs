using System.Collections;
using UnityEngine;
using TMPro;

namespace NumbersBlast.UI
{
    /// <summary>
    /// Shows the current tutorial instruction and a transient board message
    /// (e.g. the "No valid moves left" fail banner).
    /// </summary>
    public sealed class TutorialOverlay : MonoBehaviour
    {
        [SerializeField] private GameObject instructionRoot;
        [SerializeField] private TMP_Text instructionText;
        [SerializeField] private GameObject bannerRoot;
        [SerializeField] private TMP_Text bannerText;

        public void ShowInstruction(string text)
        {
            if (instructionRoot != null)
            {
                instructionRoot.SetActive(true);
            }

            if (instructionText != null)
            {
                instructionText.text = text;
            }
        }

        public void HideInstruction()
        {
            if (instructionRoot != null)
            {
                instructionRoot.SetActive(false);
            }
        }

        public IEnumerator ShowBanner(string text, float duration)
        {
            if (bannerRoot != null)
            {
                bannerRoot.SetActive(true);
            }

            if (bannerText != null)
            {
                bannerText.text = text;
            }

            yield return new WaitForSeconds(duration);

            if (bannerRoot != null)
            {
                bannerRoot.SetActive(false);
            }
        }
    }
}
