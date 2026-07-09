using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using NumbersBlast.Core;
using NumbersBlast.Data;
using NumbersBlast.App;
using NumbersBlast.Gameplay;
using NumbersBlast.UI;

namespace NumbersBlast.Tutorial
{
    /// <summary>
    /// Drives the three forced solo tutorial steps (placement, merge, line clear).
    /// It never touches board math directly — it asks the session to load a preset step and
    /// gates which anchors are allowed. Wrong placements are rejected and do not advance.
    /// </summary>
    public sealed class TutorialController : MonoBehaviour
    {
        [SerializeField] private TutorialStepDefinition[] steps;
        [SerializeField] private TutorialOverlay overlay;
        [Tooltip("Seconds the successful step result stays on screen before the next step loads.")]
        [SerializeField] private float successHoldSeconds = 1.8f;

        private static readonly string[] PraiseMessages = { "Nice!", "Great!", "Perfect!" };

        private GameSessionController _session;
        private int _index = -1;

        public bool IsRunning { get; private set; }

        public bool HasSteps => steps != null && steps.Length > 0;

        /// <summary>Raised once all tutorial steps are completed; the session then starts a real run.</summary>
        public event Action Completed;

        public void Initialize(GameSessionController session)
        {
            _session = session;
        }

        public void StartTutorial()
        {
            if (steps == null || steps.Length == 0)
            {
                IsRunning = false;
                Completed?.Invoke();
                return;
            }

            IsRunning = true;
            _index = 0;
            LoadCurrentStep();
        }

        /// <summary>
        /// Aborts the tutorial (e.g. when leaving to the main menu or starting a non-tutorial run) so
        /// its move-gating never leaks into a normal game. Does not raise <see cref="Completed"/>.
        /// </summary>
        public void StopTutorial()
        {
            StopAllCoroutines();
            IsRunning = false;
            _index = -1;
            if (overlay != null)
            {
                overlay.HideInstruction();
            }
        }

        public bool IsMoveAllowed(PieceInstance piece, Vector2Int anchor)
        {
            if (!IsRunning || _index < 0 || _index >= steps.Length)
            {
                return true;
            }

            IReadOnlyList<Vector2Int> allowed = steps[_index].AllowedAnchors;
            if (allowed == null || allowed.Count == 0)
            {
                return true;
            }

            foreach (Vector2Int a in allowed)
            {
                if (a == anchor)
                {
                    return true;
                }
            }

            return false;
        }

        public void NotifyMoveCompleted(MoveResult result)
        {
            if (!IsRunning)
            {
                return;
            }

            // Hold the successful result on screen for a moment (with a short praise) so the player
            // sees the merge/clear happen before the next step resets the board.
            StartCoroutine(AdvanceAfterHold());
        }

        private IEnumerator AdvanceAfterHold()
        {
            if (overlay != null)
            {
                overlay.ShowInstruction(PraiseMessages[Mathf.Clamp(_index, 0, PraiseMessages.Length - 1)]);
            }

            yield return new WaitForSeconds(successHoldSeconds);

            _index++;
            if (_index >= steps.Length)
            {
                IsRunning = false;
                if (overlay != null)
                {
                    overlay.HideInstruction();
                }

                Completed?.Invoke();
                yield break;
            }

            LoadCurrentStep();
        }

        private void LoadCurrentStep()
        {
            TutorialStepDefinition step = steps[_index];
            _session.ApplyTutorialStep(step);

            if (overlay != null)
            {
                overlay.ShowInstruction(step.InstructionText);
            }
        }
    }
}
