using UnityEngine;
using NumbersBlast.Core;

namespace NumbersBlast.Settings
{
    /// <summary>
    /// Plays short gameplay SFX and device vibration. Missing clips are simply skipped, so the game
    /// runs fine even before audio assets are imported. SFX respects the SFX toggle; vibration
    /// (device haptics, real devices only — a no-op in the Editor) respects the Vibration toggle
    /// independently, on the same impactful moments (merge / clear / invalid).
    /// </summary>
    public sealed class SfxPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip placeClip;
        [SerializeField] private AudioClip mergeClip;
        [SerializeField] private AudioClip clearClip;
        [SerializeField] private AudioClip invalidClip;
        [SerializeField] private AudioClip gameOverClip;

        private GameSettings _settings;

        public void Bind(GameSettings settings)
        {
            _settings = settings;
        }

        public void PlayPlace() => Play(placeClip);

        public void PlayMerge()
        {
            Play(mergeClip);
            Vibrate();
        }

        public void PlayClear()
        {
            Play(clearClip);
            Vibrate();
        }

        public void PlayInvalid()
        {
            Play(invalidClip);
            Vibrate();
        }

        public void PlayGameOver() => Play(gameOverClip);

        /// <summary>The clear/merge/place sound a resolved move deserves (clear wins over merge).</summary>
        public void PlayFor(MoveResult result)
        {
            if (result.HasLineClear)
            {
                PlayClear();
            }
            else if (result.HasMerge)
            {
                PlayMerge();
            }
            else
            {
                PlayPlace();
            }
        }

        private void Play(AudioClip clip)
        {
            if (clip == null || audioSource == null)
            {
                return;
            }

            if (_settings != null && !_settings.SfxEnabled)
            {
                return;
            }

            audioSource.PlayOneShot(clip);
        }

        private void Vibrate()
        {
            // Handheld only exists in mobile player assemblies — an unguarded call is a compile
            // error the moment a standalone/WebGL build target is added.
#if UNITY_ANDROID || UNITY_IOS
            if (_settings != null && _settings.VibrationEnabled)
            {
                Handheld.Vibrate();
            }
#endif
        }
    }
}
