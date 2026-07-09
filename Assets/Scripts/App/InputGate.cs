using NumbersBlast.Input;

namespace NumbersBlast.App
{
    /// <summary>
    /// Tracks whether player input is locked (Settings open) and which drag is live, so a lock or a
    /// timeout can cancel it instantly. Kept separate from GameState on purpose: locking never stomps
    /// the true game/resolve/turn state. Plain class owned by <see cref="GameSessionController"/>.
    /// </summary>
    internal sealed class InputGate
    {
        private PieceDragController _activeDrag;

        public bool LockedBySettings { get; private set; }

        public void SetLocked(bool locked)
        {
            LockedBySettings = locked;
            if (locked)
            {
                // A drag still in progress when input locks (e.g. a second finger opens Settings
                // mid-drag) must be released instantly, not left running under the panel.
                CancelActiveDrag();
            }
        }

        /// <summary>Defensive clear for teardown paths that bypass the Closed event.</summary>
        public void Unlock()
        {
            LockedBySettings = false;
        }

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
    }
}
