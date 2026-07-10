using System.Collections;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.App;
using NumbersBlast.Gameplay;
using NumbersBlast.Presentation;
using NumbersBlast.UI;

namespace NumbersBlast.Opponent
{
    /// <summary>
    /// Drives Part 2 turn order, the 20s player timer, timeout penalty, and the opponent turn.
    /// Core gameplay never references this; the session exposes a small API this controller uses.
    /// </summary>
    public sealed class TurnController : MonoBehaviour
    {
        [SerializeField] private TurnTimer turnTimer;
        [SerializeField] private OpponentPresentationController opponentPresentation;
        [SerializeField] private TurnView turnView;
        [SerializeField] private TimerView timerView;

        [Header("Opponent tuning")]
        [Tooltip("Player turn length in seconds.")]
        [SerializeField] private float playerTurnSeconds = 20f;
        [Tooltip("Score gap (opponent − local) beyond which the AI loosens (when ahead) or sharpens to " +
            "its best move (when behind). Smaller = tighter rubber-band. Keeps matches close.")]
        [SerializeField] private int rubberBandThreshold = 15;

        private GameSessionController _session;
        private PieceTrayView _tray;
        private BoardView _boardView;
        private OpponentMoveEvaluator _evaluator;
        private bool _initialized;
        private bool _active;

        // The MatchLoop waits on these; input stays event-driven (OnMoveResolved / OnTimeout raise them).
        private bool _moveResolved;
        private bool _timedOut;
        private bool _moveStarted;   // a move committed this player turn — beats a same-frame timeout

        public PlayerSide CurrentTurn { get; private set; }

        public void Initialize(GameSessionController session, PieceTrayView tray, BoardView boardView)
        {
            _session = session;
            _tray = tray;
            _boardView = boardView;
            _evaluator = new OpponentMoveEvaluator(rubberBandThreshold);

            if (!_initialized)
            {
                _session.MoveResolved += OnMoveResolved;
                _session.MoveStarted += OnMoveStarted;
                turnTimer.Ticked += OnTick;
                turnTimer.TimedOut += OnTimeout;
                _initialized = true;
            }
        }

        public void StartTurns()
        {
            _active = true;
            turnView.SetVisible(true);
            StartCoroutine(MatchLoop());
        }

        public void Stop()
        {
            _active = false;
            turnTimer.Stop();
            timerView.SetVisible(false);
            turnView.SetVisible(false);
            // Kill any in-flight opponent presentation (OpponentRoutine + its nested PlayMove) so it
            // can't keep animating a piece that ReturnToMenu/replay is about to destroy.
            StopAllCoroutines();
        }

        /// <summary>
        /// The whole Vs AI match as one readable top-down coroutine (replaces the old event ping-pong
        /// across three classes). Player input stays event-driven: MoveResolved / TimedOut just raise
        /// flags this loop waits on; MoveStarted still stops the timer as an event (timing-critical).
        /// Fail state is checked after the player's move (covers "before opponent turn") and again
        /// after the opponent's — exactly the two checks the old flow made.
        /// </summary>
        private IEnumerator MatchLoop()
        {
            while (_active)
            {
                // ---- player turn: wait for the move to resolve, or the timer to run out ----
                _moveResolved = false;
                _timedOut = false;
                _moveStarted = false;
                BeginPlayerTurn();
                yield return new WaitUntil(() => _moveResolved || _timedOut || !_active);
                if (!_active) yield break;

                if (_timedOut && !_moveStarted)
                {
                    // True timeout — no move was made: cancel any half-finished drag, apply the penalty,
                    // pass the turn (board unchanged, so no resolve to wait for).
                    _session.CancelActiveDrag();
                    _session.ApplyTimeoutPenalty(PlayerSide.Local);
                    turnTimer.Stop();
                }
                else if (!_moveResolved)
                {
                    // A move was committed in the same frame the timer expired (the Update order between
                    // the timer and the EventSystem is unspecified, so both flags can be raised in one
                    // frame). The move wins — no penalty; just wait for its resolve like a normal turn.
                    yield return new WaitUntil(() => _moveResolved || !_active);
                    if (!_active) yield break;
                }

                if (!_session.HasAnyValidMove())
                {
                    EndGame();
                    yield break;
                }

                // ---- opponent turn: evaluate, present, wait for its resolve ----
                BeginOpponentTurn();
                int scoreDelta = _session.OpponentScore - _session.LocalScore;
                OpponentMove move = _evaluator.ChooseMove(_session.Board, _session.Placement, _session.GetTrayPieces(), scoreDelta);
                if (move == null)
                {
                    EndGame();
                    yield break;
                }

                // Hesitate longer when the top two candidates are close, snappier when one clearly wins.
                float thinkScale = Mathf.Lerp(1.15f, 0.6f, Mathf.InverseLerp(0f, 25f, _evaluator.LastTopGap));

                _moveResolved = false;
                yield return opponentPresentation.PlayMove(move, _tray, _boardView, _session, thinkScale);
                yield return new WaitUntil(() => _moveResolved || !_active);
                if (!_active) yield break;

                if (!_session.HasAnyValidMove())
                {
                    EndGame();
                    yield break;
                }
            }
        }

        private void BeginPlayerTurn()
        {
            CurrentTurn = PlayerSide.Local;
            turnView.SetTurn(PlayerSide.Local);
            timerView.SetVisible(true);
            _session.EnterPlayerTurn();
            turnTimer.StartCountdown(playerTurnSeconds);
        }

        private void BeginOpponentTurn()
        {
            CurrentTurn = PlayerSide.Opponent;
            turnView.SetTurn(PlayerSide.Opponent);
            timerView.SetVisible(false);
            _session.EnterOpponentTurn();
        }

        private void OnMoveStarted(PlayerSide side)
        {
            // Freeze the timer the instant a move is committed, before its resolve animation.
            if (side == PlayerSide.Local)
            {
                _moveStarted = true;   // MatchLoop: a committed move beats a same-frame timeout
            }
            turnTimer.Stop();
        }

        private void OnMoveResolved(PlayerSide side, MoveResult result)
        {
            // Only the side whose turn it is may close the wait: a move whose resolve animation lands
            // after the turn has already advanced must not satisfy the NEXT wait (that stale flag is
            // exactly what could skip a player turn and let the opponent play twice).
            if (side == CurrentTurn)
            {
                _moveResolved = true;   // MatchLoop consumes this
            }
        }

        private void OnTick(float remaining, float total)
        {
            timerView.SetTime(remaining, total);
        }

        private void OnTimeout()
        {
            if (_active && CurrentTurn == PlayerSide.Local)
            {
                _timedOut = true;   // MatchLoop handles cancel + penalty + pass
            }
        }

        private void EndGame()
        {
            _active = false;
            turnTimer.Stop();
            timerView.SetVisible(false);
            turnView.SetVisible(false);
            _session.EndVsGame();
        }
    }
}
