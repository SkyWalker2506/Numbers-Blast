using UnityEngine;
using UnityEngine.EventSystems;
using NumbersBlast.Core;
using NumbersBlast.App;
using NumbersBlast.Gameplay;
using NumbersBlast.Presentation;

namespace NumbersBlast.Input
{
    /// <summary>
    /// Handles pointer drag for a single tray piece using UGUI pointer interfaces (mouse + touch).
    /// The piece stays under the pointer from the exact point it was grabbed (plus an optional small
    /// lift for touch), and the board anchor is derived from the piece's own footprint so the
    /// highlight always sits directly under the piece. All placement goes through the shared pipeline.
    /// </summary>
    public sealed class PieceDragController : MonoBehaviour,
        IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        [SerializeField] private float pickupScale = 1.1f;
        [Tooltip("Upward lift while dragging (px) so the held piece sits above the finger/pointer and " +
            "the target cells stay visible — kept under half a cell so the resolved anchor is unchanged.")]
        [SerializeField] private float dragYOffset = 45f;
        [SerializeField] private float returnSpeed = 18f;

        private GameSessionController _session;
        private BoardView _boardView;
        private PieceView _pieceView;
        private RectTransform _homeSlot;
        private RectTransform _rect;
        private Transform _originalParent;
        private Canvas _canvas;

        private bool _dragging;
        private Vector2Int _lastAnchor;
        private bool _hasAnchor;

        // Pointer-to-piece offset captured at grab time, so the piece tracks the pointer from the
        // exact spot the player grabbed it instead of snapping its pivot onto the pointer.
        private Vector3 _grabOffset;

        // Previous-frame preview state so the (allocating) move pipeline only runs when the
        // resolved anchor actually changes — no per-frame GC while the pointer stays in a cell.
        private Vector2Int _previewAnchor;
        private bool _previewShown;

        // Tracks the "snap back to slot" tween so a timeout-cancel can stop it and release the piece
        // instantly (before the opponent picks up the same tray piece).
        private Coroutine _snapRoutine;

        public void Initialize(GameSessionController session, BoardView boardView, PieceView pieceView, RectTransform homeSlot)
        {
            _session = session;
            _boardView = boardView;
            _pieceView = pieceView;
            _homeSlot = homeSlot;
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_session == null || !_session.CanAcceptPlayerInput)
            {
                return;
            }

            _dragging = true;
            _previewShown = false;
            // A fresh grab starts with no target: without this, the anchor left over from a previous
            // invalid drop survives, and a drag-less TAP (down+up, OnDrag never fires) would submit
            // the piece to that stale cell if the board has changed since.
            _hasAnchor = false;
            _session.SetActiveDrag(this);
            _originalParent = transform.parent;
            transform.SetParent(_canvas != null ? _canvas.transform : transform.root, true);
            transform.SetAsLastSibling();
            _pieceView.SetScale(pickupScale);
            _pieceView.SetRaycastTarget(false);

            // Remember where on the piece the player grabbed, so drag carries it from that point.
            _grabOffset = _rect.position - ScreenToWorld(eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            Vector3 world = ScreenToWorld(eventData);
            _rect.position = world + _grabOffset + new Vector3(0f, dragYOffset, 0f);

            // One shared per-frame preview step (identical for the AI opponent) — resolves from the
            // piece's live position and only redraws on cell change.
            _hasAnchor = PiecePlacementPreview.Tick(_pieceView, _boardView, _session, eventData.pressEventCamera,
                ref _previewAnchor, ref _previewShown, out Vector2Int anchor);
            if (_hasAnchor)
            {
                _lastAnchor = anchor;
            }
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _session.ClearActiveDrag(this);
            _boardView.ClearPreview();

            if (_hasAnchor && _session.CanSubmitMove(_pieceView.Piece, _lastAnchor))
            {
                // The session consumes the model AND the view together once the pipeline accepts the
                // move — this controller (and the AI path) never pre-consumes.
                _session.ApplyPlayerMove(_pieceView.Piece, _lastAnchor);
            }
            else
            {
                _hasAnchor = false;   // never let a failed target survive into the next interaction
                _session.NotifyInvalidDrop();
                ReturnToSlot();
            }
        }

        /// <summary>
        /// Cancels an in-progress drag (e.g. the Part 2 turn timeout) and returns the piece to its
        /// tray slot **instantly** — no snap tween. By design, not luck: the opponent picks up the same
        /// tray piece right after a timeout, so the piece must be fully released this frame rather than
        /// still animating home when two things could move the same transform.
        /// </summary>
        public void CancelDrag()
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _hasAnchor = false;
            _boardView.ClearPreview();
            ReturnToSlot(animate: false);
        }

        private void ReturnToSlot(bool animate = true)
        {
            if (_snapRoutine != null)
            {
                StopCoroutine(_snapRoutine);
                _snapRoutine = null;
            }

            _pieceView.SetScale(NumbersBlast.Presentation.PieceTrayView.TrayScale);
            _pieceView.SetRaycastTarget(true);
            transform.SetParent(_homeSlot != null ? _homeSlot : _originalParent, true);

            if (animate)
            {
                _snapRoutine = StartCoroutine(SnapHome());
            }
            else
            {
                _rect.anchoredPosition = Vector2.zero;   // instant release
            }
        }

        private System.Collections.IEnumerator SnapHome()
        {
            while ((_rect.anchoredPosition - Vector2.zero).sqrMagnitude > 1f)
            {
                _rect.anchoredPosition = Vector2.Lerp(_rect.anchoredPosition, Vector2.zero, Time.deltaTime * returnSpeed);
                yield return null;
            }

            _rect.anchoredPosition = Vector2.zero;
            _snapRoutine = null;
        }

        private Vector3 ScreenToWorld(PointerEventData eventData)
        {
            if (_canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay)
            {
                RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    (RectTransform)_canvas.transform, eventData.position, eventData.pressEventCamera, out Vector3 world);
                return world;
            }

            return new Vector3(eventData.position.x, eventData.position.y, 0f);
        }
    }
}
