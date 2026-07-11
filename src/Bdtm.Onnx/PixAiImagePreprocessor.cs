using Microsoft.ML.OnnxRuntime.Tensors;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Bdtm.Onnx;

/// <summary>
/// PixAI preprocess: resize to WxH, RGB NCHW, (x/255 - 0.5) / 0.5.
/// </summary>
public static class PixAiImagePreprocessor
{
    public static DenseTensor<float> CreateInputTensor(string imagePath, int width, int height)
    {
        using Image<Rgb24> image = Image.Load<Rgb24>(imagePath);
        return CreateInputTensor(image, width, height);
    }

    public static DenseTensor<float> CreateInputTensor(Image<Rgb24> source, int width, int height)
    {
        using var resized = source.Clone(ctx =>
        {
            ctx.Resize(new ResizeOptions
            {
                Size = new Size(width, height),
                Mode = ResizeMode.Stretch,
            });
        });

        var tensor = new DenseTensor<float>(new[] { 1, 3, height, width });
        resized.ProcessPixelRows(accessor =>
        {
            for (int y = 0; y < height; y++)
            {
                Span<Rgb24> row = accessor.GetRowSpan(y);
                for (int x = 0; x < width; x++)
                {
                    Rgb24 px = row[x];
                    tensor[0, 0, y, x] = (px.R / 255f - 0.5f) / 0.5f;
                    tensor[0, 1, y, x] = (px.G / 255f - 0.5f) / 0.5f;
                    tensor[0, 2, y, x] = (px.B / 255f - 0.5f) / 0.5f;
                }
            }
        });
        return tensor;
    }
}
