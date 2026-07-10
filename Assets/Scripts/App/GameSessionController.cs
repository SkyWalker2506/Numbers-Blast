using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using NumbersBlast.Core;
using NumbersBlast.Data;
using NumbersBlast.Gameplay;
using NumbersBlast.Input;
using NumbersBlast.Opponent;
using NumbersBlast.Presentation;
using NumbersBlast.Settings;
using NumbersBlast.Tutorial;
using NumbersBlast.UI;

namespace NumbersBlast.App
{
    /// <summary>
    /// Composition root and orchestrator. Owns the board model, services and factory, wires the
    /// views, and exposes a small move API. Pure gameplay services stay UI-free; this MonoBehaviour
    /// is the one place allowed to know about presentation, input and the opponent controllers.
    /// </summary>
    public sealed class GameSessionController : MonoBehaviour
    {
        [Header("Config")]
        [SerializeField] private BoardConfig boardConfig;
        [SerializeField] private PieceSetConfig pieceSetConfig;
        [SerializeField] private float failBannerSeconds = 2f;

        [Header("Views")]
        [SerializeField] private GameObject gameplayRoot;
        [SerializeField] private BoardView boardView;
        [SerializeField] private PieceTrayView trayView;
        [SerializeField] private GameplayAnimationController animationController;
        [SerializeField] private ScoreView localScoreView;
        [Tooltip("The 'YOU' pill in the versus HUD; the big central solo score is hidden in Vs AI.")]
        [SerializeField] private ScoreView versusLocalScoreView;
        [SerializeField] private ScoreView opponentScoreView;
        [SerializeField] private TMP_Text bestScoreLabel;
        [SerializeField] private GameObject bestScoreRoot;
        [SerializeField] private GameObject versusHudRoot;
        [SerializeField] private TutorialOverlay tutorialOverlay;
        [SerializeField] private GameOverPanel gameOverPanel;
        [SerializeField] private MainMenuView mainMenuView;
        [SerializeField] private MatchmakingOverlay matchmakingOverlay;
        [Tooltip("In-game gear button. Only interactable during the player's turn, so Settings can " +
            "never be opened mid-resolve, mid-opponent-turn, or after game-over — closes off the whole " +
            "class of 'opponent moves in the background while Settings is open' bugs structurally.")]
        [SerializeField] private Button settingsGearButton;

        [Header("Controllers")]
        [SerializeField] private TutorialController tutorialController;
        [SerializeField] private TurnController turnController;
        [SerializeField] private SettingsPanel settingsPanel;
        [SerializeField] private SfxPlayer sfxPlayer;
        [SerializeField] private MusicPlayer musicPlayer;

        private BoardModel _board;
        private BoardModel _previewScratch;
        private PlacementService _placement;
        private PieceFactory _factory;
        private ScoreService _localScore;
        private ScoreService _opponentScore;
        private GameSettings _settings;
        private PieceDragController _activeDrag;
        private GameState _state = GameState.MainMenu;
        private GameMode _mode = GameMode.Solo;
        private bool _tutorialOnly;

        // Settings locks input via this flag instead of touching _state directly. Keeping the two
        // separate means _state always reflects the true game/resolve/turn state, even while Settings
        // is open — nothing can "stomp" ResolvingMove/GameOver into PlayerTurn/OpponentTurn anymore.
        private bool _inputLockedBySettings;

        // Persisted player meta (best score + first-launch tutorial flag) lives in PlayerProgress, so
        // this orchestrator never touches PlayerPrefs directly.
        private PlayerProgress _progress;

        // Authoritative tray contents (which pieces are live). The tray view only renders this — the
        // model is the single source of truth for HasAnyValidMove and opponent evaluation.
        private readonly TrayModel _trayModel = new TrayModel();

        public event Action<PlayerSide, MoveResult> MoveResolved;
        public event Action<PlayerSide> MoveStarted;

