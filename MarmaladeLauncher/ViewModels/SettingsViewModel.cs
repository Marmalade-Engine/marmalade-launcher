using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.ViewModels;

public record PostLaunchOption(PostLaunchBehaviour Value, string DisplayName);

public partial class SettingsViewModel : ViewModelBase {
    private readonly SettingsService _settingsService;

    [ObservableProperty]
    private string _defaultInstallLocation = string.Empty;

    [ObservableProperty]
    private PostLaunchOption _selectedPostLaunchOption = null!;
    
    [ObservableProperty]
    private bool _isDirty;

    public string DefaultPathPlaceholder { get; } = SettingsService.DefaultBaseDirectory;

    public List<PostLaunchOption> PostLaunchOptions { get; } = new() {
        new(PostLaunchBehaviour.PostLaunchBehaviour_KEEPOPEN, "Keep launcher open"),
        new(PostLaunchBehaviour.PostLaunchBehaviour_MINIMISE, "Minimise launcher"),
        new(PostLaunchBehaviour.PostLaunchBehaviour_CLOSE, "Close launcher")
    };
    
    public SettingsViewModel(SettingsService settingsService) {
        _settingsService = settingsService;
        ResetToSaved();
    }

    public SettingsViewModel() : this(new SettingsService()) { }

    partial void OnDefaultInstallLocationChanged(string value) {
        CheckDirtyState();
    }
    
    partial void OnSelectedPostLaunchOptionChanged(PostLaunchOption value) => CheckDirtyState();
    
    private void CheckDirtyState() {
        if (_settingsService?.Settings == null) return;

        bool isLocationDirty = DefaultInstallLocation != _settingsService.Settings.DefaultInstallLocation;
        bool isBehaviorDirty = SelectedPostLaunchOption?.Value != _settingsService.Settings.PostLaunchBehaviour;

        IsDirty = isLocationDirty || isBehaviorDirty;
    }

    [RelayCommand]
    private void ResetToDefault() {
        DefaultInstallLocation = SettingsService.DefaultBaseDirectory;
    }

    [RelayCommand]
    private void Cancel() {
        ResetToSaved();
    }

    [RelayCommand]
    private void ResetToSaved() {
        var savedBehaviour = _settingsService.Settings.PostLaunchBehaviour;

        SelectedPostLaunchOption = PostLaunchOptions.FirstOrDefault(o => o.Value == savedBehaviour) 
                                   ?? PostLaunchOptions[0];

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
            DefaultInstallLocation = DefaultInstallLocation,
            PostLaunchBehaviour = SelectedPostLaunchOption.Value
        };

        await _settingsService.SaveSettings(updatedSettings);
        IsDirty = false;
    }
}