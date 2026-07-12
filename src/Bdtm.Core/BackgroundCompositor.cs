using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;

namespace Bdtm.Core;

public static class BackgroundCompositor
{
    /// <summary>
    /// Parses an ARGB/RGB hex color string (#RRGGBB or #AARRGGBB) into component bytes.
    /// Falls back to opaque white on a malformed value.
    /// </summary>
    public static (byte R, byte G, byte B, byte A) ParseColor(string argb)
    {
        if (string.IsNullOrWhiteSpace(argb))
            return (255, 255, 255, 255);

        string hex = argb.TrimStart('#');
        if (hex.Length == 6)
        {
            return (
                Convert.ToByte(hex[0..2], 16),
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                (byte)255);
        }
        if (hex.Length == 8)
        {
            return (
                Convert.ToByte(hex[2..4], 16),
                Convert.ToByte(hex[4..6], 16),
                Convert.ToByte(hex[6..8], 16),
                Convert.ToByte(hex[0..2], 16));
        }
        return (255, 255, 255, 255);
    }

    /// <summary>
    /// Composites a transparent PNG onto a solid background color. Pixels with alpha
    /// below 128 are replaced by the solid color at full opacity; semi-transparent and
    /// opaque pixels are kept as-is. Returns the resulting PNG encoded bytes.
    /// </summary>
    public static byte[] CompositeTransparentToSolid(byte[] rgbaPngBytes, int width, int height, string solidColorArgb)
    {
        var (r, g, b, _) = ParseColor(solidColorArgb);
        var solid = new Rgba32(r, g, b, 255);

        using var image = Image.Load<Rgba32>(rgbaPngBytes);
        // The caller-supplied width/height are advisory; trust the decoded image bounds.
        if (image.Width != width || image.Height != height)
        {
            width = image.Width;
            height = image.Height;
        }

        image.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < accessor.Height; y++)
            {
                var row = accessor.GetRowSpan(y);
                for (int x = 0; x < row.Length; x++)
                {
                    if (row[x].A < 128)
                        row[x] = solid;
                }
            }
        });

        using var ms = new MemoryStream();
        image.Save(ms, new PngEncoder());
        return ms.ToArray();
    }
}