        public BoardModel Board => _board;
        public PlacementService Placement => _placement;
        // Read-only score access for the Vs AI rubber-band (opponent widens/narrows its pick by the gap).
        public int LocalScore => _localScore != null ? _localScore.Score : 0;
        public int OpponentScore => _opponentScore != null ? _opponentScore.Score : 0;
        public bool IsResolving => _state == GameState.ResolvingMove;
        public bool CanAcceptPlayerInput => _state == GameState.PlayerTurn && !IsResolving && !_inputLockedBySettings;

        /// <summary>
        /// The single place _state is ever assigned. Also keeps the in-game gear's interactable state
        /// in sync: it's only tappable during the player's turn, so Settings structurally can never be
        /// opened mid-resolve, mid-opponent-turn, or after game-over.
        /// </summary>
        private void SetState(GameState state)
        {
            if (!IsLegalTransition(_state, state))
            {
                // Warn only, never block — a cheap tripwire if the flow ever grows an unintended edge.
                Debug.LogWarning($"[NumbersBlast] Unexpected state transition {_state} -> {state}");
            }

            _state = state;
            if (settingsGearButton != null)
            {
                settingsGearButton.interactable = state == GameState.PlayerTurn;
            }
        }

        /// <summary>
        /// The transitions the flow actually uses. "→ MainMenu" (return/quit) and "→ PlayerTurn"
        /// (menu start, replay, and tutorial step loads, which can arrive from any state) are always
        /// legal; the rest must follow the resolve pipeline. Not a state-machine framework — a guard.
        /// </summary>
        private static bool IsLegalTransition(GameState from, GameState to)
        {
            if (to == GameState.MainMenu || to == GameState.PlayerTurn)
            {
                return true;
            }

            switch (from)
            {
                case GameState.PlayerTurn:    return to == GameState.ResolvingMove;
                case GameState.ResolvingMove: return to == GameState.OpponentTurn || to == GameState.GameOver;
                case GameState.OpponentTurn:  return to == GameState.ResolvingMove || to == GameState.GameOver;
                default:                      return false;
            }
        }

        private void Awake()
        {
            _settings = new GameSettings();
            _board = new BoardModel(boardConfig.Width, boardConfig.Height);
            _previewScratch = new BoardModel(boardConfig.Width, boardConfig.Height);
            _placement = new PlacementService(new MergeResolver(), new LineClearResolver());
            _factory = new PieceFactory(pieceSetConfig);
            _localScore = new ScoreService();
            _opponentScore = new ScoreService();
            _progress = new PlayerProgress();
            _progress.Load();
            _localScore.ScoreChanged += OnLocalScoreChanged;
            UpdateBestScoreLabel();

            if (sfxPlayer != null) sfxPlayer.Bind(_settings);
            if (musicPlayer != null) musicPlayer.Bind(_settings);
            if (settingsPanel != null) settingsPanel.Bind(_settings);

            boardView.Build(boardConfig);
            trayView.Initialize(this, boardView, boardConfig);
            if (tutorialController != null) tutorialController.Initialize(this);

            WireUi();
        }

        private void Start()
        {
            ReturnToMenu();
        }

        private void WireUi()
        {
            if (mainMenuView != null)
            {
                mainMenuView.PlaySolo += StartSolo;
                mainMenuView.PlayVsAi += StartVsAI;
                mainMenuView.PlayTutorial += StartTutorialOnly;
                mainMenuView.OpenSettings += () => settingsPanel.ShowFromMenu();
            }

            if (gameOverPanel != null)
            {
                gameOverPanel.Configure(Replay, ReturnToMenu);
            }

            if (settingsPanel != null)
            {
                settingsPanel.Restart += Replay;
                settingsPanel.MainMenu += ReturnToMenu;
                settingsPanel.Opened += OnSettingsOpened;
                settingsPanel.Closed += OnSettingsClosed;
            }

            if (tutorialController != null)
            {
                tutorialController.Completed += OnTutorialCompleted;
            }
        }

