using Bdtm.Core;
using Xunit;

namespace Bdtm.Core.Tests;

public class CropGeometryTests
{
    [Fact]
    public void ClampToImage_ClipsOverflow()
    {
        var r = CropCanvasHelper.ClampToImage(
            new CropRect { X = -10, Y = -5, Width = 50, Height = 40 },
            100,
            80);

        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
        Assert.True(r.Right <= 100);
        Assert.True(r.Bottom <= 80);
        Assert.Equal(40, r.Width);
        Assert.Equal(35, r.Height);
    }

    [Fact]
    public void NormalizeDragRectangle_OrdersCorners()
    {
        var r = CropCanvasHelper.NormalizeDragRectangle(30, 40, 10, 15);

        Assert.Equal(10, r.X);
        Assert.Equal(15, r.Y);
        Assert.Equal(20, r.Width);
        Assert.Equal(25, r.Height);
    }

    [Fact]
    public void CalcZoomMod_FitsInsideViewport()
    {
        float mod = CropCanvasHelper.CalcZoomMod(imageW: 200, imageH: 100, viewW: 100, viewH: 100);

        Assert.True(mod * 200 <= 100 + 0.01f);
        Assert.True(mod * 100 <= 100 + 0.01f);
    }

    [Fact]
    public void ScreenRectToImageRect_RoundTripApprox()
    {
        int iw = 200, ih = 100, vw = 400, vh = 200;
        var imgLoc = CropCanvasHelper.CalcImageLocation(iw, ih, vw, vh);
        var image = CropCanvasHelper.ScreenRectToImageRect(imgLoc, iw, ih, vw, vh);

        Assert.InRange(image.Width, iw - 2, iw + 2);
        Assert.InRange(image.Height, ih - 2, ih + 2);
    }

    [Fact]
    public void HitTest_ReturnsTopmostContaining()
    {
        var regions = new List<CropRegion>
        {
            new() { Index = 1, Bounds = new CropRect { X = 0, Y = 0, Width = 50, Height = 50 } },
            new() { Index = 2, Bounds = new CropRect { X = 10, Y = 10, Width = 20, Height = 20 } },
        };

        // image 100x100 view 100x100 → mod=1, location 0,0
        var hit = CropCanvasHelper.HitTest(
            regions,
            screenX: 15,
            screenY: 15,
            imageW: 100,
            imageH: 100,
            viewW: 100,
            viewH: 100);

        Assert.NotNull(hit);
        Assert.Equal(2, hit!.Index);
    }

    [Fact]
    public void RegionColors_MatchOriginalArgb()
    {
        Assert.Equal(6, CropCanvasHelper.RegionColors.Length);
        Assert.Equal(0xDCE74C3Cu, CropCanvasHelper.RegionColors[0]);
        Assert.Equal(0xDC2ECC71u, CropCanvasHelper.RegionColors[1]);
        Assert.Equal(0xDC3498DBu, CropCanvasHelper.RegionColors[2]);
        Assert.Equal(0xDCF1C40Fu, CropCanvasHelper.RegionColors[3]);
        Assert.Equal(0xDC9B59B6u, CropCanvasHelper.RegionColors[4]);
        Assert.Equal(0xDCE67E22u, CropCanvasHelper.RegionColors[5]);
    }

    [Fact]
    public void NormalizeDragRectangleWithAspect_LocksOneToOne()
    {
        // drag right 100, down 40 → square side 100
        var r = CropCanvasHelper.NormalizeDragRectangleWithAspect(10, 20, 110, 60, 1, 1);
        Assert.Equal(10, r.X);
        Assert.Equal(20, r.Y);
        Assert.Equal(100, r.Width);
        Assert.Equal(100, r.Height);
    }

    [Fact]
    public void NormalizeDragRectangleWithAspect_LocksSixteenNine()
    {
        // drag height-dominant: height 90 → width 160
        var r = CropCanvasHelper.NormalizeDragRectangleWithAspect(0, 0, 10, 90, 16, 9);
        Assert.Equal(0, r.X);
        Assert.Equal(0, r.Y);
        Assert.Equal(160, r.Width);
        Assert.Equal(90, r.Height);
    }

    [Fact]
    public void NormalizeDragRectangleWithAspect_NegativeDragKeepsStartAsOppositeCorner()
    {
        var r = CropCanvasHelper.NormalizeDragRectangleWithAspect(100, 100, 40, 40, 1, 1);
        Assert.Equal(60, r.Width);
        Assert.Equal(60, r.Height);
        Assert.Equal(40, r.X); // 100 - 60
        Assert.Equal(40, r.Y);
    }

    [Fact]
    public void NormalizeDragRectangleWithAspect_ZeroAspectIsFree()
    {
        var r = CropCanvasHelper.NormalizeDragRectangleWithAspect(0, 0, 30, 10, 0, 0);
        Assert.Equal(30, r.Width);
        Assert.Equal(10, r.Height);
    }

    [Fact]
    public void PlaceFixedSize_CentersAndClamps()
    {
        var r = CropCanvasHelper.PlaceFixedSize(50, 50, 40, 40, 100, 100);
        Assert.Equal(30, r.X);
        Assert.Equal(30, r.Y);
        Assert.Equal(40, r.Width);
        Assert.Equal(40, r.Height);

        var small = CropCanvasHelper.PlaceFixedSize(10, 10, 200, 200, 100, 80);
        Assert.Equal(0, small.X);
        Assert.Equal(0, small.Y);
        Assert.Equal(100, small.Width);
        Assert.Equal(80, small.Height);
    }

    [Fact]
    public void CropAspectPreset_DefaultsIncludeOneToOneAndFixed()
    {
        var list = CropAspectPreset.CreateDefaults();
        Assert.Contains(list, p => p.Name == "自由" && p.IsFree);
        Assert.Contains(list, p => p.Name == "1:1" && p.TryGetAspect(out var w, out var h) && w == 1 && h == 1);
        Assert.Contains(list, p => p.Name.StartsWith("512") && p.HasFixedSize && p.FixedWidth == 512);
    }
}
