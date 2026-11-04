using System.Windows.Input;
using faceBukDL.Services;
using System.Collections.ObjectModel;
using Microsoft.Maui.ApplicationModel;

namespace faceBukDL.ViewModels;

public sealed class MainViewModel : BindableObject
{
    private readonly IVideoDownloadService _downloads;
    private readonly IClipboardService _clipboard;
    private readonly IVideoExportService _export;
    private readonly IVideoLibraryService _library;
    private CancellationTokenSource? _downloadCts;
    private string _linkText = string.Empty;
    private string _status = "Copy a Facebook video link, then return here.";
    private double _progress;
    private bool _isBusy;

    public string LinkText
    {
        get => _linkText;
        set
        {
            _linkText = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(HasLink));
            OnPropertyChanged(nameof(CanDownload));
            (DownloadCommand as Command)?.ChangeCanExecute();
        }
    }
    public bool HasLink => !string.IsNullOrWhiteSpace(LinkText);
    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; private set { _progress = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanExport)); OnPropertyChanged(nameof(CanDownload)); } }
    public bool CanExport => !IsBusy && !string.IsNullOrEmpty(_savedPath) && File.Exists(_savedPath);
    public bool CanDownload => !IsBusy && HasLink;
    public ICommand CheckClipboardCommand { get; }
    public ICommand PasteCommand { get; }
    public ICommand DownloadCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }
    public ICommand OpenRecordCommand { get; }
    public ICommand ShareRecordCommand { get; }
    public ICommand DeleteRecordCommand { get; }
    public ObservableCollection<VideoDownloadRecord> Downloads { get; } = [];
    private string? _savedPath;

    public MainViewModel(IVideoDownloadService downloads, IClipboardService clipboard,
        IVideoExportService export, IVideoLibraryService library)
    {
        _downloads = downloads;
        _clipboard = clipboard;
        _export = export;
        _library = library;
        CheckClipboardCommand = new Command(async () => await CheckClipboardAsync());
        PasteCommand = new Command(async () => await PasteClipboardAsync());
        DownloadCommand = new Command(async () => await DownloadLinkAsync(), () => CanDownload);
        CancelCommand = new Command(() => _downloadCts?.Cancel(), () => IsBusy);
        ExportCommand = new Command(async () => await ExportAsync(), () => CanExport);
        OpenRecordCommand = new Command<VideoDownloadRecord>(async record => await OpenRecordAsync(record));
        ShareRecordCommand = new Command<VideoDownloadRecord>(async record => await ShareRecordAsync(record));
        DeleteRecordCommand = new Command<VideoDownloadRecord>(async record => await DeleteRecordAsync(record));
    }

    public async Task InitializeAsync()
    {
        Downloads.Clear();
        foreach (var record in await _library.GetAllAsync())
            if (File.Exists(record.FilePath)) Downloads.Add(record);
    }

    public async Task CheckClipboardAsync()
    {
        if (IsBusy) return;
        try
        {
            // Clipboard access may not be ready on the first lifecycle tick; retry briefly.
            for (var attempt = 0; attempt < 3; attempt++)
            {
                var text = await _clipboard.GetTextAsync();
                if (FacebookUrl.TryParse(text, out var uri) && uri is not null)
                {
                    LinkText = uri.AbsoluteUri;
                    await DownloadAsync(uri);
                    return;
                }
                if (attempt < 2) await Task.Delay(250);
            }

            Status = HasLink
                ? "Link is ready. Tap Download to try again."
                : "No Facebook link found. Copy a link, then tap Paste link.";
        }
        catch (Exception ex)
        {
            Status = $"Clipboard access was unavailable. Tap Paste link to try manually. ({ex.Message})";
        }
    }

    private async Task PasteClipboardAsync()
    {
        try
        {
            var text = await _clipboard.GetTextAsync();
            if (FacebookUrl.TryParse(text, out var uri) && uri is not null)
            {
                LinkText = uri.AbsoluteUri;
                Status = "Facebook link detected. Tap Download to continue.";
            }
            else
            {
                Status = "Clipboard has no recognized Facebook video link. You can paste or type one below.";
            }
        }
        catch (Exception ex)
        {
            Status = $"Could not read clipboard. Paste the link into the field instead. ({ex.Message})";
        }
    }

    private async Task DownloadLinkAsync()
    {
        if (IsBusy) return;
        if (!FacebookUrl.TryParse(LinkText, out var uri) || uri is null)
        {
            Status = "Enter a valid facebook.com or fb.watch link first.";
            return;
        }
        await DownloadAsync(uri);
    }

    private async Task DownloadAsync(Uri uri)
    {
        if (IsBusy) return;
        if (Downloads.Any(x => x.SourceUrl == uri.AbsoluteUri && File.Exists(x.FilePath)))
        {
            Status = "This video is already in your library.";
            return;
        }

        IsBusy = true;
        _downloadCts = new CancellationTokenSource();
        Progress = 0;
        Status = "Link detected. Preparing video…";
        try
        {
            var reporter = new Progress<double>(value => Progress = value);
            var path = await _downloads.DownloadFacebookVideoAsync(uri, reporter, _downloadCts.Token);
            _savedPath = path;
            var info = new FileInfo(path);
            var record = new VideoDownloadRecord(Guid.NewGuid(), uri.AbsoluteUri, path,
                Path.GetFileName(path), info.Length, DateTimeOffset.UtcNow);
            await _library.AddAsync(record, _downloadCts.Token);
            Downloads.Insert(0, record);
            Status = $"Saved {record.FileName} ({FormatSize(record.SizeBytes)}).";
            OnPropertyChanged(nameof(CanExport));
        }
        catch (OperationCanceledException) { Status = "Download cancelled. Tap Download to retry."; }
        catch (NotSupportedException ex) { Status = ex.Message; }
        catch (Exception ex) { Status = $"Download failed: {ex.Message}"; }
        finally
        {
            _downloadCts.Dispose();
            _downloadCts = null;
            IsBusy = false;
        }
    }

    private async Task ExportAsync()
    {
        if (!CanExport || _savedPath is null) return;
        try { await _export.ShareAsync(_savedPath); }
        catch (Exception ex) { Status = $"Could not share video: {ex.Message}"; }
    }

    private async Task OpenRecordAsync(VideoDownloadRecord? record)
    {
        if (record is null || !File.Exists(record.FilePath)) { Status = "That file is no longer available."; return; }
        try { await Launcher.Default.OpenAsync(new OpenFileRequest(record.FileName, new ReadOnlyFile(record.FilePath))); }
        catch (Exception ex) { Status = $"Could not open the video: {ex.Message}"; }
    }

    private async Task ShareRecordAsync(VideoDownloadRecord? record)
    {
        if (record is null || !File.Exists(record.FilePath)) { Status = "That file is no longer available."; return; }
        try { await _export.ShareAsync(record.FilePath); }
        catch (Exception ex) { Status = $"Could not share the video: {ex.Message}"; }
    }

    private async Task DeleteRecordAsync(VideoDownloadRecord? record)
    {
        if (record is null) return;
        try
        {
            await _library.DeleteAsync(record.Id);
            Downloads.Remove(record);
            if (File.Exists(record.FilePath)) File.Delete(record.FilePath);
            if (_savedPath == record.FilePath) { _savedPath = null; OnPropertyChanged(nameof(CanExport)); }
            Status = $"Deleted {record.FileName}.";
        }
        catch (Exception ex) { Status = $"Could not delete the video: {ex.Message}"; }
    }

    private static string FormatSize(long size) => size < 1024 * 1024
        ? $"{size / 1024d:0} KB" : $"{size / (1024d * 1024):0.0} MB";
}
