using System;
using UnityEngine;
using UnityEngine.UI;

namespace NumbersBlast.Settings
{
    /// <summary>
    /// Settings panel, three layouts depending on where it's opened from:
    /// - In-game (Solo/VsAI): Restart + Main Menu + Close.
    /// - In-game during the tutorial: Restart doesn't make sense mid-script, so just Main Menu + Close.
    /// - From the main menu: neither applies, so just the toggles + Close in a shorter card.
    /// Opening it in Vs AI pauses the turn timer (handled by the listener on Opened/Closed).
    /// </summary>
    public sealed class SettingsPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private RectTransform card;
        [SerializeField] private Toggle sfxToggle;
        [SerializeField] private Toggle bgmToggle;
        [SerializeField] private Toggle vibrationToggle;
        [SerializeField] private Button restartButton;
        [SerializeField] private Button mainMenuButton;
        [SerializeField] private Button closeButton;

        [Header("Layout (full / no-restart / menu-compact)")]
        [SerializeField] private float fullHeight = 1000f;
        [SerializeField] private float noRestartHeight = 860f;
        [SerializeField] private float compactHeight = 720f;
        [SerializeField] private float mainMenuFullY = -700f;
        [SerializeField] private float mainMenuNoRestartY = -560f;
        [SerializeField] private float closeFullY = -840f;
        [SerializeField] private float closeNoRestartY = -700f;
        [SerializeField] private float closeCompactY = -560f;

        private GameSettings _settings;

        public event Action Opened;
        public event Action Closed;
        public event Action Restart;
        public event Action MainMenu;

        private void Awake()
        {
            if (restartButton != null)
            {
                restartButton.onClick.AddListener(() =>
                {
                    Restart?.Invoke();
                    Hide();
                });
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(() =>
                {
                    // Close the panel without firing Closed (the session tears the run down itself).
                    if (root != null)
                    {
                        root.SetActive(false);
                    }

                    MainMenu?.Invoke();
                });
            }

            if (closeButton != null)
            {
                closeButton.onClick.AddListener(Hide);
            }

            // The panel starts inactive in the scene and is shown via code; do not self-hide here.
        }

        public void Bind(GameSettings settings)
        {
            _settings = settings;

            if (sfxToggle != null)
            {
                sfxToggle.isOn = settings.SfxEnabled;
                sfxToggle.onValueChanged.AddListener(settings.SetSfxEnabled);
            }

            if (bgmToggle != null)
            {
                bgmToggle.isOn = settings.BgmEnabled;
                bgmToggle.onValueChanged.AddListener(settings.SetBgmEnabled);
            }

            if (vibrationToggle != null)
            {
                vibrationToggle.isOn = settings.VibrationEnabled;
                vibrationToggle.onValueChanged.AddListener(settings.SetVibrationEnabled);
            }
        }

        /// <summary>
        /// In-game settings. <paramref name="allowRestart"/> is false during the tutorial, where
        /// restarting mid-script doesn't make sense — only Main Menu + Close show then.
        /// </summary>
        public void Show(bool allowRestart = true)
        {
            Open(true, allowRestart);
        }

        /// <summary>Main-menu settings: Restart / Main Menu hidden (they don't apply here), shorter card.</summary>
        public void ShowFromMenu()
        {
            Open(false, false);
        }

        private void Open(bool inGame, bool allowRestart)
        {
            bool showRestart = inGame && allowRestart;
            bool showMainMenu = inGame;

            if (restartButton != null) restartButton.gameObject.SetActive(showRestart);
            if (mainMenuButton != null) mainMenuButton.gameObject.SetActive(showMainMenu);

            if (card != null)
            {
                Vector2 size = card.sizeDelta;
                size.y = !inGame ? compactHeight : (allowRestart ? fullHeight : noRestartHeight);
                card.sizeDelta = size;
            }

            if (mainMenuButton != null && showMainMenu)
            {
                var rt = (RectTransform)mainMenuButton.transform;
                Vector2 pos = rt.anchoredPosition;
                pos.y = allowRestart ? mainMenuFullY : mainMenuNoRestartY;
                rt.anchoredPosition = pos;
            }

            if (closeButton != null)
            {
                var rt = (RectTransform)closeButton.transform;
                Vector2 pos = rt.anchoredPosition;
                pos.y = !inGame ? closeCompactY : (allowRestart ? closeFullY : closeNoRestartY);
                rt.anchoredPosition = pos;
            }

            if (root != null) root.SetActive(true);
            Opened?.Invoke();
        }

        public void Hide()
        {
            if (root != null)
            {
                root.SetActive(false);
            }

            Closed?.Invoke();
        }
    }
}
