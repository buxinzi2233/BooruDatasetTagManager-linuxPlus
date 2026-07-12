namespace Bdtm.Core;

/// <summary>Supported media file extensions for dataset loading.</summary>
public static class MediaExtensions
{
    public static readonly string[] ImageExtensions = { ".jpg", ".png", ".bmp", ".jpeg", ".webp" };
    public static readonly string[] VideoExtensions = { ".mp4", ".flv", ".mkv", ".ts", ".avi", ".webm", ".mov" };

    public static bool IsSupportedMedia(string path)
    {
        string ext = Path.GetExtension(path).ToLowerInvariant();
        return ImageExtensions.Contains(ext) || VideoExtensions.Contains(ext);
    }
}
