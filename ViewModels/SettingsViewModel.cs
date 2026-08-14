using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.ViewModels;

public partial class SettingsViewModel : ViewModelBase {
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string _defaultInstallLocation = string.Empty;

    [ObservableProperty]
    private bool _isDirty;

    public string DefaultPathPlaceholder { get; } = SettingsService.DefaultBaseDirectory;

    public SettingsViewModel(SettingsService settingsService) {
        _settingsService = settingsService;
        ResetToSaved();
    }

    public SettingsViewModel() : this(new SettingsService()) { }

    partial void OnDefaultInstallLocationChanged(string value) {
        CheckDirtyState();
    }

    private void CheckDirtyState() {
        IsDirty = DefaultInstallLocation != _settingsService.Settings.DefaultInstallLocation;
    }

    [RelayCommand]
    private void ResetToDefault() {
        DefaultInstallLocation = SettingsService.DefaultBaseDirectory;
    }

    [RelayCommand]
    private void Cancel() {
        ResetToSaved();
    }

    private void ResetToSaved() {
        DefaultInstallLocation = _settingsService.Settings.DefaultInstallLocation;
        IsDirty = false;
    }

    [RelayCommand]
    private async Task BrowseFolderAsync() {
        if (Avalonia.Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop 
            && desktop.MainWindow?.StorageProvider is { } storageProvider) {
            
            var folders = await storageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions {
                Title = "Select Installation Directory",
                AllowMultiple = false
            });

            if (folders.Count > 0) {
                DefaultInstallLocation = folders[0].Path.LocalPath;
            }
        }
    }

    [RelayCommand]
    private async Task SaveAsync() {
        var updatedSettings = new LauncherSettings {
            DefaultInstallLocation = DefaultInstallLocation
        };

        await _settingsService.SaveSettings(updatedSettings);
        IsDirty = false;
    }
}