        private void OnTutorialCompleted()
        {
            // Replaying the tutorial from the menu returns there; the first Solo run marks the
            // tutorial as seen (so it never auto-runs again) and continues straight into play.
            if (_tutorialOnly)
            {
                ReturnToMenu();
            }
            else
            {
                _progress.MarkTutorialSeen();
                BeginSoloRun();
            }
        }

        // ---- Mode entry ----------------------------------------------------

        public void StartSolo()
        {
            StartSoloFlow(tutorialOnly: false);
        }

        /// <summary>Plays only the tutorial (from the menu "Tutorial" button) and returns to the menu when done.</summary>
        public void StartTutorialOnly()
        {
            StartSoloFlow(tutorialOnly: true);
        }

        private void StartSoloFlow(bool tutorialOnly)
        {
            _mode = GameMode.Solo;
            _tutorialOnly = tutorialOnly;
            ShowGameplay();
            if (versusHudRoot != null) versusHudRoot.SetActive(false);
            if (opponentScoreView != null) opponentScoreView.gameObject.SetActive(false);
            // Best score only makes sense as a solo "beat your record" stat; a versus match already
            // has two live scores (you/opponent) and showing career-best too would just clutter it.
            if (bestScoreRoot != null) bestScoreRoot.SetActive(true);
            // Local ScoreView has no wired label (the HUD's big central number has no caption),
            // so it's bound without a label string.
            if (localScoreView != null)
            {
                localScoreView.gameObject.SetActive(true);   // Vs AI hides it in favour of the YOU pill
                localScoreView.Bind(_localScore);
            }

            _board.ClearAll();
            boardView.Refresh(_board);
            gameOverPanel.Hide();

            // Tutorial runs only when explicitly requested (button) or the first time solo is played.
            bool firstSolo = !_progress.HasSeenTutorial;
            bool runTutorial = tutorialController != null && tutorialController.HasSteps
                && (tutorialOnly || firstSolo);

            if (runTutorial)
            {
                // No dedicated GameState here: StartTutorial's first step synchronously calls
                // ApplyTutorialStep, which sets _state to PlayerTurn (gated by IsMoveAllowed).
                tutorialController.StartTutorial();
            }
            else if (tutorialOnly)
            {
                ReturnToMenu();
            }
            else
            {
                BeginSoloRun();
            }
        }

        private void BeginSoloRun()
        {
            _inputLockedBySettings = false;   // a fresh run always accepts input (belt-and-suspenders)
            if (tutorialController != null) tutorialController.StopTutorial();
            _board.ClearAll();
            boardView.Refresh(_board);
            boardView.ClearTutorialHighlight();
            _localScore.Reset();
            if (tutorialOverlay != null) tutorialOverlay.HideInstruction();
            RefillTray();
            SetState(GameState.PlayerTurn);
        }

        /// <summary>Starts a fresh Vs AI match, with a brief fake "finding opponent" connect.</summary>
        public void StartVsAI()
        {
            StartCoroutine(ConnectAndStartVsAI());
        }

        private IEnumerator ConnectAndStartVsAI()
        {
            // A restart can arrive mid-match (Settings ▸ Restart): kill the live MatchLoop FIRST, or
            // StartTurns below would stack a second loop on the shared flags — and the old turn timer
            // could even fire during matchmaking and penalise the fresh score.
            if (turnController != null) turnController.Stop();

            if (matchmakingOverlay != null)
            {
                yield return matchmakingOverlay.PlayConnecting();
            }

            _mode = GameMode.VsAI;
            _inputLockedBySettings = false;   // a fresh match always accepts input (belt-and-suspenders)
            ShowGameplay();
            if (versusHudRoot != null) versusHudRoot.SetActive(true);
            if (opponentScoreView != null) opponentScoreView.gameObject.SetActive(true);
            if (bestScoreRoot != null) bestScoreRoot.SetActive(false);

            if (tutorialController != null) tutorialController.StopTutorial();
            _board.ClearAll();
            boardView.Refresh(_board);
            boardView.ClearTutorialHighlight();
            if (tutorialOverlay != null) tutorialOverlay.HideInstruction();
            _localScore.Reset();
            _opponentScore.Reset();
            // The versus scoreboard is the two corner pills; the big central solo score would
            // duplicate the YOU pill, so it's hidden for the match.
            if (localScoreView != null) localScoreView.gameObject.SetActive(false);
            if (versusLocalScoreView != null) versusLocalScoreView.Bind(_localScore, "YOU");
            if (opponentScoreView != null) opponentScoreView.Bind(_opponentScore, "OPPONENT");
            gameOverPanel.Hide();

            RefillTray();

            turnController.Initialize(this, trayView, boardView);
            turnController.StartTurns();
        }

