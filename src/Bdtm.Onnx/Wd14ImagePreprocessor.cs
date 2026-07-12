using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bdtm.Onnx;

/// <summary>
/// ImageSharp-based WD14 preprocessor (BGR NHWC float tensor), replacing System.Drawing.
/// </summary>
public static class Wd14ImagePreprocessor
{
    public static DenseTensor<float> CreateInputTensor(string imagePath, int targetSize)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(imagePath);
        return CreateInputTensor(image, targetSize);
    }

    public static DenseTensor<float> CreateInputTensor(Image<Rgb24> source, int targetSize)
    {
        using Image<Rgb24> prepared = Prepare(source, targetSize);
        int width = prepared.Width;
        int height = prepared.Height;
        var tensor = new DenseTensor<float>(new[] { 1, height, width, 3 });

        prepared.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    Rgb24 px = row[x];
                    // WD14 expects BGR channel order in NHWC layout.
                    tensor[0, y, x, 0] = px.B;
                    tensor[0, y, x, 1] = px.G;
                    tensor[0, y, x, 2] = px.R;
                }
            }
        });

        return tensor;
    }

    private static Image<Rgb24> Prepare(Image<Rgb24> source, int targetSize)
    {
        // Pad to square on white, then resize to targetSize.
        int maxSide = Math.Max(source.Width, source.Height);
        int canvas = Math.Max(maxSide, targetSize);

        var square = new Image<Rgb24>(canvas, canvas, Color.White);
        int offsetX = (canvas - source.Width) / 2;
        int offsetY = (canvas - source.Height) / 2;
        square.Mutate(ctx => ctx.DrawImage(source, new Point(offsetX, offsetY), 1f));

        if (square.Width != targetSize || square.Height != targetSize)
        {
            square.Mutate(ctx => ctx.Resize(targetSize, targetSize));
        }

        return square;
    }
}
