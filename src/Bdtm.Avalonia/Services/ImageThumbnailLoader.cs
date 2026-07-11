using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Avalonia.Media.Imaging;
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

    public static async Task<Bitmap?> LoadAsync(string path, int maxEdge, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path) || !IsRasterImage(path))
            return null;

        return await Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            using var image = Image.Load(path);
            image.Mutate(ctx =>
            {
                ctx.AutoOrient();
                ctx.Resize(new ResizeOptions
                {
                    Size = new Size(maxEdge, maxEdge),
                    Mode = ResizeMode.Max,
                });
            });

            using var ms = new MemoryStream();
            image.Save(ms, new PngEncoder());
            ms.Position = 0;
            return new Bitmap(ms);
        }, ct).ConfigureAwait(false);
    }
}
