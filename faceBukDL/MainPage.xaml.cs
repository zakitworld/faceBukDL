using faceBukDL.ViewModels;

namespace faceBukDL;

public partial class MainPage : ContentPage
{
    private readonly MainViewModel _viewModel;

    public MainPage(MainViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.InitializeAsync();
        await CheckClipboardAsync();
    }

    public Task CheckClipboardAsync() => _viewModel.CheckClipboardAsync();
}
