using System;
using System.Collections.Generic;
using UnityEngine;

namespace NumbersBlast.Data
{
    /// <summary>
    /// A single interactive tutorial step. Board and piece presets are inlined here as
    /// serializable structs instead of separate ScriptableObjects to keep the data footprint small.
    /// </summary>
    [CreateAssetMenu(menuName = "Numbers Blast/Tutorial Step", fileName = "TutorialStep")]
    public sealed class TutorialStepDefinition : ScriptableObject
    {
        [SerializeField] [TextArea] private string instructionText;
        [SerializeField] private BoardCellPreset[] boardCells;
        [SerializeField] private TutorialPiecePreset[] trayPieces;
        [SerializeField] private Vector2Int[] allowedAnchors;
        [SerializeField] private Vector2Int[] highlightedCells;

        public string InstructionText => instructionText;
        public IReadOnlyList<BoardCellPreset> BoardCells => boardCells;
        public IReadOnlyList<TutorialPiecePreset> TrayPieces => trayPieces;
        public IReadOnlyList<Vector2Int> AllowedAnchors => allowedAnchors;
        public IReadOnlyList<Vector2Int> HighlightedCells => highlightedCells;
    }

    [Serializable]
    public struct BoardCellPreset
    {
        public Vector2Int Position;
        public int Value;
    }

    [Serializable]
    public struct TutorialPiecePreset
    {
        public PieceCellPreset[] Cells;
    }

    [Serializable]
    public struct PieceCellPreset
    {
        public Vector2Int Offset;
        public int Value;
    }
}
