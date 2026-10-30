using Microsoft.Maui.ApplicationModel.DataTransfer;

namespace faceBukDL.Services;

public interface IClipboardService
{
    Task<string?> GetTextAsync();
}

public sealed class MauiClipboardService : IClipboardService
{
    public Task<string?> GetTextAsync() => Clipboard.GetTextAsync();
}

public interface IVideoExportService
{
    Task ShareAsync(string filePath, CancellationToken cancellationToken = default);
}

public sealed class VideoExportService : IVideoExportService
{
    public async Task ShareAsync(string filePath, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(filePath)) throw new FileNotFoundException("Video file was not found.", filePath);
        cancellationToken.ThrowIfCancellationRequested();
        await Share.Default.RequestAsync(new ShareFileRequest
        {
            Title = "Share video",
            File = new ShareFile(filePath, "video/mp4")
        });
    }
}
