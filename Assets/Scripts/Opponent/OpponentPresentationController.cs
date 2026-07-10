using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.App;
using NumbersBlast.Gameplay;
using NumbersBlast.Presentation;

namespace NumbersBlast.Opponent
{
    /// <summary>
    /// Makes the opponent's move visible on the local screen, matching the case brief's "select a
    /// block, hover over cells, try placements, cancel, hesitate, then place" beats. Movement is eased
    /// and slightly curved, hovers bob gently, and the timings/paths are jittered so it reads like a
    /// human hand rather than a robot snapping between cells. The opponent never plays off-screen.
    /// </summary>
    public sealed class OpponentPresentationController : MonoBehaviour
    {
        [SerializeField] private float moveSpeed = 1000f;
        [Tooltip("How high the held piece floats above the cell it's previewing. Kept under half a " +
            "cell so the live-resolved preview lands on that same cell (glow stays under the piece).")]
        [SerializeField] private float dragYOffset = 45f;
        [SerializeField] private float hoverHesitation = 0.5f;
        [Tooltip("Random think-time before the opponent touches a piece, so it never moves instantly.")]
        [SerializeField] private float preMoveDelayMin = 1.6f;
        [SerializeField] private float preMoveDelayMax = 2.7f;
        [Tooltip("Idle bob amplitude (px) while the opponent hovers a cell, so it never sits perfectly still.")]
        [SerializeField] private float bobAmount = 7f;
        [Tooltip("How much the travel path bows sideways (fraction of distance) for a non-robotic arc.")]
        [SerializeField] private float pathCurve = 0.13f;
        [Tooltip("Fewest fake 'try a cell' attempts before placing. 0 = sometimes it just grabs the " +
            "piece and takes it straight to its cell — decisive turns read as human as hesitant ones.")]
        [SerializeField] private int minAttempts = 0;
        [Tooltip("Most fake 'try a cell' attempts (inclusive). Actual count is random in [min, max] each turn.")]
        [SerializeField] private int maxAttempts = 2;
        [Tooltip("Chance [0,1] of actually attempting an invalid drop first, bouncing back, then retrying (about 1 in 20 by default).")]
        [SerializeField] private float misdropChance = 0.05f;
        [Tooltip("Chance [0,1] of first picking up a DIFFERENT tray piece, considering a cell, then " +
            "putting it back and playing the real one — the brief's 'cancel' beat, literally.")]
        [SerializeField] private float swapFakeoutChance = 0.25f;

        // Decides the beat list (which cells, how long, whether to misdrop); this controller only plays it.
        private OpponentActPlanner _planner;

        private void Awake()
        {
            RebuildPlanner();   // planner built from the serialized tuning fields
        }

        private void RebuildPlanner()
        {
            _planner = new OpponentActPlanner(minAttempts, maxAttempts, preMoveDelayMin, preMoveDelayMax,
                hoverHesitation, misdropChance, swapFakeoutChance);
        }

        // Live preview context for the current move. The glow is NOT a hover-only mechanic: it runs the
        // same continuous PiecePlacementPreview.Tick the player's drag uses, every frame the piece
        // moves, so it updates on cell-change while travelling — not only when the piece stops.
        private PieceView _previewView;
        private BoardView _previewBoard;
        private GameSessionController _previewSession;
        private Camera _previewCam;
        private Vector2Int _previewAnchor;
        private bool _previewShown;

        /// <summary>One shared, position-driven preview step — same mechanism the player runs each drag frame.</summary>
        private void TickPreview()
        {
            if (_previewView != null)
            {
                PiecePlacementPreview.Tick(_previewView, _previewBoard, _previewSession, _previewCam,
                    ref _previewAnchor, ref _previewShown, out _);
            }
        }

