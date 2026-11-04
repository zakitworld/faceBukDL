namespace faceBukDL;

public partial class AppShell : Shell
{
    public AppShell(MainPage mainPage)
    {
        InitializeComponent();
        Items.Add(new ShellContent
        {
            Title = "Home",
            Route = "MainPage",
            Content = mainPage
        });
    }
}
