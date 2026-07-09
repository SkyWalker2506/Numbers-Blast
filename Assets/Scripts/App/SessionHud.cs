using TMPro;
using UnityEngine;
using NumbersBlast.Gameplay;
using NumbersBlast.UI;

namespace NumbersBlast.App
{
    /// <summary>
    /// Arranges the HUD for each mode — pure view toggling and score binding, no game rules.
    /// Solo shows the big central score + career best; Vs AI swaps those for the YOU/OPPONENT
    /// pills. Owned and driven by <see cref="GameSessionController"/>; plain class, so the scene
    /// contract (which serialized fields exist, and on what) is untouched.
    /// </summary>
    internal sealed class SessionHud
    {
        private readonly GameObject _gameplayRoot;
        private readonly MainMenuView _mainMenu;
        private readonly GameObject _bestScoreRoot;
        private readonly GameObject _versusHudRoot;
        private readonly ScoreView _localScoreView;
        private readonly ScoreView _versusLocalScoreView;
        private readonly ScoreView _opponentScoreView;
        private readonly TMP_Text _bestScoreLabel;

        public SessionHud(GameObject gameplayRoot, MainMenuView mainMenu, GameObject bestScoreRoot,
            GameObject versusHudRoot, ScoreView localScoreView, ScoreView versusLocalScoreView,
            ScoreView opponentScoreView, TMP_Text bestScoreLabel)
        {
            _gameplayRoot = gameplayRoot;
            _mainMenu = mainMenu;
            _bestScoreRoot = bestScoreRoot;
            _versusHudRoot = versusHudRoot;
            _localScoreView = localScoreView;
            _versusLocalScoreView = versusLocalScoreView;
            _opponentScoreView = opponentScoreView;
            _bestScoreLabel = bestScoreLabel;
        }

        public void ShowGameplay()
        {
            if (_gameplayRoot != null) _gameplayRoot.SetActive(true);
            if (_mainMenu != null) _mainMenu.Hide();
        }

        public void HideGameplay()
        {
            if (_gameplayRoot != null) _gameplayRoot.SetActive(false);
        }

        /// <summary>Solo HUD: central score + career best; no versus elements.</summary>
        public void EnterSolo(ScoreService localScore)
        {
            if (_versusHudRoot != null) _versusHudRoot.SetActive(false);
            if (_opponentScoreView != null) _opponentScoreView.gameObject.SetActive(false);
            // Best score only makes sense as a solo "beat your record" stat; a versus match already
            // has two live scores (you/opponent) and showing career-best too would just clutter it.
            if (_bestScoreRoot != null) _bestScoreRoot.SetActive(true);
            // The central ScoreView has no wired label (the big number has no caption), so it's
            // bound without a label string.
            if (_localScoreView != null)
            {
                _localScoreView.gameObject.SetActive(true);   // Vs AI hides it in favour of the YOU pill
                _localScoreView.Bind(localScore);
            }
        }

        /// <summary>Versus HUD: YOU/OPPONENT pills; the big central solo score is hidden.</summary>
        public void EnterVersus(ScoreService localScore, ScoreService opponentScore)
        {
            if (_versusHudRoot != null) _versusHudRoot.SetActive(true);
            if (_opponentScoreView != null) _opponentScoreView.gameObject.SetActive(true);
            if (_bestScoreRoot != null) _bestScoreRoot.SetActive(false);
            // The versus scoreboard is the two corner pills; the big central solo score would
            // duplicate the YOU pill, so it's hidden for the match.
            if (_localScoreView != null) _localScoreView.gameObject.SetActive(false);
            if (_versusLocalScoreView != null) _versusLocalScoreView.Bind(localScore, "YOU");
            if (_opponentScoreView != null) _opponentScoreView.Bind(opponentScore, "OPPONENT");
        }

        /// <summary>Undo the Vs AI central-score hide when the run tears down (return to menu).</summary>
        public void RestoreCentralScore()
        {
            if (_localScoreView != null) _localScoreView.gameObject.SetActive(true);
        }

        public void SetBestScore(int value)
        {
            if (_bestScoreLabel != null)
            {
                _bestScoreLabel.text = value.ToString();
            }
        }
    }
}