        public IEnumerator PlayMove(OpponentMove move, PieceTrayView tray, BoardView board, GameSessionController session, float thinkScale = 1f)
        {
            _previewView = null;   // nothing held yet
            PieceView view = tray.FindViewFor(move.Piece);
            if (view == null)
            {
                session.ApplyOpponentMove(move.Piece, move.Anchor);
                yield break;
            }

            Canvas canvas = view.GetComponentInParent<Canvas>();
            Transform layer = canvas != null ? canvas.transform : view.transform.root;
            // Overlay canvases resolve screen points with a null camera; match the player's drag path.
            Camera cam = (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay) ? canvas.worldCamera : null;
            RectTransform rect = view.RectTransform;
            Vector3 offset = new Vector3(0f, dragYOffset, 0f);
            Vector3 homePos = rect.position;   // tray slot, for a natural invalid-drop bounce-back

            // The planner decides the whole turn (think delay, 1–4 decoy cells, dwell times, whether to
            // misdrop); this controller just plays each beat with its motion primitives.
            List<OpponentBeat> beats = _planner.Plan(session.Board, session.Placement, move, thinkScale,
                session.GetTrayPieces());
            Vector3 currentPos = homePos;

            foreach (OpponentBeat beat in beats)
            {
                // The piece can be destroyed mid-turn (return to menu / replay) — bail cleanly.
                if (view == null)
                {
                    _previewView = null;
                    yield break;
                }

                switch (beat.Type)
                {
                    case OpponentBeatType.SwapFakeout:
                        // Runs BEFORE the real piece is picked up (the planner emits it first).
                        yield return SwapFakeout(beat, tray, board, session, cam, layer, offset);
                        break;

                    case OpponentBeatType.SelectDelay:
                        // "Select": a randomized think, then pick the piece up and scale it like the player.
                        yield return new WaitForSeconds(beat.Seconds);
                        if (view == null) { _previewView = null; yield break; }
                        view.transform.SetParent(layer, true);
                        view.transform.SetAsLastSibling();
                        view.SetScale(1.2f);
                        RerollIdle();   // seed the additive idle layer before the first travel
                        // Arm the continuous, position-driven preview for this move.
                        _previewView = view;
                        _previewBoard = board;
                        _previewSession = session;
                        _previewCam = cam;
                        _previewShown = false;
                        break;

                    case OpponentBeatType.TravelTo:
                        // Positioned by the reference cell so the live-resolved glow lands on that cell.
                        currentPos = CellTargetPos(view, rect, board, beat.Cell, offset);
                        yield return MoveTo(rect, currentPos);
                        break;

                    case OpponentBeatType.Hover:
                        yield return Hover(rect, currentPos, beat.Seconds);
                        break;

                    case OpponentBeatType.Misdrop:
                        yield return AttemptMisdrop(rect, board, offset, homePos, beat.Cell, session);
                        if (view != null) view.SetScale(1.2f);
                        break;

                    case OpponentBeatType.FinalPlace:
                        _previewView = null;   // stop the preview tick, then clear and drop
                        board.ClearPreview();
                        if (view == null) yield break;
                        // The session consumes model + view together once the pipeline accepts the move.
                        session.ApplyOpponentMove(move.Piece, move.Anchor);
                        break;
                }
            }
        }

        /// <summary>
        /// The "cancel" beat, literally: pick up a DIFFERENT tray piece, carry it over a cell (the shared
        /// preview glows under it, exactly like a real drag), linger, then change its mind — return the
        /// piece to its tray slot the same way a player's cancelled drag does — before reaching for the
        /// piece it actually plays.
        /// </summary>
        private IEnumerator SwapFakeout(OpponentBeat beat, PieceTrayView tray, BoardView board,
            GameSessionController session, Camera cam, Transform layer, Vector3 offset)
        {
            PieceView other = tray.FindViewFor(beat.Piece);
            if (other == null)
            {
                yield break;
            }

            RectTransform rect = other.RectTransform;
            Transform homeParent = other.transform.parent;
            Vector3 homePos = rect.position;

            // A short look at the tray before committing to (the wrong) piece.
            yield return new WaitForSeconds(hoverHesitation * Random.Range(0.4f, 0.8f));
            if (other == null) yield break;

            other.transform.SetParent(layer, true);
            other.transform.SetAsLastSibling();
            other.SetScale(1.2f);
            RerollIdle();
            _previewView = other;
            _previewBoard = board;
            _previewSession = session;
            _previewCam = cam;
            _previewShown = false;

            Vector3 target = CellTargetPos(other, rect, board, beat.Cell, offset);
            yield return MoveTo(rect, target);
            if (rect == null) { _previewView = null; yield break; }
            yield return Hover(rect, target, beat.Seconds);

            // Change of mind: stop the glow and put the piece back exactly where it came from.
            _previewView = null;
            board.ClearPreview();
            if (rect == null) yield break;
            yield return MoveTo(rect, homePos);
            if (rect == null) yield break;
            other.transform.SetParent(homeParent, true);
            other.SetScale(PieceTrayView.TrayScale);
            rect.anchoredPosition = Vector2.zero;
            yield return new WaitForSeconds(0.15f * Random.Range(0.8f, 1.3f));
        }

