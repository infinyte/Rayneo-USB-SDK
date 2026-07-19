// -----------------------------------------------------------------------------
// DisplayInfo.cs
// Author: Kurt Mitchell
// -----------------------------------------------------------------------------

namespace Infinyte.RayNeo.Hud.Display;

/// <summary>A physical monitor: index, bounds in physical pixels, primary flag.</summary>
public sealed record DisplayInfo(int Index, int Left, int Top, int Width, int Height, bool IsPrimary)
{
    /// <inheritdoc/>
    public override string ToString() =>
        $"#{Index} {Width}x{Height} @ ({Left},{Top}){(IsPrimary ? " [primary]" : string.Empty)}";
}
