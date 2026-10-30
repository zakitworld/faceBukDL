using System.Windows.Input;
using faceBukDL.Services;

namespace faceBukDL.ViewModels;

public sealed class MainViewModel : BindableObject
{
    private readonly IVideoDownloadService _downloads;
    private readonly IClipboardService _clipboard;
    private readonly IVideoExportService _export;
    private CancellationTokenSource? _downloadCts;
    private string? _lastUrl;
    private string? _savedPath;
    private string _status = "Copy a Facebook video link, then open this app.";
    private double _progress;
    private bool _isBusy;

    public string Status { get => _status; private set { _status = value; OnPropertyChanged(); } }
    public double Progress { get => _progress; private set { _progress = value; OnPropertyChanged(); } }
    public bool IsBusy { get => _isBusy; private set { _isBusy = value; OnPropertyChanged(); OnPropertyChanged(nameof(CanExport)); } }
    public bool CanExport => !IsBusy && !string.IsNullOrEmpty(_savedPath) && File.Exists(_savedPath);
    public ICommand CheckClipboardCommand { get; }
    public ICommand CancelCommand { get; }
    public ICommand ExportCommand { get; }

    public MainViewModel(IVideoDownloadService downloads, IClipboardService clipboard, IVideoExportService export)
    {
        _downloads = downloads;
        _clipboard = clipboard;
        _export = export;
        CheckClipboardCommand = new Command(async () => await CheckClipboardAsync(), () => !IsBusy);
        CancelCommand = new Command(() => _downloadCts?.Cancel(), () => IsBusy);
        ExportCommand = new Command(async () => await ExportAsync(), () => CanExport);
    }

    public async Task CheckClipboardAsync()
    {
        if (IsBusy) return;
        string? url = null;
        try
        {
            var text = await _clipboard.GetTextAsync();
            if (!FacebookUrl.TryParse(text, out var uri) || uri is null) return;
            url = uri.AbsoluteUri;
            if (url == _lastUrl && _savedPath is not null && File.Exists(_savedPath)) return;

            IsBusy = true;
            _downloadCts = new CancellationTokenSource();
            Progress = 0;
            Status = "Preparing download…";
            var reporter = new Progress<double>(value => Progress = value);
            var path = await _downloads.DownloadFacebookVideoAsync(uri, reporter, _downloadCts.Token);
            _lastUrl = url;
            _savedPath = path;
            Status = $"Saved to app storage: {Path.GetFileName(path)}";
            OnPropertyChanged(nameof(CanExport));
        }
        catch (OperationCanceledException) { Status = "Download cancelled. Copy or check the link to retry."; }
        catch (NotSupportedException ex) { Status = ex.Message; }
        catch (Exception ex) { Status = $"Could not download video: {ex.Message}"; }
        finally
        {
            _downloadCts?.Dispose();
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
}
