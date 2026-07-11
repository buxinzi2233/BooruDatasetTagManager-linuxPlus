using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
using Avalonia.Threading;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.Processing;

namespace Bdtm.Avalonia.Services;

public static class ImageThumbnailLoader
{
    private static readonly string[] ImageExt =
    {
        ".jpg", ".jpeg", ".png", ".bmp", ".webp",
    };

    public static bool IsRasterImage(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return Array.IndexOf(ImageExt, ext) >= 0;
    }

    /// <summary>
    /// Decode/resize off UI thread; construct Avalonia Bitmap on UI thread only.
    /// Off-thread Bitmap construction causes native abort with Avalonia/Skia.
    /// </summary>
    public static async Task<Bitmap?> LoadAsync(string path, int maxEdge, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !IsRasterImage(path))
            return null;

        byte[]? pngBytes;
        try
        {
            pngBytes = await Task.Run(() => EncodePngThumbnail(path, maxEdge, ct), ct);
        }
        catch (OperationCanceledException)
        {
            return null;
        }
        catch
        {
            return null;
        }

        if (pngBytes is null || pngBytes.Length == 0)
            return null;

        if (Dispatcher.UIThread.CheckAccess())
            return DecodeBitmap(pngBytes);

        return await Dispatcher.UIThread.InvokeAsync(() => DecodeBitmap(pngBytes));
    }

    private static byte[]? EncodePngThumbnail(string path, int maxEdge, CancellationToken ct)
    {
        ct.ThrowIfCancellationRequested();
        using var image = Image.Load(path);
        ct.ThrowIfCancellationRequested();
        image.Mutate(ctx =>
        {
            try { ctx.AutoOrient(); } catch { /* optional EXIF */ }
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(maxEdge, maxEdge),
                Mode = ResizeMode.Max,
            });
        });

        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }

    private static Bitmap? DecodeBitmap(byte[] pngBytes)
    {
        try
        {
            using var ms = new MemoryStream(pngBytes);
            return new Bitmap(ms);
        }
        catch
        {
            return null;
        }
    }
}