        /// <summary>
        /// Plays a misdrop on the planned invalid cell: show the red glow there, then bounce the piece
        /// back to its tray slot exactly like a real failed drop (same SFX) before it settles for real.
        /// The planner only emits this beat when a valid invalid-cell exists, so no search is needed here.
        /// </summary>
        private IEnumerator AttemptMisdrop(RectTransform rect, BoardView board, Vector3 offset, Vector3 homePos, Vector2Int wrongCell, GameSessionController session)
        {
            // Positioned by the reference cell so the live preview tick lands its (invalid) red glow on
            // the occupied cell — the shared tick handles the highlight.
            Vector3 wrongPos = CellTargetPos(_previewView, rect, board, wrongCell, offset);
            yield return MoveTo(rect, wrongPos);
            yield return Hover(rect, wrongPos, hoverHesitation * Random.Range(0.5f, 0.9f));

            session.NotifyInvalidDrop();
            yield return MoveTo(rect, homePos);   // bounce back to the tray, same as the player's failed drop
            if (rect == null) yield break;
            rect.localScale = Vector3.one;
            yield return new WaitForSeconds(0.25f * Random.Range(0.8f, 1.3f));
            if (rect == null) yield break;
            rect.localScale = Vector3.one * 1.2f;
        }

        /// <summary>
        /// World position to move the piece to so its REFERENCE cell lands over <paramref name="cell"/>
        /// (with the given lift) — so a live anchor-resolve returns exactly that cell, whatever the
        /// piece's scale or pivot is. This is what keeps the glow, the piece and the eventual placement
        /// all on the same cell instead of the glow shifting to a neighbouring (sometimes invalid) cell.
        /// </summary>
        private static Vector3 CellTargetPos(PieceView view, RectTransform rect, BoardView board, Vector2Int cell, Vector3 lift)
        {
            if (!view.HasReferenceCell)
            {
                return board.GetCellWorldCenter(cell) + lift;
            }

            // Rigid offset from the reference cell to the transform origin at the current scale;
            // measuring it live makes the placement scale-proof.
            Vector3 refToOrigin = rect.position - view.ReferenceCellWorldCenter;
            return board.GetCellWorldCenter(cell + view.ReferenceOffset) + refToOrigin + lift;
        }

        // ---- Additive hand-motion (same GameObject, no separate layer/object) ---------------------
        // Both the travel and the "hand feel" are composited onto the piece's OWN transform: the
        // logical position (travelling / resting) is computed, then this continuously evolving offset
        // is added on top -> rect.position = basePos + IdleOffset(scale). There is no extra cursor or
        // layer object; it's one additive animation on the same GO. The ever-advancing clock means it
        // never resets or snaps — the hand always carries a little life.

        private float _idleClock;
        private float _idAmpX, _idAmpY;
        private float _idFx1, _idFx2, _idFy1, _idFy2;
        private float _idPx1, _idPx2, _idPy1, _idPy2;
        private float _idBreatheFreq, _idBreathePhase;
        private float _idDriftFreq, _idDriftPhase, _idDriftAmp;
        private Vector2 _idDriftDir;

        /// <summary>Re-rolls the idle layer's amplitudes, rhythms and breathing so each rest wobbles differently.</summary>
        private void RerollIdle()
        {
            _idAmpX = bobAmount * Random.Range(0.5f, 1.1f);
            _idAmpY = bobAmount * Random.Range(0.35f, 0.8f);
            _idFx1 = Random.Range(1.6f, 3.2f);
            _idFx2 = Random.Range(3.4f, 6.0f);
            _idFy1 = Random.Range(1.2f, 2.6f);
            _idFy2 = Random.Range(2.8f, 5.2f);
            _idPx1 = Random.value * 6.283f;
            _idPx2 = Random.value * 6.283f;
            _idPy1 = Random.value * 6.283f;
            _idPy2 = Random.value * 6.283f;
            _idBreatheFreq = Random.Range(0.4f, 1.1f);
            _idBreathePhase = Random.value * 6.283f;
            _idDriftFreq = Random.Range(0.25f, 0.8f);
            _idDriftPhase = Random.value * 6.283f;
            _idDriftAmp = bobAmount * Random.Range(0.3f, 0.8f);
            _idDriftDir = Random.insideUnitCircle;
        }

