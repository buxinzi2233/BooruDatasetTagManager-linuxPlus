using System.Net;
using System.Net.Http.Headers;
using System.Text;

namespace Bdtm.Core;

public enum HuggingFaceDownloadSource
{
    HuggingFace,
    HfMirror,
}

/// <summary>
/// Download ONNX tagger files into a models root: Models/&lt;org&gt;/&lt;repo&gt;/file
/// </summary>
public sealed class HuggingFaceModelDownloader
{
    private const long MinOnnxFileBytes = 1024 * 1024;
    private static readonly HttpClient SharedClient = new()
    {
        Timeout = TimeSpan.FromHours(6),
    };

    static HuggingFaceModelDownloader()
    {
        SharedClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "Bdtm.Linux/0.3 (+https://github.com/buxinzi2233/BooruDatasetTagManager-linuxPlus)");
    }

    private readonly string _modelsRoot;

    public HuggingFaceModelDownloader(string modelsRoot)
    {
        _modelsRoot = modelsRoot ?? throw new ArgumentNullException(nameof(modelsRoot));
    }

    public static string BuildDownloadUrl(HuggingFaceDownloadSource source, string repo, string filename)
    {
        string baseUrl = source == HuggingFaceDownloadSource.HfMirror
            ? "https://hf-mirror.com"
            : "https://huggingface.co";
        return $"{baseUrl}/{repo.Trim('/')}/resolve/main/{filename}";
    }

    public string GetLocalDirectory(string repo)
    {
        string safeRepo = repo.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
        return Path.Combine(_modelsRoot, safeRepo);
    }

    public string GetLocalPath(string repo, string filename) =>
        Path.Combine(GetLocalDirectory(repo), filename);

    public static bool ValidateCachedFile(string path, string filename)
    {
        try
        {
            if (!File.Exists(path))
                return false;
            var info = new FileInfo(path);
            if (info.Length == 0)
                return false;
            if (LooksLikeHtml(path))
                return false;
            if (string.Equals(filename, "model.onnx", StringComparison.OrdinalIgnoreCase))
                return info.Length >= MinOnnxFileBytes;
            if (string.Equals(filename, "selected_tags.csv", StringComparison.OrdinalIgnoreCase))
            {
                string first = ReadFirstLine(path);
                return first.Contains("name", StringComparison.OrdinalIgnoreCase)
                    || first.Contains("tag", StringComparison.OrdinalIgnoreCase)
                    || first.Length > 0;
            }
            return info.Length > 0;
        }
        catch
        {
            return false;
        }
    }

    public bool IsFileCached(string repo, string filename) =>
        ValidateCachedFile(GetLocalPath(repo, filename), filename);

    public bool IsModelReady(string repo) =>
        IsFileCached(repo, "model.onnx") && IsFileCached(repo, "selected_tags.csv");

    public void DeleteCachedFile(string repo, string filename)
    {
        string path = GetLocalPath(repo, filename);
        if (File.Exists(path))
            File.Delete(path);
    }

    public async Task DownloadModelAsync(
        HuggingFaceDownloadSource source,
        string repo,
        IProgress<(string file, long downloaded, long? total)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        foreach (string file in new[] { "model.onnx", "selected_tags.csv" })
        {
            if (IsFileCached(repo, file))
            {
                progress?.Report((file + " (cached)", 1, 1));
                continue;
            }
            await DownloadFileAsync(source, repo, file, progress, cancellationToken).ConfigureAwait(false);
        }
    }

    public async Task<string> DownloadFileAsync(
        HuggingFaceDownloadSource source,
        string repo,
        string filename,
        IProgress<(string file, long downloaded, long? total)>? progress,
        CancellationToken cancellationToken)
    {
        string localPath = GetLocalPath(repo, filename);
        Directory.CreateDirectory(Path.GetDirectoryName(localPath)!);

        if (File.Exists(localPath) && !ValidateCachedFile(localPath, filename))
            DeleteCachedFile(repo, filename);

        string url = BuildDownloadUrl(source, repo, filename);
        long existingLength = File.Exists(localPath) ? new FileInfo(localPath).Length : 0;

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        if (existingLength > 0)
            request.Headers.Range = new RangeHeaderValue(existingLength, null);

        using HttpResponseMessage response = await SharedClient.SendAsync(
            request, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

        if (response.StatusCode == HttpStatusCode.RequestedRangeNotSatisfiable)
        {
            progress?.Report((filename, existingLength, existingLength));
            EnsureValid(repo, filename, localPath);
            return localPath;
        }

        response.EnsureSuccessStatusCode();
        string contentType = response.Content.Headers.ContentType?.MediaType ?? string.Empty;
        if (contentType.Contains("text/html", StringComparison.OrdinalIgnoreCase))
        {
            DeleteCachedFile(repo, filename);
            throw new InvalidOperationException("Download returned HTML (mirror/auth error). Try another source.");
        }

        long? total = response.Content.Headers.ContentLength.HasValue
            ? existingLength + response.Content.Headers.ContentLength.Value
            : null;

        FileMode mode = response.StatusCode == HttpStatusCode.PartialContent && existingLength > 0
            ? FileMode.Append
            : FileMode.Create;
        if (mode == FileMode.Create)
            existingLength = 0;

        await using (Stream remote = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false))
        await using (var local = new FileStream(localPath, mode, FileAccess.Write, FileShare.Read))
        {
            byte[] buffer = new byte[81920];
            long downloaded = existingLength;
            int read;
            while ((read = await remote.ReadAsync(buffer.AsMemory(0, buffer.Length), cancellationToken).ConfigureAwait(false)) > 0)
            {
                await local.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                downloaded += read;
                progress?.Report((filename, downloaded, total));
            }
        }

        EnsureValid(repo, filename, localPath);
        return localPath;
    }

    private void EnsureValid(string repo, string filename, string localPath)
    {
        if (ValidateCachedFile(localPath, filename))
            return;
        DeleteCachedFile(repo, filename);
        throw new InvalidOperationException("Downloaded file failed validation: " + filename);
    }

    private static string ReadFirstLine(string path)
    {
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var reader = new StreamReader(stream);
        return reader.ReadLine() ?? string.Empty;
    }

    private static bool LooksLikeHtml(string path)
    {
        Span<byte> buffer = stackalloc byte[512];
        int read;
        using (var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            read = stream.Read(buffer);
        if (read == 0) return false;
        string prefix = Encoding.ASCII.GetString(buffer[..read]).TrimStart();
        return prefix.StartsWith("<!DOCTYPE", StringComparison.OrdinalIgnoreCase)
            || prefix.StartsWith("<html", StringComparison.OrdinalIgnoreCase);
    }
}
