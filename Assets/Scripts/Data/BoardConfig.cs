using UnityEngine;

namespace NumbersBlast.Data
{
    [CreateAssetMenu(menuName = "Numbers Blast/Board Config", fileName = "BoardConfig")]
    public sealed class BoardConfig : ScriptableObject
    {
        [SerializeField] private int width = 8;
        [SerializeField] private int height = 8;
        [SerializeField] private int trayPieceCount = 3;
        [SerializeField] private float cellSize = 96f;
        [Tooltip("Fraction of the current score lost on a turn timeout (Part 2). 0.05 = 5%.")]
        [SerializeField] private float timeoutPenaltyPercent = 0.05f;
        [SerializeField] private Color emptyCellColor = new Color(0.12f, 0.14f, 0.20f, 1f);

        [Tooltip("Colors for cell values. Index 0 = value 1. Values above the array length clamp to the last color.")]
        [SerializeField] private Color[] valueColors;

        public int Width => width;
        public int Height => height;
        public int TrayPieceCount => trayPieceCount;
        public float CellSize => cellSize;
        public float TimeoutPenaltyPercent => timeoutPenaltyPercent;
        public Color EmptyCellColor => emptyCellColor;

        /// <summary>
        /// Maps a cell value to a color. Values 1..N use the authored palette. Merge can push values
        /// well past the palette (6, 12, 24...); rather than clamping every high value to one flat
        /// colour, those derive a stable, distinct colour via a golden-ratio hue walk, with S/V bands
        /// tuned so the white value text stays readable.
        /// </summary>
        public Color GetColorForValue(int value)
        {
            if (valueColors == null || valueColors.Length == 0)
            {
                return Color.white;
            }

            if (value >= 1 && value <= valueColors.Length)
            {
                return valueColors[value - 1];
            }

            // Golden-ratio hue rotation gives well-spread, non-repeating hues per step past the palette;
            // it's deterministic (same value → same colour every time), so merges stay visually stable.
            int steps = Mathf.Max(1, value - valueColors.Length);
            float hue = (steps * 0.6180339887f) % 1f;
            return Color.HSVToRGB(hue, 0.62f, 0.80f);
        }

        private void OnValidate()
        {
            width = Mathf.Clamp(width, 4, 12);
            height = Mathf.Clamp(height, 4, 12);
            trayPieceCount = Mathf.Clamp(trayPieceCount, 1, 3);   // the tray layout has 3 fixed slots
            cellSize = Mathf.Max(16f, cellSize);
            timeoutPenaltyPercent = Mathf.Clamp01(timeoutPenaltyPercent);
        }
    }
}
