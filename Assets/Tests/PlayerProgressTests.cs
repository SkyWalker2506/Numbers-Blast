using NUnit.Framework;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.App;

namespace NumbersBlast.Tests
{
    /// <summary>
    /// EditMode tests for the persisted-meta guard. Uses a test key prefix and deletes those keys in
    /// TearDown, so it never touches the player's real best-score / tutorial-seen data.
    /// </summary>
    public sealed class PlayerProgressTests
    {
        private const string Prefix = "test_nbpp_";

        [TearDown]
        public void Cleanup()
        {
            PlayerPrefs.DeleteKey(Prefix + "nb_best_score");
            PlayerPrefs.DeleteKey(Prefix + "nb_tutorial_seen");
            PlayerPrefs.Save();
        }

        private static PlayerProgress Fresh()
        {
            var p = new PlayerProgress(Prefix);
            p.Load();
            return p;
        }

        [Test]
        public void PlayerProgress_TrySubmitBestScore_RejectsVsAI()
        {
            var p = Fresh();
            Assert.IsFalse(p.TrySubmitBestScore(100, GameMode.VsAI, tutorialActive: false));
            Assert.AreEqual(0, p.BestScore);
        }

        [Test]
        public void PlayerProgress_TrySubmitBestScore_RejectsTutorial()
        {
            var p = Fresh();
            Assert.IsFalse(p.TrySubmitBestScore(100, GameMode.Solo, tutorialActive: true));
            Assert.AreEqual(0, p.BestScore);
        }

        [Test]
        public void PlayerProgress_TrySubmitBestScore_RejectsLowerScore()
        {
            var p = Fresh();
            Assert.IsTrue(p.TrySubmitBestScore(50, GameMode.Solo, tutorialActive: false));
            Assert.IsFalse(p.TrySubmitBestScore(40, GameMode.Solo, tutorialActive: false));
            Assert.AreEqual(50, p.BestScore);
        }

        [Test]
        public void PlayerProgress_MarkTutorialSeen_Persists()
        {
            var p = Fresh();
            Assert.IsFalse(p.HasSeenTutorial);

            p.MarkTutorialSeen();
            Assert.IsTrue(p.HasSeenTutorial);

            Assert.IsTrue(Fresh().HasSeenTutorial);   // survives a reload
        }
    }
}
