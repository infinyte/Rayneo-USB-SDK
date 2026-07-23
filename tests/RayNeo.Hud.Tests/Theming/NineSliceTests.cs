// -----------------------------------------------------------------------------
// NineSliceTests.cs
// Author: Kurt Mitchell
//
// Nine-slice source-rectangle math: the nine regions tile the image exactly,
// the manifest-slice overload rounds to whole pixels, and invalid margins
// (negative, or exceeding the image size) are rejected.
// -----------------------------------------------------------------------------

using System;
using System.Windows;
using Infinyte.RayNeo.Hud.Theming;

namespace RayNeo.Hud.Tests;

public sealed class NineSliceTests
{
    [Fact]
    public void ComputesTilingRegions()
    {
        NineSliceRects r = NineSlice.Compute(100, 80, 20, 10, 20, 10);

        Assert.Equal(new Int32Rect(0, 0, 20, 10), r.TopLeft);
        Assert.Equal(new Int32Rect(20, 0, 60, 10), r.TopCenter);
        Assert.Equal(new Int32Rect(80, 0, 20, 10), r.TopRight);
        Assert.Equal(new Int32Rect(0, 10, 20, 60), r.MidLeft);
        Assert.Equal(new Int32Rect(20, 10, 60, 60), r.Center);
        Assert.Equal(new Int32Rect(80, 10, 20, 60), r.MidRight);
        Assert.Equal(new Int32Rect(0, 70, 20, 10), r.BottomLeft);
        Assert.Equal(new Int32Rect(20, 70, 60, 10), r.BottomCenter);
        Assert.Equal(new Int32Rect(80, 70, 20, 10), r.BottomRight);
    }

    [Fact]
    public void SliceOverloadRoundsToWholePixels()
    {
        var slice = new HudThemeSlice { Left = 20.2, Top = 10.0, Right = 19.8, Bottom = 10.0 };
        NineSliceRects r = NineSlice.Compute(100, 80, slice);

        // 20.2 -> 20 and 19.8 -> 20, so the center spans 100 - 20 - 20 = 60.
        Assert.Equal(new Int32Rect(20, 10, 60, 60), r.Center);
    }

    [Fact]
    public void RejectsMarginsWiderThanImage()
    {
        Assert.Throws<ArgumentException>(() => NineSlice.Compute(40, 40, 20, 0, 20, 0));
    }

    [Fact]
    public void RejectsNegativeMargins()
    {
        Assert.Throws<ArgumentException>(() => NineSlice.Compute(100, 80, -1, 10, 20, 10));
    }
}
