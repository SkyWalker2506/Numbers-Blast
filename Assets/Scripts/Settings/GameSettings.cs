using System;

namespace NumbersBlast.Settings
{
    /// <summary>
    /// Minimal in-session settings. Plain C# — no save/load, values reset each launch.
    /// </summary>
    public sealed class GameSettings
    {
        public bool SfxEnabled { get; private set; } = true;
        public bool BgmEnabled { get; private set; } = true;
        public bool VibrationEnabled { get; private set; } = true;

        public event Action Changed;

        public void SetSfxEnabled(bool enabled)
        {
            SfxEnabled = enabled;
            Changed?.Invoke();
        }

        public void SetBgmEnabled(bool enabled)
        {
            BgmEnabled = enabled;
            Changed?.Invoke();
        }

        public void SetVibrationEnabled(bool enabled)
        {
            VibrationEnabled = enabled;
            Changed?.Invoke();
        }
    }
}