        // ---- Move API ------------------------------------------------------

        public MoveResult PreviewMove(PieceInstance piece, Vector2Int anchor)
        {
            _previewScratch.CopyFrom(_board);
            return _placement.ApplyMove(_previewScratch, piece, anchor);
        }

        public bool CanSubmitMove(PieceInstance piece, Vector2Int anchor)
        {
            if (!CanAcceptPlayerInput)
            {
                return false;
            }

            if (!_placement.CanPlace(_board, piece, anchor))
            {
                return false;
            }

            if (tutorialController != null && tutorialController.IsRunning
                && !tutorialController.IsMoveAllowed(piece, anchor))
            {
                return false;
            }

            return true;
        }

        public void ApplyPlayerMove(PieceInstance piece, Vector2Int anchor)
        {
            ApplyMoveInternal(piece, anchor, PlayerSide.Local, _localScore);
        }

        public void ApplyOpponentMove(PieceInstance piece, Vector2Int anchor)
        {
            ApplyMoveInternal(piece, anchor, PlayerSide.Opponent, _opponentScore);
        }

        private void ApplyMoveInternal(PieceInstance piece, Vector2Int anchor, PlayerSide side, ScoreService score)
        {
            MoveResult result = _placement.ApplyMove(_board, piece, anchor);
            if (!result.IsValid)
            {
                return;
            }

            // Single-consumption, and only after the pipeline accepted the move: the piece leaves the
            // authoritative model and its view together, here — callers never pre-consume, so a move
            // that fails validation can't half-consume anything.
            trayView.ConsumePiece(trayView.FindViewFor(piece));
            _trayModel.Remove(piece);
            Debug.Assert(_trayModel.Pieces.Count == trayView.ActiveCount,
                "Tray model/view desync after placement");

            MoveStarted?.Invoke(side);
            score.Add(result.ScoreGain);
            StartCoroutine(ResolveRoutine(result, side));
        }

        private IEnumerator ResolveRoutine(MoveResult result, PlayerSide side)
        {
            SetState(GameState.ResolvingMove);
            boardView.Refresh(_board);
            PlaySfxFor(result);

            if (animationController != null)
            {
                yield return animationController.PlayMove(result, boardView);
            }

            // During the tutorial each step supplies its own pieces, so don't auto-refill a random
            // 3-piece tray after the single taught block is placed — keep it simple.
            bool tutorialActive = tutorialController != null && tutorialController.IsRunning;
            if (!tutorialActive && _trayModel.IsEmpty)
            {
                RefillTray();
            }

            OnMoveResolved(side, result);
        }

        private void OnMoveResolved(PlayerSide side, MoveResult result)
        {
            if (_mode == GameMode.Solo)
            {
                if (tutorialController != null && tutorialController.IsRunning)
                {
                    tutorialController.NotifyMoveCompleted(result);
                    return;
                }

                if (!HasAnyValidMove())
                {
                    StartCoroutine(SoloGameOver());
                }
                else
                {
                    SetState(GameState.PlayerTurn);
                }
            }
            else
            {
                MoveResolved?.Invoke(side, result);
            }
        }

        // ---- Fail / game over ---------------------------------------------

