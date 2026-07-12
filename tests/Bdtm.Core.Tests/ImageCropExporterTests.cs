using Bdtm.Core;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Png;
using SixLabors.ImageSharp.PixelFormats;
using Xunit;

namespace Bdtm.Core.Tests;

public sealed class ImageCropExporterTests
{
    [Fact]
    public void GetOutputPath_uses_same_folder_and_region_suffix()
    {
        string root = Path.Combine(Path.GetTempPath(), "bdtm-crop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string imagePath = Path.Combine(root, "folder", "sample.png");
            Directory.CreateDirectory(Path.GetDirectoryName(imagePath)!);

            string output = ImageCropExporter.GetOutputPath(imagePath, 2);

            Assert.Equal(Path.Combine(root, "folder", "sample_r2.png"), output);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void GetOutputPath_defaults_empty_extension_to_png()
    {
        string root = Path.Combine(Path.GetTempPath(), "bdtm-crop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string imagePath = Path.Combine(root, "noext");
            string output = ImageCropExporter.GetOutputPath(imagePath, 1);
            Assert.Equal(Path.Combine(root, "noext_r1.png"), output);
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }

    [Fact]
    public void ExportRegions_writes_correct_size_png_and_skips_tiny()
    {
        string root = Path.Combine(Path.GetTempPath(), "bdtm-crop-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            string imagePath = Path.Combine(root, "source.png");
            using (var image = new Image<Rgba32>(120, 80))
            {
                image.Save(imagePath, new PngEncoder());
            }

            var regions = new List<CropRegion>
            {
                new() { Index = 1, Bounds = new CropRect { X = 0, Y = 0, Width = 40, Height = 40 } },
                new() { Index = 2, Bounds = new CropRect { X = 50, Y = 10, Width = 30, Height = 30 } },
                new() { Index = 3, Bounds = new CropRect { X = 0, Y = 0, Width = 1, Height = 1 } },
            };

            IReadOnlyList<string> exportedPaths = ImageCropExporter.ExportRegions(imagePath, regions);

            Assert.Equal(2, exportedPaths.Count);
            Assert.True(File.Exists(ImageCropExporter.GetOutputPath(imagePath, 1)));
            Assert.True(File.Exists(ImageCropExporter.GetOutputPath(imagePath, 2)));
            Assert.False(File.Exists(ImageCropExporter.GetOutputPath(imagePath, 3)));
            Assert.Equal(root, ImageCropExporter.GetOutputDirectory(imagePath));

            using (var crop1 = Image.Load<Rgba32>(ImageCropExporter.GetOutputPath(imagePath, 1)))
            {
                Assert.Equal(40, crop1.Width);
                Assert.Equal(40, crop1.Height);
            }

            using (var crop2 = Image.Load<Rgba32>(ImageCropExporter.GetOutputPath(imagePath, 2)))
            {
                Assert.Equal(30, crop2.Width);
                Assert.Equal(30, crop2.Height);
            }
        }
        finally
        {
            Directory.Delete(root, true);
        }
    }
}
