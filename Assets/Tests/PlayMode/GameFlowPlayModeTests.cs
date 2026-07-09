using System.Collections;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.SceneManagement;
using NumbersBlast.App;
using NumbersBlast.Core;
using NumbersBlast.Gameplay;
using NumbersBlast.Presentation;

namespace NumbersBlast.PlayModeTests
{
    /// <summary>
    /// PlayMode smoke tests: load the real Game scene and drive it through the controlled session API
    /// (no simulated pointer/drag). Marks the tutorial as already seen so the first solo doesn't gate
    /// input, and restores the player's real key afterwards.
    /// </summary>
    public sealed class GameFlowPlayModeTests
    {
        private const string TutorialKey = "nb_tutorial_seen";
        private int _prevTutorialSeen;

        [SetUp]
        public void SetUp()
        {
            _prevTutorialSeen = PlayerPrefs.GetInt(TutorialKey, 0);
            PlayerPrefs.SetInt(TutorialKey, 1);   // skip the forced first-solo tutorial gating
            PlayerPrefs.Save();
        }

        [TearDown]
        public void TearDown()
        {
            Time.timeScale = 1f;   // some tests accelerate time; never leak that into the next test
            PlayerPrefs.SetInt(TutorialKey, _prevTutorialSeen);
            PlayerPrefs.Save();
        }

        private static IEnumerator LoadGameScene()
        {
            yield return SceneManager.LoadSceneAsync("Game", LoadSceneMode.Single);
            yield return null;   // let Awake/Start run
        }

        [UnityTest]
        public IEnumerator Scene_Loads_WithSessionPresent()
        {
            yield return LoadGameScene();
            Assert.IsNotNull(Object.FindFirstObjectByType<GameSessionController>(), "GameSessionController in the loaded scene");
        }

        [UnityTest]
        public IEnumerator Solo_Starts_ThenAValidMoveResolves()
        {
            yield return LoadGameScene();
            var session = Object.FindFirstObjectByType<GameSessionController>();
            // The tray lives under GameplayRoot, which is inactive at the menu — include inactive.
            var trayView = Object.FindFirstObjectByType<PieceTrayView>(FindObjectsInactive.Include);
            Assert.IsNotNull(session);
            Assert.IsNotNull(trayView);

            session.StartSolo();
            yield return null;

            var tray = session.GetTrayPieces();
            Assert.AreEqual(3, tray.Count, "solo tray refills to 3 pieces");

            BoardModel board = session.Board;
            PlacementService placement = session.Placement;
            PieceInstance piece = null;
            Vector2Int at = default;
            bool found = false;
            foreach (PieceInstance p in tray)
            {
                for (int y = 0; y < board.Height && !found; y++)
                {
                    for (int x = 0; x < board.Width && !found; x++)
                    {
                        var a = new Vector2Int(x, y);
                        if (placement.CanPlace(board, p, a))
                        {
                            piece = p;
                            at = a;
                            found = true;
                        }
                    }
                }
            }

            Assert.IsTrue(found, "a valid solo move exists");

            // Drive it exactly like the drag does — the session consumes the model and the view
            // together once the pipeline accepts the move.
            session.ApplyPlayerMove(piece, at);
            yield return new WaitForSeconds(1.0f);   // let the resolve routine + refill finish

            Assert.GreaterOrEqual(session.GetTrayPieces().Count, 1, "tray still has / refilled pieces after the move");
        }

        [UnityTest]
        public IEnumerator VsAI_Starts_AndReachesPlayerTurn()
        {
            yield return LoadGameScene();
            var session = Object.FindFirstObjectByType<GameSessionController>();
            Assert.IsNotNull(session);

            Time.timeScale = 5f;   // speed up the fake matchmaking; restored below
            session.StartVsAI();

            // Wait (generously) for matchmaking to complete and the player's turn to begin.
            float timeout = 8f;
            while (timeout > 0f && !session.CanAcceptPlayerInput)
            {
                timeout -= Time.unscaledDeltaTime;
                yield return null;
            }

            Time.timeScale = 1f;
            Assert.IsTrue(session.CanAcceptPlayerInput, "Vs AI reaches the player's turn after matchmaking");
            Assert.AreEqual(3, session.GetTrayPieces().Count, "Vs AI tray has 3 pieces");
        }

        [UnityTest]
        public IEnumerator VsAI_PlayerTimeout_PassesTurnToOpponent_ThenBackToPlayer()
        {
            // Regression scope: the MatchLoop timeout branch (previously untested) — the player makes
            // NO move, the 20s timer expires, the turn must pass to the opponent (who visibly places a
            // piece) and then come back to the player. Also covers the side-labeled MoveResolved fix:
            // the opponent's late resolve must not satisfy the player-turn wait.
            yield return LoadGameScene();
            var session = Object.FindFirstObjectByType<GameSessionController>();
            Assert.IsNotNull(session);

            Time.timeScale = 10f;   // 20s timer + the opponent's think/travel beats, compressed
            session.StartVsAI();

            float budget = 10f;
            while (budget > 0f && !session.CanAcceptPlayerInput)
            {
                budget -= Time.unscaledDeltaTime;
                yield return null;
            }

            Assert.IsTrue(session.CanAcceptPlayerInput, "reached the player's first turn");
            int occupiedBefore = CountOccupied(session.Board);

            // Make no move: wait through the timeout, the opponent's act, and the handover back.
            budget = 30f;
            bool opponentPlaced = false;
            bool backToPlayer = false;
            while (budget > 0f)
            {
                budget -= Time.unscaledDeltaTime;
                if (!opponentPlaced && CountOccupied(session.Board) > occupiedBefore)
                {
                    opponentPlaced = true;   // the opponent placed after our timeout
                }

                if (opponentPlaced && session.CanAcceptPlayerInput)
                {
                    backToPlayer = true;     // and the turn came back to us exactly once
                    break;
                }

                yield return null;
            }

            Time.timeScale = 1f;
            Assert.IsTrue(opponentPlaced, "opponent placed a piece after the player's timeout");
            Assert.IsTrue(backToPlayer, "the turn returned to the player after the opponent's move");
        }

        private static int CountOccupied(BoardModel board)
        {
            int count = 0;
            for (int y = 0; y < board.Height; y++)
            {
                for (int x = 0; x < board.Width; x++)
                {
                    if (board.IsOccupied(new Vector2Int(x, y)))
                    {
                        count++;
                    }
                }
            }

            return count;
        }

        [UnityTest]
        public IEnumerator InputLock_FromInGameSettings_ClearsWhenReturningToMenu()
        {
            // Regression: the Settings "Main Menu" button hides the panel WITHOUT a Closed event, so the
            // in-game input lock could stay stuck true and silently kill dragging in the next run.
            yield return LoadGameScene();
            var session = Object.FindFirstObjectByType<GameSessionController>();

            session.StartSolo();
            yield return null;

            session.SetInputLocked(true);   // simulate opening Settings mid-run
            Assert.IsFalse(session.CanAcceptPlayerInput, "input is locked while (simulated) Settings is open");

            session.ReturnToMenu();
            yield return null;
            session.StartSolo();            // a fresh run must accept input again
            yield return null;

            Assert.IsTrue(session.CanAcceptPlayerInput, "no stuck input lock — the next run is draggable");
        }
    }
}
