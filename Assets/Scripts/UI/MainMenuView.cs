using System;
using UnityEngine;
using UnityEngine.UI;

namespace NumbersBlast.UI
{
    /// <summary>
    /// Minimal main menu: Play (solo), Play vs AI, and a settings gear.
    /// No store / more-games / ads — intentionally kept to case scope.
    /// </summary>
    public sealed class MainMenuView : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private Button playButton;
        [SerializeField] private Button playVsAiButton;
        [SerializeField] private Button tutorialButton;
        [SerializeField] private Button settingsButton;

        public event Action PlaySolo;
        public event Action PlayVsAi;
        public event Action PlayTutorial;
        public event Action OpenSettings;

        private void Awake()
        {
            if (playButton != null)
            {
                playButton.onClick.AddListener(() => PlaySolo?.Invoke());
            }

            if (playVsAiButton != null)
            {
                playVsAiButton.onClick.AddListener(() => PlayVsAi?.Invoke());
            }

            if (tutorialButton != null)
            {
                tutorialButton.onClick.AddListener(() => PlayTutorial?.Invoke());
            }

            if (settingsButton != null)
            {
                settingsButton.onClick.AddListener(() => OpenSettings?.Invoke());
            }
        }

        public void Show()
        {
            if (root != null)
            {
                root.SetActive(true);
            }
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }
        }
    }
}
