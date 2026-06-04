namespace BlockJam.Data
{
    /// <summary>
    /// Identity of a unit. Three of the same color collected in the slot bar match and clear.
    /// Kept as an explicit enum (instead of int) so level data is human-readable in the
    /// inspector and the custom level editor, and so the compiler catches typos.
    /// Add new colors at the END to keep previously-authored level data stable
    /// (enum values are serialized by their integer index).
    /// </summary>
    public enum UnitColor
    {
        Red = 0,
        Green = 1,
        Blue = 2,
        Yellow = 3,
        Purple = 4,
        Orange = 5
    }
}
