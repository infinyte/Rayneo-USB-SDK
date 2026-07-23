// -----------------------------------------------------------------------------
// NineSlice.cs
// Author: Kurt Mitchell
//
// Pure geometry for nine-slice (nine-patch) scaling: given a source image size
// and its four slice margins, compute the nine source rectangles. Corners stay
// crisp; edges stretch along one axis; the center stretches both. Kept free of
// any WPF control so the math is unit-testable on its own.
// -----------------------------------------------------------------------------

using System;
using System.Windows;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>The nine source rectangles of a sliced image, row-major from top-left.</summary>
public readonly record struct NineSliceRects(
    Int32Rect TopLeft, Int32Rect TopCenter, Int32Rect TopRight,
    Int32Rect MidLeft, Int32Rect Center, Int32Rect MidRight,
    Int32Rect BottomLeft, Int32Rect BottomCenter, Int32Rect BottomRight);

/// <summary>Computes nine-slice source rectangles.</summary>
public static class NineSlice
{
    /// <summary>Computes the nine source rectangles for the given image size and margins.</summary>
    /// <exception cref="ArgumentException">
    /// Thrown if any margin is negative, or if the left+right (or top+bottom)
    /// margins leave no room for a center region.
    /// </exception>
    public static NineSliceRects Compute(int pixelWidth, int pixelHeight, int left, int top, int right, int bottom)
    {
        if (left < 0 || top < 0 || right < 0 || bottom < 0)
        {
            throw new ArgumentException("Nine-slice margins must be non-negative.");
        }
        if (left + right >= pixelWidth)
        {
            throw new ArgumentException(
                $"Nine-slice horizontal margins ({left}+{right}) must be smaller than the image width ({pixelWidth}).");
        }
        if (top + bottom >= pixelHeight)
        {
            throw new ArgumentException(
                $"Nine-slice vertical margins ({top}+{bottom}) must be smaller than the image height ({pixelHeight}).");
        }

        int centerWidth = pixelWidth - left - right;
        int centerHeight = pixelHeight - top - bottom;

        int x0 = 0;
        int x1 = left;
        int x2 = pixelWidth - right;
        int y0 = 0;
        int y1 = top;
        int y2 = pixelHeight - bottom;

        return new NineSliceRects(
            new Int32Rect(x0, y0, left, top),
            new Int32Rect(x1, y0, centerWidth, top),
            new Int32Rect(x2, y0, right, top),
            new Int32Rect(x0, y1, left, centerHeight),
            new Int32Rect(x1, y1, centerWidth, centerHeight),
            new Int32Rect(x2, y1, right, centerHeight),
            new Int32Rect(x0, y2, left, bottom),
            new Int32Rect(x1, y2, centerWidth, bottom),
            new Int32Rect(x2, y2, right, bottom));
    }

    /// <summary>Computes the nine source rectangles from a manifest slice (rounded to pixels).</summary>
    public static NineSliceRects Compute(int pixelWidth, int pixelHeight, HudThemeSlice slice) =>
        Compute(
            pixelWidth, pixelHeight,
            (int)Math.Round(slice.Left), (int)Math.Round(slice.Top),
            (int)Math.Round(slice.Right), (int)Math.Round(slice.Bottom));
}
