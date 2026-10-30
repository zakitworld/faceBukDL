using System.Text.RegularExpressions;

namespace faceBukDL.Services;

public static class FacebookUrl
{
    private static readonly Regex Pattern = new(
        @"^https?://(?:www\.|m\.)?(?:facebook\.com|fb\.watch)(?:/|$)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public static bool TryParse(string? text, out Uri? uri)
    {
        uri = null;
        if (string.IsNullOrWhiteSpace(text) ||
            !Uri.TryCreate(text.Trim().Trim('"', '\''), UriKind.Absolute, out var parsed) ||
            parsed.Scheme is not ("http" or "https") || !Pattern.IsMatch(parsed.AbsoluteUri))
            return false;
        uri = parsed;
        return true;
    }
}

public interface IVideoUrlResolver
{
    Task<Uri> ResolveAsync(Uri pageUrl, CancellationToken cancellationToken = default);
}

// Facebook page URLs require an authorized resolver; this default makes that limitation explicit.
public sealed class DirectVideoUrlResolver : IVideoUrlResolver
{
    public Task<Uri> ResolveAsync(Uri pageUrl, CancellationToken cancellationToken = default) =>
        throw new NotSupportedException("This Facebook link cannot be resolved here. Configure an authorized video source.");
}

public interface IVideoDownloadService
{
    Task<string> DownloadFacebookVideoAsync(Uri pageUrl, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default);
}

public sealed class VideoDownloadService : IVideoDownloadService
{
    private const long MaxBytes = 500L * 1024 * 1024;
    private static readonly HashSet<string> AllowedHosts = new(StringComparer.OrdinalIgnoreCase)
    {
        // Configure only stream hosts used by your authorized resolver.
    };

    private readonly HttpClient _http;
    private readonly IVideoUrlResolver _resolver;

    public VideoDownloadService(HttpClient http, IVideoUrlResolver resolver)
    {
        _http = http;
        _resolver = resolver;
    }

    public async Task<string> DownloadFacebookVideoAsync(Uri pageUrl, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var streamUrl = await _resolver.ResolveAsync(pageUrl, cancellationToken);
        if (streamUrl.Scheme != Uri.UriSchemeHttps ||
            (!AllowedHosts.Contains(streamUrl.Host) && !IsPrivateDirectTestHost(streamUrl.Host)))
            throw new InvalidOperationException("The video host is not on the configured allowlist.");

        using var response = await _http.GetAsync(streamUrl, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();
        var type = response.Content.Headers.ContentType?.MediaType;
        if (type is null || !type.StartsWith("video/", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected video content, received {type ?? "unknown content"}.");
        if (response.Content.Headers.ContentLength is > MaxBytes)
            throw new InvalidDataException("Video exceeds the 500 MB download limit.");

        var finalPath = Path.Combine(FileSystem.AppDataDirectory,
            $"facebook_{DateTime.UtcNow:yyyyMMdd_HHmmss_fff}.mp4");
        var tempPath = finalPath + ".partial";
        try
        {
            await using var input = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using var output = new FileStream(tempPath, FileMode.CreateNew, FileAccess.Write,
                FileShare.None, 81920, FileOptions.Asynchronous | FileOptions.SequentialScan);
            var buffer = new byte[81920];
            long total = 0;
            int read;
            while ((read = await input.ReadAsync(buffer, cancellationToken)) > 0)
            {
                total += read;
                if (total > MaxBytes) throw new InvalidDataException("Video exceeds the 500 MB download limit.");
                await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
                if (response.Content.Headers.ContentLength is long length && length > 0)
                    progress?.Report(Math.Clamp((double)total / length, 0, 1));
            }
            await output.FlushAsync(cancellationToken);
            cancellationToken.ThrowIfCancellationRequested();
            File.Move(tempPath, finalPath);
            progress?.Report(1);
            return finalPath;
        }
        catch
        {
            if (File.Exists(tempPath)) File.Delete(tempPath);
            throw;
        }
    }

    private static bool IsPrivateDirectTestHost(string host) => false;
}
