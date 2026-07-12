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

    /// <summary>
    /// Build a drag rectangle from start→end that keeps width:height = aspectW:aspectH.
    /// Anchor is the drag start corner; size grows with the dominant mouse axis.
    /// Pass aspectW/aspectH ≤ 0 for free (unconstrained) drag.
    /// </summary>
    public static CropRect NormalizeDragRectangleWithAspect(
        int x1,
        int y1,
        int x2,
        int y2,
        double aspectW,
        double aspectH)
    {
        if (aspectW <= 0 || aspectH <= 0)
            return NormalizeDragRectangle(x1, y1, x2, y2);

        int dx = x2 - x1;
        int dy = y2 - y1;
        double adx = Math.Abs(dx);
        double ady = Math.Abs(dy);
        if (adx < 0.5 && ady < 0.5)
            return default;

        double targetRatio = aspectW / aspectH; // width / height
        double width;
        double height;
        if (ady < 0.5 || adx / Math.Max(ady, 0.5) > targetRatio)
        {
            width = Math.Max(adx, 1);
            height = width / targetRatio;
        }
        else
        {
            height = Math.Max(ady, 1);
            width = height * targetRatio;
        }

        int w = Math.Max(1, (int)Math.Round(width));
        int h = Math.Max(1, (int)Math.Round(height));
        int x = dx >= 0 ? x1 : x1 - w;
        int y = dy >= 0 ? y1 : y1 - h;
        return new CropRect { X = x, Y = y, Width = w, Height = h };
    }

    /// <summary>
    /// Place a fixed pixel size box on the image, centered on <paramref name="anchor"/> when possible.
    /// If the image is smaller than the fixed size on an axis, that axis is clamped to the image size.
    /// </summary>
    public static CropRect PlaceFixedSize(
        int anchorX,
        int anchorY,
        int fixedW,
        int fixedH,
        int imageW,
        int imageH)
    {
        if (imageW <= 0 || imageH <= 0 || fixedW <= 0 || fixedH <= 0)
            return default;

        int w = Math.Min(fixedW, imageW);
        int h = Math.Min(fixedH, imageH);
        int x = anchorX - w / 2;
        int y = anchorY - h / 2;
        x = Math.Clamp(x, 0, Math.Max(0, imageW - w));
        y = Math.Clamp(y, 0, Math.Max(0, imageH - h));
        return new CropRect { X = x, Y = y, Width = w, Height = h };
    }

    /// <summary>
    /// Center of a rectangle (integer).
    /// </summary>
    public static (int X, int Y) RectCenter(CropRect rect) =>
        (rect.X + rect.Width / 2, rect.Y + rect.Height / 2);
}

/// <summary>
/// Crop drag preset: free, locked aspect ratio, and/or fixed export pixel size.
/// </summary>
public sealed class CropAspectPreset
{
    public CropAspectPreset(
        string name,
        double? aspectWidth = null,
        double? aspectHeight = null,
        int? fixedWidth = null,
        int? fixedHeight = null)
    {
        Name = name ?? throw new ArgumentNullException(nameof(name));
        AspectWidth = aspectWidth;
        AspectHeight = aspectHeight;
        FixedWidth = fixedWidth;
        FixedHeight = fixedHeight;
    }

    public string Name { get; }
    public double? AspectWidth { get; }
    public double? AspectHeight { get; }
    public int? FixedWidth { get; }
    public int? FixedHeight { get; }

    public bool IsFree =>
        (!AspectWidth.HasValue || !AspectHeight.HasValue || AspectWidth <= 0 || AspectHeight <= 0)
        && !FixedWidth.HasValue;

    public bool HasFixedSize => FixedWidth is > 0 && FixedHeight is > 0;

    /// <summary>Effective aspect for rubber-band (fixed size implies its ratio).</summary>
    public bool TryGetAspect(out double aspectW, out double aspectH)
    {
        if (FixedWidth is int fw && fw > 0 && FixedHeight is int fh && fh > 0)
        {
            aspectW = fw;
            aspectH = fh;
            return true;
        }

        if (AspectWidth is double aw && aw > 0 && AspectHeight is double ah && ah > 0)
        {
            aspectW = aw;
            aspectH = ah;
            return true;
        }

        aspectW = 0;
        aspectH = 0;
        return false;
    }

    public static IReadOnlyList<CropAspectPreset> CreateDefaults() => new[]
    {
        new CropAspectPreset("自由"),
        new CropAspectPreset("1:1", 1, 1),
        new CropAspectPreset("4:3", 4, 3),
        new CropAspectPreset("3:4", 3, 4),
        new CropAspectPreset("16:9", 16, 9),
        new CropAspectPreset("9:16", 9, 16),
        new CropAspectPreset("3:2", 3, 2),
        new CropAspectPreset("2:3", 2, 3),
        new CropAspectPreset("512×512", 1, 1, 512, 512),
        new CropAspectPreset("768×768", 1, 1, 768, 768),
        new CropAspectPreset("1024×1024", 1, 1, 1024, 1024),
        new CropAspectPreset("1024×576 (16:9)", 16, 9, 1024, 576),
        new CropAspectPreset("576×1024 (9:16)", 9, 16, 576, 1024),
    };
}
