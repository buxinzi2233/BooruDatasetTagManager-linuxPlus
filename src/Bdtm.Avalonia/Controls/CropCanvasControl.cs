using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Data;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Bdtm.Core;

namespace Bdtm.Avalonia.Controls;

/// <summary>
/// Fitted image canvas with multi-region crop overlays and drag-to-add interaction.
/// </summary>
public class CropCanvasControl : Control
{
    public static readonly StyledProperty<Bitmap?> SourceProperty =
        AvaloniaProperty.Register<CropCanvasControl, Bitmap?>(nameof(Source));

    public static readonly StyledProperty<IList<CropRegion>?> RegionsProperty =
        AvaloniaProperty.Register<CropCanvasControl, IList<CropRegion>?>(nameof(Regions));

    public static readonly StyledProperty<CropRegion?> SelectedRegionProperty =
        AvaloniaProperty.Register<CropCanvasControl, CropRegion?>(
            nameof(SelectedRegion),
            defaultBindingMode: BindingMode.TwoWay);

    public static readonly StyledProperty<int> ImagePixelWidthProperty =
        AvaloniaProperty.Register<CropCanvasControl, int>(nameof(ImagePixelWidth));

    public static readonly StyledProperty<int> ImagePixelHeightProperty =
        AvaloniaProperty.Register<CropCanvasControl, int>(nameof(ImagePixelHeight));

    public static readonly StyledProperty<CropAspectPreset?> AspectPresetProperty =
        AvaloniaProperty.Register<CropCanvasControl, CropAspectPreset?>(nameof(AspectPreset));

    private bool _dragging;
    private Point _dragStart;
    private Point _dragEnd;
    private INotifyCollectionChanged? _regionsNotify;

    public CropCanvasControl()
    {
        ClipToBounds = true;
        Focusable = true;
        Cursor = new Cursor(StandardCursorType.Cross);
    }

    public Bitmap? Source
    {
        get => GetValue(SourceProperty);
        set => SetValue(SourceProperty, value);
    }

    public IList<CropRegion>? Regions
    {
        get => GetValue(RegionsProperty);
        set => SetValue(RegionsProperty, value);
    }

    public CropRegion? SelectedRegion
    {
        get => GetValue(SelectedRegionProperty);
        set => SetValue(SelectedRegionProperty, value);
    }

    public int ImagePixelWidth
    {
        get => GetValue(ImagePixelWidthProperty);
        set => SetValue(ImagePixelWidthProperty, value);
    }

    public int ImagePixelHeight
    {
        get => GetValue(ImagePixelHeightProperty);
        set => SetValue(ImagePixelHeightProperty, value);
    }

    /// <summary>When set, drag rubber-band locks to this aspect / fixed ratio.</summary>
    public CropAspectPreset? AspectPreset
    {
        get => GetValue(AspectPresetProperty);
        set => SetValue(AspectPresetProperty, value);
    }

    /// <summary>Raised when user finishes dragging a new region (image-space rect, pre-clamp).</summary>
    public event Action<CropRect>? RegionDragCompleted;