        private IEnumerator SoloGameOver()
        {
            SetState(GameState.GameOver);
            yield return ShowFailBanner();
            yield return PlayGameOverWave();
            gameOverPanel.ShowSolo(_localScore.Score);
        }

        public void EndVsGame()
        {
            StartCoroutine(VsGameOver());
        }

        private IEnumerator VsGameOver()
        {
            SetState(GameState.GameOver);
            yield return ShowFailBanner();
            yield return PlayGameOverWave();
            gameOverPanel.ShowVersus(_localScore.Score, _opponentScore.Score);
        }

        private IEnumerator ShowFailBanner()
        {
            if (sfxPlayer != null) sfxPlayer.PlayGameOver();

            if (tutorialOverlay != null)
            {
                yield return tutorialOverlay.ShowBanner("No valid moves left", failBannerSeconds);
            }
            else
            {
                yield return new WaitForSeconds(failBannerSeconds);
            }
        }

        private IEnumerator PlayGameOverWave()
        {
            if (animationController != null)
            {
                yield return animationController.PlayGameOverWave(boardView, boardConfig.Width, boardConfig.Height);
            }
        }

        // ---- Turn / input state (used by TurnController) -------------------

        public void EnterPlayerTurn()
        {
            SetState(GameState.PlayerTurn);
        }

        public void EnterOpponentTurn()
        {
            SetState(GameState.OpponentTurn);
        }

        /// <summary>
        /// Locks/unlocks player input without touching <see cref="_state"/> — Settings can open on top
        /// of any state (though in practice the gear is only interactable during PlayerTurn) without
        /// ever corrupting what the real underlying state is.
        /// </summary>
        public void SetInputLocked(bool locked)
        {
            _inputLockedBySettings = locked;
            if (locked)
            {
                // A drag still in progress when input locks (e.g. a second finger opens Settings mid-drag)
                // must be released instantly, not left running under the panel.
                CancelActiveDrag();
            }
        }

        public bool HasAnyValidMove()
        {
            return _placement.HasAnyValidMove(_board, _trayModel.Pieces);
        }

        public IReadOnlyList<PieceInstance> GetTrayPieces()
        {
            return _trayModel.Pieces;
        }

        public void ApplyTimeoutPenalty(PlayerSide side)
        {
            ScoreService score = side == PlayerSide.Local ? _localScore : _opponentScore;
            score.ApplyPenaltyPercent(boardConfig.TimeoutPenaltyPercent);
            if (sfxPlayer != null) sfxPlayer.PlayInvalid();
        }

        // ---- Active drag (for timeout cancel) -----------------------------

        public void SetActiveDrag(PieceDragController drag)
        {
            _activeDrag = drag;
        }

        public void ClearActiveDrag(PieceDragController drag)
        {
            if (_activeDrag == drag)
            {
                _activeDrag = null;
            }
        }

        public void CancelActiveDrag()
        {
            if (_activeDrag != null)
            {
                _activeDrag.CancelDrag();
                _activeDrag = null;
            }
        }

        public void NotifyInvalidDrop()
        {
            if (sfxPlayer != null) sfxPlayer.PlayInvalid();
        }

        // ---- Tutorial preset ----------------------------------------------

        public void ApplyTutorialStep(TutorialStepDefinition step)
        {
            _board.ClearAll();
            foreach (BoardCellPreset cell in step.BoardCells)
            {
                _board.SetValue(cell.Position, cell.Value);
            }

            boardView.Refresh(_board);

            var pieces = new List<PieceInstance>();
            foreach (TutorialPiecePreset tp in step.TrayPieces)
            {
                var cells = new List<PieceCellData>();
                foreach (PieceCellPreset pc in tp.Cells)
                {
                    cells.Add(new PieceCellData(pc.Offset, pc.Value));
                }

                pieces.Add(new PieceInstance(cells));
            }

            _trayModel.SetPieces(pieces);
            trayView.ShowPieces(pieces);
            boardView.SetTutorialHighlight(step.HighlightedCells);
            SetState(GameState.PlayerTurn);
        }

