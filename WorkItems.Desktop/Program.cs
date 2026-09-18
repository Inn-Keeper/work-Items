using Avalonia;
using Avalonia.Controls;
using Avalonia.Themes.Fluent;

namespace WorkItems.Desktop;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args) =>
        AppBuilder.Configure<Application>()
            .UsePlatformDetect()
            .Start((app, _) =>
            {
                app.Styles.Add(new FluentTheme());
                MainWindow.ApplyTheme(app);
                var window = new MainWindow();
                window.Show();
                app.Run(window);
            }, args);
}
