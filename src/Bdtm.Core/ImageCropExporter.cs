using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bdtm.Core;

/// <summary>
/// Multi-region crop export using ImageSharp (no System.Drawing).
/// </summary>
public static class ImageCropExporter
{
    public static string GetOutputPath(string imagePath, int regionIndex)
    {
        string fullImage = Path.GetFullPath(imagePath);
        string directory = Path.GetDirectoryName(fullImage) ?? string.Empty;
        string baseName = Path.GetFileNameWithoutExtension(fullImage);
        string ext = Path.GetExtension(fullImage);
        if (string.IsNullOrEmpty(ext))
            ext = ".png";
        return Path.Combine(directory, baseName + "_r" + regionIndex + ext);
    }

    public static string GetOutputDirectory(string imagePath)
    {
        return Path.GetDirectoryName(Path.GetFullPath(imagePath)) ?? string.Empty;
    }

    public static IReadOnlyList<string> ExportRegions(string imagePath, IReadOnlyList<CropRegion> regions)
    {
        if (regions is null || regions.Count == 0)
            return Array.Empty<string>();

        var exportedPaths = new List<string>();
        using Image<Rgba32> source = Image.Load<Rgba32>(imagePath);

        foreach (CropRegion region in regions)
        {
            CropRect bounds = CropCanvasHelper.ClampToImage(region.Bounds, source.Width, source.Height);
            if (bounds.Width < 2 || bounds.Height < 2)
                continue;

            string outputPath = GetOutputPath(imagePath, region.Index);
            var cropRect = new Rectangle(bounds.X, bounds.Y, bounds.Width, bounds.Height);
            using (Image<Rgba32> cropped = source.Clone(ctx => ctx.Crop(cropRect)))
            {
                cropped.Save(outputPath);
            }

            exportedPaths.Add(outputPath);
        }

        return exportedPaths;
    }
}
