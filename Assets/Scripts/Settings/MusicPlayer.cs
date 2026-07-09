using UnityEngine;

namespace NumbersBlast.Settings
{
    /// <summary>
    /// Loops one background-music clip, gated by the Music toggle in GameSettings. A missing clip is
    /// simply not played, so the game runs fine without the asset.
    /// </summary>
    public sealed class MusicPlayer : MonoBehaviour
    {
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip musicClip;
        [SerializeField, Range(0f, 1f)] private float volume = 0.45f;

        private GameSettings _settings;

        public void Bind(GameSettings settings)
        {
            _settings = settings;
            if (_settings != null)
            {
                _settings.Changed += Apply;
            }

            if (audioSource != null)
            {
                audioSource.clip = musicClip;
                audioSource.loop = true;
                audioSource.playOnAwake = false;
                audioSource.volume = volume;
            }

            Apply();
        }

        private void OnDestroy()
        {
            if (_settings != null)
            {
                _settings.Changed -= Apply;
            }
        }

        // Called on any settings change; re-reads the Music toggle and plays/pauses accordingly.
        private void Apply()
        {
            if (audioSource == null || musicClip == null)
            {
                return;
            }

            bool on = _settings == null || _settings.BgmEnabled;
            if (on)
            {
                if (!audioSource.isPlaying)
                {
                    audioSource.Play();
                }
            }
            else if (audioSource.isPlaying)
            {
                audioSource.Pause();
            }
        }
    }
}
