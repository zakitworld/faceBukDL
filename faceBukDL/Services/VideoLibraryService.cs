using System.Text.Json;

namespace faceBukDL.Services;

public sealed record VideoDownloadRecord(
    Guid Id,
    string SourceUrl,
    string FilePath,
    string FileName,
    long SizeBytes,
    DateTimeOffset DownloadedAt);

public interface IVideoLibraryService
{
    Task<IReadOnlyList<VideoDownloadRecord>> GetAllAsync(CancellationToken cancellationToken = default);
    Task AddAsync(VideoDownloadRecord record, CancellationToken cancellationToken = default);
    Task DeleteAsync(Guid id, CancellationToken cancellationToken = default);
}

public sealed class JsonVideoLibraryService : IVideoLibraryService
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web) { WriteIndented = true };
    private readonly SemaphoreSlim _gate = new(1, 1);
    private string FilePath => Path.Combine(FileSystem.AppDataDirectory, "video-library.json");

    public async Task<IReadOnlyList<VideoDownloadRecord>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try { return await ReadUnsafeAsync(cancellationToken); }
        finally { _gate.Release(); }
    }

    public async Task AddAsync(VideoDownloadRecord record, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = (await ReadUnsafeAsync(cancellationToken)).ToList();
            records.RemoveAll(x => x.Id == record.Id);
            records.Insert(0, record);
            await WriteUnsafeAsync(records, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var records = (await ReadUnsafeAsync(cancellationToken)).ToList();
            records.RemoveAll(x => x.Id == id);
            await WriteUnsafeAsync(records, cancellationToken);
        }
        finally { _gate.Release(); }
    }

    private async Task<List<VideoDownloadRecord>> ReadUnsafeAsync(CancellationToken cancellationToken)
    {
        if (!File.Exists(FilePath)) return [];
        await using var input = File.OpenRead(FilePath);
        return await JsonSerializer.DeserializeAsync<List<VideoDownloadRecord>>(input, JsonOptions, cancellationToken) ?? [];
    }

    private async Task WriteUnsafeAsync(List<VideoDownloadRecord> records, CancellationToken cancellationToken)
    {
        var temp = FilePath + ".tmp";
        await using (var output = new FileStream(temp, FileMode.Create, FileAccess.Write, FileShare.None, 4096, true))
            await JsonSerializer.SerializeAsync(output, records, JsonOptions, cancellationToken);
        File.Move(temp, FilePath, true);
    }
}
