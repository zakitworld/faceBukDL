namespace faceBukDL;

public partial class App : Application
{
    private readonly AppShell _shell;

    public App(AppShell shell)
    {
        InitializeComponent();
        _shell = shell;
    }

    protected override Window CreateWindow(IActivationState? activationState)
    {
        var window = new Window(_shell);
        window.Resumed += async (_, _) =>
        {
            if (_shell.CurrentPage is MainPage page)
                await page.CheckClipboardAsync();
        };
        return window;
    }
}
