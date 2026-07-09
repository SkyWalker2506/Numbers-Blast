using System;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NumbersBlast.Core;

namespace NumbersBlast.UI
{
    /// <summary>
    /// End-of-run panel. Solo shows final score; Vs AI shows both scores and the winner.
    /// </summary>
    public sealed class GameOverPanel : MonoBehaviour
    {
        [SerializeField] private GameObject root;
        [SerializeField] private TMP_Text titleText;
        [SerializeField] private TMP_Text soloScoreText;
        [SerializeField] private GameObject versusGroup;
        [SerializeField] private TMP_Text playerScoreText;
        [SerializeField] private TMP_Text opponentScoreText;
        [SerializeField] private TMP_Text resultText;
        [SerializeField] private Button replayButton;
        [SerializeField] private Button mainMenuButton;

        private Action _onReplay;
        private Action _onMainMenu;

        private void Awake()
        {
            // Button wiring runs on first activation. The panel starts inactive in the scene
            // (clean editor) and is shown via code, so Awake must not hide it again here.
            if (replayButton != null)
            {
                replayButton.onClick.AddListener(() => _onReplay?.Invoke());
            }

            if (mainMenuButton != null)
            {
                mainMenuButton.onClick.AddListener(() => _onMainMenu?.Invoke());
            }
        }

        public void Configure(Action onReplay, Action onMainMenu)
        {
            _onReplay = onReplay;
            _onMainMenu = onMainMenu;
        }

        public void ShowSolo(int finalScore)
        {
            if (versusGroup != null)
            {
                versusGroup.SetActive(false);
            }

            if (soloScoreText != null)
            {
                soloScoreText.gameObject.SetActive(true);
                soloScoreText.text = "Score: " + finalScore;
            }

            if (titleText != null)
            {
                titleText.text = "Game Over";
            }

            Show();
        }

        public void ShowVersus(int playerScore, int opponentScore)
        {
            if (soloScoreText != null)
            {
                soloScoreText.gameObject.SetActive(false);
            }

            if (versusGroup != null)
            {
                versusGroup.SetActive(true);
            }

            if (playerScoreText != null)
            {
                playerScoreText.text = "You: " + playerScore;
            }

            if (opponentScoreText != null)
            {
                opponentScoreText.text = "Opponent: " + opponentScore;
            }

            string result;
            if (playerScore > opponentScore)
            {
                result = "You Win!";
            }
            else if (playerScore < opponentScore)
            {
                result = "You Lose";
            }
            else
            {
                result = "Draw";
            }

            if (resultText != null)
            {
                resultText.text = result;
            }

            if (titleText != null)
            {
                titleText.text = "Game Over";
            }

            Show();
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