    static CropCanvasControl()
    {
        AffectsRender<CropCanvasControl>(
            SourceProperty,
            RegionsProperty,
            SelectedRegionProperty,
            ImagePixelWidthProperty,
            ImagePixelHeightProperty,
            AspectPresetProperty);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == RegionsProperty)
        {
            if (_regionsNotify is not null)
                _regionsNotify.CollectionChanged -= OnRegionsChanged;

            _regionsNotify = change.NewValue as INotifyCollectionChanged;
            if (_regionsNotify is not null)
                _regionsNotify.CollectionChanged += OnRegionsChanged;

            InvalidateVisual();
        }
        else if (change.Property == SourceProperty
                 || change.Property == SelectedRegionProperty
                 || change.Property == ImagePixelWidthProperty
                 || change.Property == ImagePixelHeightProperty
                 || change.Property == AspectPresetProperty)
        {
            InvalidateVisual();
        }
    }

    private CropRect BuildDragScreenRect()
    {
        CropAspectPreset? preset = AspectPreset;
        if (preset is not null && preset.TryGetAspect(out double aw, out double ah))
        {
            return CropCanvasHelper.NormalizeDragRectangleWithAspect(
                (int)_dragStart.X, (int)_dragStart.Y,
                (int)_dragEnd.X, (int)_dragEnd.Y,
                aw, ah);
        }

        return CropCanvasHelper.NormalizeDragRectangle(
            (int)_dragStart.X, (int)_dragStart.Y,
            (int)_dragEnd.X, (int)_dragEnd.Y);
    }

    private void OnRegionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
        => InvalidateVisual();

    public override void Render(DrawingContext context)
    {
        base.Render(context);

        var viewBounds = Bounds;
        int viewW = Math.Max(1, (int)viewBounds.Width);
        int viewH = Math.Max(1, (int)viewBounds.Height);

        context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x1e, 0x1e, 0x1e)), new Rect(0, 0, viewW, viewH));

        int imageW = ImagePixelWidth;
        int imageH = ImagePixelHeight;
        if (imageW <= 0 || imageH <= 0)
        {
            if (Source is not null)
            {
                imageW = Source.PixelSize.Width;
                imageH = Source.PixelSize.Height;
            }
            else
            {
                return;
            }
        }

        CropRect imgLoc = CropCanvasHelper.CalcImageLocation(imageW, imageH, viewW, viewH);
        var dest = new Rect(imgLoc.X, imgLoc.Y, Math.Max(1, imgLoc.Width), Math.Max(1, imgLoc.Height));

        if (Source is not null)
        {
            context.DrawImage(Source, new Rect(0, 0, Source.PixelSize.Width, Source.PixelSize.Height), dest);
        }
        else
        {
            context.FillRectangle(new SolidColorBrush(Color.FromRgb(0x33, 0x33, 0x33)), dest);
        }

        IList<CropRegion>? regions = Regions;
        if (regions is not null)
        {
            foreach (CropRegion region in regions)
            {
                CropRect screen = CropCanvasHelper.ImageRectToScreenRect(
                    region.Bounds, imageW, imageH, viewW, viewH);
                if (screen.IsEmpty)
                    continue;

                bool selected = ReferenceEquals(region, SelectedRegion);
                Color color = Color.FromUInt32(region.DisplayColorArgb);
                double thickness = selected ? 3 : 2;
                var pen = new Pen(new SolidColorBrush(color), thickness);
                var rect = new Rect(screen.X, screen.Y, screen.Width, screen.Height);
                context.DrawRectangle(null, pen, rect);

                // Label background
                var labelBg = new Rect(screen.X, screen.Y, 32, 18);
                context.FillRectangle(new SolidColorBrush(color), labelBg);

                var ft = new FormattedText(
                    "#" + region.Index,
                    CultureInfo.CurrentCulture,
                    FlowDirection.LeftToRight,
                    new Typeface("Inter"),
                    12,
                    Brushes.White);
                context.DrawText(ft, new Point(screen.X + 3, screen.Y + 1));
            }
        }

        if (_dragging)
        {
            CropRect drag = BuildDragScreenRect();
            if (!drag.IsEmpty)
            {
                var pen = new Pen(Brushes.Red, 2);
                context.DrawRectangle(null, pen, new Rect(drag.X, drag.Y, drag.Width, drag.Height));
            }
        }
    }

    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        base.OnPointerPressed(e);
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        Focus();
        Point p = e.GetPosition(this);
        int viewW = Math.Max(1, (int)Bounds.Width);
        int viewH = Math.Max(1, (int)Bounds.Height);
        int imageW = EffectiveImageWidth();
        int imageH = EffectiveImageHeight();
        if (imageW <= 0 || imageH <= 0)
            return;

        IList<CropRegion>? regions = Regions;
        if (regions is not null && regions.Count > 0)
        {
            CropRegion? hit = CropCanvasHelper.HitTest(
                regions is IReadOnlyList<CropRegion> ro
                    ? ro
                    : new List<CropRegion>(regions),
                (int)p.X, (int)p.Y,
                imageW, imageH, viewW, viewH);
            if (hit is not null)
            {
                SelectedRegion = hit;
                _dragging = false;
                e.Handled = true;
                InvalidateVisual();
                return;
            }
        }

        SelectedRegion = null;
        _dragging = true;
        _dragStart = p;
        _dragEnd = p;
        e.Pointer.Capture(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerMoved(PointerEventArgs e)
    {
        base.OnPointerMoved(e);
        if (!_dragging)
            return;

        _dragEnd = e.GetPosition(this);
        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        base.OnPointerReleased(e);
        if (!_dragging)
            return;

        _dragging = false;
        e.Pointer.Capture(null);
        _dragEnd = e.GetPosition(this);

        int viewW = Math.Max(1, (int)Bounds.Width);
        int viewH = Math.Max(1, (int)Bounds.Height);
        int imageW = EffectiveImageWidth();
        int imageH = EffectiveImageHeight();

        CropRect screenRect = BuildDragScreenRect();

        if (!screenRect.IsEmpty && imageW > 0 && imageH > 0)
        {
            CropRect imageRect = CropCanvasHelper.ScreenRectToImageRect(
                screenRect, imageW, imageH, viewW, viewH);

            // Fixed pixel presets: snap to exact WxH around drag center (when image allows).
            CropAspectPreset? preset = AspectPreset;
            if (preset is not null && preset.HasFixedSize
                && preset.FixedWidth is int fw && preset.FixedHeight is int fh
                && !imageRect.IsEmpty)
            {
                var (cx, cy) = CropCanvasHelper.RectCenter(imageRect);
                imageRect = CropCanvasHelper.PlaceFixedSize(cx, cy, fw, fh, imageW, imageH);
            }

            if (!imageRect.IsEmpty)
                RegionDragCompleted?.Invoke(imageRect);
        }

        e.Handled = true;
        InvalidateVisual();
    }

    protected override void OnPointerCaptureLost(PointerCaptureLostEventArgs e)
    {
        base.OnPointerCaptureLost(e);
        if (_dragging)
        {
            _dragging = false;
            InvalidateVisual();
        }
    }

    protected override Size MeasureOverride(Size availableSize)
    {
        double w = double.IsInfinity(availableSize.Width) ? 400 : availableSize.Width;
        double h = double.IsInfinity(availableSize.Height) ? 400 : availableSize.Height;
        return new Size(Math.Max(0, w), Math.Max(0, h));
    }

    private int EffectiveImageWidth()
    {
        if (ImagePixelWidth > 0) return ImagePixelWidth;
        return Source?.PixelSize.Width ?? 0;
    }

    private int EffectiveImageHeight()
    {
        if (ImagePixelHeight > 0) return ImagePixelHeight;
        return Source?.PixelSize.Height ?? 0;
    }
}
