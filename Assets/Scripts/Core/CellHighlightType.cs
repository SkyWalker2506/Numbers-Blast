namespace NumbersBlast.Core
{
    /// <summary>
    /// Visual highlight states a board cell can show during drag preview and resolution.
    /// </summary>
    public enum CellHighlightType
    {
        None,
        ValidPlacement,
        InvalidPlacement,
        MergeSource,
        LineClear
    }
}
