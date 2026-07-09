using System;
using UnityEngine;

namespace NumbersBlast.Opponent
{
    /// <summary>
    /// Simple countdown timer for the player's turn. Deliberately unpausable: like a real online
    /// match, nothing (not even the settings panel) stops the clock once a turn starts.
    /// </summary>
    public sealed class TurnTimer : MonoBehaviour
    {
        private float _remaining;
        private float _total;
        private bool _running;

        public event Action<float, float> Ticked;
        public event Action TimedOut;

        public void StartCountdown(float duration)
        {
            _total = duration;
            _remaining = duration;
            _running = true;
            Ticked?.Invoke(_remaining, _total);
        }

        public void Stop()
        {
            _running = false;
        }

        private void Update()
        {
            if (!_running)
            {
                return;
            }

            _remaining -= Time.deltaTime;
            if (_remaining <= 0f)
            {
                _remaining = 0f;
                _running = false;
                Ticked?.Invoke(0f, _total);
                TimedOut?.Invoke();
                return;
            }

            Ticked?.Invoke(_remaining, _total);
        }
    }
}
