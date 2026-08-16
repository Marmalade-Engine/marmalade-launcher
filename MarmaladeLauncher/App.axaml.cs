using System.Globalization;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using MarmaladeLauncher.Services;
using MarmaladeLauncher.ViewModels;
using MarmaladeLauncher.Views;

namespace MarmaladeLauncher;

public partial class App : Application {
    public override void Initialize() {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted() {
        var settingsService = new SettingsService();
        settingsService.EnsureDirectoriesExist();

        var localisationService = new LocalisationService(settingsService);
    
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            desktop.MainWindow = new MainWindow {
                DataContext = new MainViewModel(settingsService, localisationService),
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}