        // ---- Settings input lock -------------------------------------------
        // Deliberately NO timer pause in Vs AI: like a real online match, opening settings doesn't
        // stop the clock — the turn can time out (penalty + pass) while the panel is up.

        private void OnSettingsOpened()
        {
            // Opened from the main menu there's no run to lock.
            if (_state == GameState.MainMenu)
            {
                return;
            }

            SetInputLocked(true);
        }

        private void OnSettingsClosed()
        {
            if (_state == GameState.MainMenu)
            {
                return;
            }

            SetInputLocked(false);
        }

        // ---- Settings gear (in-game) ---------------------------------------

        /// <summary>
        /// Opens settings from the in-game gear. Hides Restart while the tutorial is running — the
        /// tutorial script has no meaningful "restart", only Main Menu / Close apply then.
        /// </summary>
        public void OpenInGameSettings()
        {
            bool tutorialActive = tutorialController != null && tutorialController.IsRunning;
            settingsPanel.Show(!tutorialActive);
        }

        // ---- Flow helpers -------------------------------------------------

        public void Replay()
        {
            if (_mode == GameMode.Solo)
            {
                gameOverPanel.Hide();
                StartSolo();
            }
            else
            {
                // Keep the game-over panel up behind the "finding opponent" overlay (hidden once
                // ConnectAndStartVsAI actually rebuilds the board) instead of cutting to a stale board.
                StartCoroutine(ConnectAndStartVsAI());
            }
        }

        public void ReturnToMenu()
        {
            CancelActiveDrag();   // defensive: never leave a drag running when the run tears down
            // Clear the settings input-lock: the Settings panel's "Main Menu" button hides itself
            // WITHOUT raising Closed, so without this the lock could stay stuck true and silently kill
            // dragging in the next run (settings opened in-game → Main Menu → start a new game).
            _inputLockedBySettings = false;
            if (turnController != null) turnController.Stop();
            if (tutorialController != null) tutorialController.StopTutorial();
            if (localScoreView != null) localScoreView.gameObject.SetActive(true);   // undo the Vs AI hide
            gameOverPanel.Hide();
            HideGameplay();
            if (mainMenuView != null) mainMenuView.Show();
            SetState(GameState.MainMenu);
        }

        private void RefillTray()
        {
            int count = boardConfig.TrayPieceCount;
            var pieces = new List<PieceInstance>(count);
            for (int i = 0; i < count; i++)
            {
                pieces.Add(_factory.CreateRandomPiece());
            }

            _trayModel.SetPieces(pieces);
            trayView.ShowPieces(pieces);
        }

        private void OnLocalScoreChanged(int value)
        {
            // Best score is a SOLO-only stat and never counts the tutorial's scripted demo clear;
            // PlayerProgress.TrySubmitBestScore enforces that (and persists) — see PlayerProgress.
            bool tutorialActive = tutorialController != null && tutorialController.IsRunning;
            if (_progress.TrySubmitBestScore(value, _mode, tutorialActive))
            {
                UpdateBestScoreLabel();
            }
        }

        private void UpdateBestScoreLabel()
        {
            if (bestScoreLabel != null)
            {
                bestScoreLabel.text = _progress.BestScore.ToString();
            }
        }

        private void PlaySfxFor(MoveResult result)
        {
            if (sfxPlayer == null)
            {
                return;
            }

            if (result.HasLineClear)
            {
                sfxPlayer.PlayClear();
            }
            else if (result.HasMerge)
            {
                sfxPlayer.PlayMerge();
            }
            else
            {
                sfxPlayer.PlayPlace();
            }
        }

        private void ShowGameplay()
        {
            if (gameplayRoot != null) gameplayRoot.SetActive(true);
            if (mainMenuView != null) mainMenuView.Hide();
        }

        private void HideGameplay()
        {
            if (gameplayRoot != null) gameplayRoot.SetActive(false);
        }
    }
}
