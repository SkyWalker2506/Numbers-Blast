using System.Collections;
using UnityEngine;
using NumbersBlast.Data;
using NumbersBlast.Gameplay;
using NumbersBlast.Presentation;
using NumbersBlast.Settings;
using NumbersBlast.UI;

namespace NumbersBlast.App
{
    /// <summary>
    /// The end-of-run presentation: game-over SFX → "No valid moves left" banner → board fade wave →
    /// result panel. Pure choreography over existing views; the session decides WHEN a game is over
    /// and runs these as coroutines. Plain class — no scene contract impact. Scores are read from the
    /// services at panel-show time (after the banner/wave), never snapshotted early.
    /// </summary>
    internal sealed class GameOverSequence
    {
        private readonly TutorialOverlay _overlay;
        private readonly GameplayAnimationController _animation;
        private readonly BoardView _boardView;
        private readonly SfxPlayer _sfx;
        private readonly GameOverPanel _panel;
        private readonly BoardConfig _config;
        private readonly float _bannerSeconds;

        public GameOverSequence(TutorialOverlay overlay, GameplayAnimationController animation,
            BoardView boardView, SfxPlayer sfx, GameOverPanel panel,
            BoardConfig config, float bannerSeconds)
        {
            _overlay = overlay;
            _animation = animation;
            _boardView = boardView;
            _sfx = sfx;
            _panel = panel;
            _config = config;
            _bannerSeconds = bannerSeconds;
        }

        public IEnumerator PlaySolo(ScoreService localScore)
        {
            yield return ShowFailBanner();
            yield return PlayWave();
            _panel.ShowSolo(localScore.Score);
        }

        public IEnumerator PlayVersus(ScoreService localScore, ScoreService opponentScore)
        {
            yield return ShowFailBanner();
            yield return PlayWave();
            _panel.ShowVersus(localScore.Score, opponentScore.Score);
        }

        private IEnumerator ShowFailBanner()
        {
            if (_sfx != null) _sfx.PlayGameOver();

            if (_overlay != null)
            {
                yield return _overlay.ShowBanner("No valid moves left", _bannerSeconds);
            }
            else
            {
                yield return new WaitForSeconds(_bannerSeconds);
            }
        }

        private IEnumerator PlayWave()
        {
            if (_animation != null)
            {
                yield return _animation.PlayGameOverWave(_boardView, _config.Width, _config.Height);
            }
        }
    }
}