        /// <summary>
        /// Additive idle-hover offset (px), sampled from the ever-advancing <see cref="_idleClock"/>:
        ///   offset = (two sine octaves per axis) × amp × breathe  +  slow drift
        /// The two octaves make the wobble organic (not a single clean sine); the "breathe" term swells
        /// and shrinks the amplitude (distance grows/shrinks) over time; the drift slowly wanders the
        /// centre. <paramref name="scale"/> fades the whole layer down during fast travel.
        /// </summary>
        private Vector3 IdleOffset(float scale)
        {
            float t = _idleClock;
            float breathe = 1f + 0.4f * Mathf.Sin(t * _idBreatheFreq + _idBreathePhase);   // amp swells 0.6..1.4
            float x = (Mathf.Sin(t * _idFx1 + _idPx1) * 0.65f + Mathf.Sin(t * _idFx2 + _idPx2) * 0.35f) * _idAmpX * breathe;
            float y = (Mathf.Cos(t * _idFy1 + _idPy1) * 0.65f + Mathf.Cos(t * _idFy2 + _idPy2) * 0.35f) * _idAmpY * breathe;
            float drift = Mathf.Sin(t * _idDriftFreq + _idDriftPhase) * _idDriftAmp;
            return new Vector3(x + _idDriftDir.x * drift, y + _idDriftDir.y * drift, 0f) * scale;
        }

        /// <summary>
        /// Eased, gently-curved travel (Bézier through a bowed mid-point) with a human-hand speed
        /// profile: the base ease gets a randomized "start–stop" ripple that fades in/out at the ends.
        /// The additive idle layer is mixed on top — faint mid-travel, swelling back in as the hand
        /// slows into the target — so movement stays crisp yet the arrival already carries its life.
        /// </summary>
        private IEnumerator MoveTo(RectTransform rect, Vector3 worldTarget)
        {
            Vector3 start = rect.position;
            float dist = Vector3.Distance(start, worldTarget);
            if (dist < 1f)
            {
                rect.position = worldTarget + IdleOffset(0.6f);
                yield break;
            }

            float duration = Mathf.Clamp(dist / moveSpeed, 0.32f, 1.15f) * Random.Range(0.9f, 1.45f);
            Vector2 bow = Random.insideUnitCircle * dist * pathCurve;
            Vector3 mid = (start + worldTarget) * 0.5f + new Vector3(bow.x, bow.y, 0f);

            // Randomized speed ripple: gives the "başla–dur" (surge then hold back) feel of a real hand.
            float rippleFreq = Random.Range(1.5f, 3.5f);
            float rippleAmp = Random.Range(0.05f, 0.16f);
            float ripplePhase = Random.value * 6.283f;

            float t = 0f;
            while (t < duration)
            {
                // The piece can be destroyed mid-animation if the turn/game is reset (return to menu,
                // replay, new match). Bail cleanly instead of writing to a dead transform.
                if (rect == null)
                {
                    yield break;
                }

                t += Time.deltaTime;
                _idleClock += Time.deltaTime;
                float u = Mathf.Clamp01(t / duration);
                float eased = Mathf.SmoothStep(0f, 1f, u);
                // Envelope is 0 at both ends -> start and finish stay clean; ripple only bends the middle.
                float envelope = Mathf.Sin(u * Mathf.PI);
                float k = Mathf.Clamp01(eased + Mathf.Sin(u * rippleFreq * Mathf.PI + ripplePhase) * rippleAmp * envelope);
                Vector3 a = Vector3.Lerp(start, mid, k);
                Vector3 b = Vector3.Lerp(mid, worldTarget, k);
                Vector3 basePos = Vector3.Lerp(a, b, k);      // quadratic Bézier through the bowed mid-point
                // Idle layer fades out mid-travel (envelope high) and back in near the ends (envelope low).
                rect.position = basePos + IdleOffset(0.25f + 0.35f * (1f - envelope));
                TickPreview();                                // glow tracks the piece while it travels
                yield return null;
            }

            if (rect == null)
            {
                yield break;
            }

            rect.position = worldTarget + IdleOffset(0.6f);
            TickPreview();
        }

        /// <summary>
        /// Holds near a cell while the additive idle layer (re-rolled for this rest) drives a soft,
        /// breathing wobble on top of the fixed centre — like a hand resting on a mouse, never frozen.
        /// </summary>
        private IEnumerator Hover(RectTransform rect, Vector3 center, float seconds)
        {
            RerollIdle();
            float t = 0f;
            while (t < seconds)
            {
                // Bail if the piece was destroyed mid-hover (turn/game reset) — never touch a dead rect.
                if (rect == null)
                {
                    yield break;
                }

                t += Time.deltaTime;
                _idleClock += Time.deltaTime;
                rect.position = center + IdleOffset(1f);
                TickPreview();                                // glow keeps tracking while it hovers
                yield return null;
            }
        }

    }
}
