// -----------------------------------------------------------------------------
// NineSlicePanel.cs
// Author: Kurt Mitchell
//
// Builds a WPF visual that renders a bitmap as a nine-slice (nine-patch): the
// four corners are drawn at their native size in fixed grid cells while the
// edges and center stretch to fill the requested width/height. This lets a
// single small panel PNG scale to any size without distorting its rounded
// corners or border. The source-rectangle math lives in NineSlice; this file is
// only the WPF assembly of it.
// -----------------------------------------------------------------------------

using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace Infinyte.RayNeo.Hud.Theming;

/// <summary>Assembles a nine-slice bitmap into a sized WPF visual.</summary>
public static class NineSlicePanel
{
    /// <summary>
    /// Creates a <see cref="Grid"/> that renders <paramref name="source"/> as a
    /// nine-slice at the given <paramref name="width"/> and <paramref name="height"/>.
    /// </summary>
    /// <exception cref="ArgumentException">Thrown if the slice margins are invalid for the image size.</exception>
    public static FrameworkElement Create(BitmapSource source, HudThemeSlice slice, double width, double height)
    {
        NineSliceRects rects = NineSlice.Compute(source.PixelWidth, source.PixelHeight, slice);

        double left = Math.Round(slice.Left);
        double top = Math.Round(slice.Top);
        double right = Math.Round(slice.Right);
        double bottom = Math.Round(slice.Bottom);

        var grid = new Grid
        {
            Width = width,
            Height = height,
            IsHitTestVisible = false,
            SnapsToDevicePixels = true,
        };

        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(left) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
        grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(right) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(top) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
        grid.RowDefinitions.Add(new RowDefinition { Height = new GridLength(bottom) });

        AddCell(grid, source, rects.TopLeft, 0, 0);
        AddCell(grid, source, rects.TopCenter, 0, 1);
        AddCell(grid, source, rects.TopRight, 0, 2);
        AddCell(grid, source, rects.MidLeft, 1, 0);
        AddCell(grid, source, rects.Center, 1, 1);
        AddCell(grid, source, rects.MidRight, 1, 2);
        AddCell(grid, source, rects.BottomLeft, 2, 0);
        AddCell(grid, source, rects.BottomCenter, 2, 1);
        AddCell(grid, source, rects.BottomRight, 2, 2);

        return grid;
    }

    private static void AddCell(Grid grid, BitmapSource source, Int32Rect rect, int row, int column)
    {
        // A zero-width or zero-height slice (e.g. a zero margin) has no pixels.
        if (rect.Width <= 0 || rect.Height <= 0)
        {
            return;
        }

        var image = new Image
        {
            Source = new CroppedBitmap(source, rect),
            Stretch = Stretch.Fill,
            IsHitTestVisible = false,
        };
        Grid.SetRow(image, row);
        Grid.SetColumn(image, column);
        grid.Children.Add(image);
    }
}
