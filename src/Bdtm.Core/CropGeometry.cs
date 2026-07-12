namespace Bdtm.Core;

/// <summary>
/// Integer crop rectangle in image or screen space (no System.Drawing).
/// </summary>
public readonly struct CropRect
{
    public int X { get; init; }
    public int Y { get; init; }
    public int Width { get; init; }
    public int Height { get; init; }

    public int Right => X + Width;
    public int Bottom => Y + Height;

    public bool IsEmpty => Width <= 0 || Height <= 0;

    public bool Contains(int x, int y) =>
        x >= X && x < Right && y >= Y && y < Bottom;

    public static CropRect Intersect(CropRect a, CropRect b)
    {
        int x = Math.Max(a.X, b.X);
        int y = Math.Max(a.Y, b.Y);
        int right = Math.Min(a.Right, b.Right);
        int bottom = Math.Min(a.Bottom, b.Bottom);
        if (right <= x || bottom <= y)
            return default;
        return new CropRect { X = x, Y = y, Width = right - x, Height = bottom - y };
    }
}

/// <summary>
/// A user-defined crop region with display color (ARGB packed as uint).
/// </summary>
public sealed class CropRegion
{
    public CropRect Bounds { get; set; }
    public int Index { get; set; }
    public uint DisplayColorArgb { get; set; }
}

/// <summary>
/// Port of WinForms <c>CropCanvasHelper</c> without System.Drawing.
/// Coordinates use integer image/viewport sizes and screen points.
/// </summary>
public static class CropCanvasHelper
{
    /// <summary>
    /// Region overlay colors as ARGB uint, matching original FromArgb(A,R,G,B) values.
    /// </summary>
    public static readonly uint[] RegionColors =
    {
        0xDCE74C3C, // 220, 231, 76, 60
        0xDC2ECC71, // 220, 46, 204, 113
        0xDC3498DB, // 220, 52, 152, 219
        0xDCF1C40F, // 220, 241, 196, 15
        0xDC9B59B6, // 220, 155, 89, 182
        0xDCE67E22, // 220, 230, 126, 34
    };

    public static float CalcZoomMod(int imageW, int imageH, int viewW, int viewH)
    {
        if (imageW <= 0 || imageH <= 0)
            return 1f;

        float mod = (float)viewH / imageH;
        if ((int)(mod * imageW) > viewW)
            mod = (float)viewW / imageW;
        return mod;
    }

    public static CropRect CalcImageLocation(int imageW, int imageH, int viewW, int viewH)
    {
        float mod = CalcZoomMod(imageW, imageH, viewW, viewH);
        int w = (int)(mod * imageW);
        int h = (int)(mod * imageH);
        int x = w == viewW ? 0 : (viewW - w) / 2;
        int y = h == viewH ? 0 : (viewH - h) / 2;
        return new CropRect { X = x, Y = y, Width = w, Height = h };
    }

    public static CropRect ScreenRectToImageRect(
        CropRect screenRect,
        int imageW,
        int imageH,
        int viewW,
        int viewH)
    {
        CropRect imgLocation = CalcImageLocation(imageW, imageH, viewW, viewH);
        CropRect inter = CropRect.Intersect(imgLocation, screenRect);
        if (inter.IsEmpty)
            return default;

        float mod = CalcZoomMod(imageW, imageH, viewW, viewH);
        return new CropRect
        {
            X = (int)((inter.X - imgLocation.X) / mod),
            Y = (int)((inter.Y - imgLocation.Y) / mod),
            Width = Math.Max(1, (int)(inter.Width / mod)),
            Height = Math.Max(1, (int)(inter.Height / mod)),
        };
    }

    public static CropRect ImageRectToScreenRect(
        CropRect imageRect,
        int imageW,
        int imageH,
        int viewW,
        int viewH)
    {
        CropRect imgLocation = CalcImageLocation(imageW, imageH, viewW, viewH);
        float mod = CalcZoomMod(imageW, imageH, viewW, viewH);
        return new CropRect
        {
            X = imgLocation.X + (int)(imageRect.X * mod),
            Y = imgLocation.Y + (int)(imageRect.Y * mod),
            Width = Math.Max(1, (int)(imageRect.Width * mod)),
            Height = Math.Max(1, (int)(imageRect.Height * mod)),
        };
    }

    public static CropRect NormalizeDragRectangle(int x1, int y1, int x2, int y2)
    {
        int x = Math.Min(x1, x2);
        int y = Math.Min(y1, y2);
        return new CropRect
        {
            X = x,
            Y = y,
            Width = Math.Abs(x2 - x1),
            Height = Math.Abs(y2 - y1),
        };
    }

    public static (int X, int Y) ScreenPointToImagePoint(
        int screenX,
        int screenY,
        int imageW,
        int imageH,
        int viewW,
        int viewH)
    {
        CropRect imgLocation = CalcImageLocation(imageW, imageH, viewW, viewH);
        float mod = CalcZoomMod(imageW, imageH, viewW, viewH);
        return (
            (int)((screenX - imgLocation.X) / mod),
            (int)((screenY - imgLocation.Y) / mod));
    }

    public static CropRegion? HitTest(
        IReadOnlyList<CropRegion> regions,
        int screenX,
        int screenY,
        int imageW,
        int imageH,
        int viewW,
        int viewH)
    {
        (int imageX, int imageY) = ScreenPointToImagePoint(screenX, screenY, imageW, imageH, viewW, viewH);
        for (int i = regions.Count - 1; i >= 0; i--)
        {
            if (regions[i].Bounds.Contains(imageX, imageY))
                return regions[i];
        }

        return null;
    }

    public static CropRect ClampToImage(CropRect bounds, int imageW, int imageH)
    {
        int x = Math.Max(0, bounds.X);
        int y = Math.Max(0, bounds.Y);
        int right = Math.Min(imageW, bounds.Right);
        int bottom = Math.Min(imageH, bounds.Bottom);
        return new CropRect
        {
            X = x,
            Y = y,
            Width = Math.Max(0, right - x),
            Height = Math.Max(0, bottom - y),
        };
    }
}
