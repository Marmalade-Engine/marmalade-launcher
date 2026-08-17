using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarmaladeLauncher.Assets.Localisation;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.ViewModels;

public record PostLaunchOption(PostLaunchBehaviour Value) {
    public string DisplayName => Value switch {
        PostLaunchBehaviour.PostLaunchBehaviour_KEEPOPEN => Resources.Settings_PostLaunch_KeepOpen,
        PostLaunchBehaviour.PostLaunchBehaviour_MINIMISE => Resources.Settings_PostLaunch_Minimise,
        PostLaunchBehaviour.PostLaunchBehaviour_CLOSE => Resources.Settings_PostLaunch_Close,
        _ => Value.ToString()
    };
}

public partial class SettingsViewModel : ViewModelBase {
    private readonly SettingsService _settingsService;
    private readonly LocalisationService _localisationService;

    [ObservableProperty]
    private string _defaultInstallLocation = string.Empty;

    [ObservableProperty]
    private PostLaunchOption _selectedPostLaunchOption = null!;
    
    public List<LocalisationEntry.LanguageMetadata> Locales { get; } = LocalisationEntry.LanguageDisplay.languages;
    
    [ObservableProperty]
    private LocalisationEntry.LanguageMetadata? _selectedLocale;
    
    [ObservableProperty]
    private bool _isDirty;

    public string DefaultPathPlaceholder { get; } = SettingsService.DefaultBaseDirectory;

    public List<PostLaunchOption> PostLaunchOptions { get; } = [
        new(PostLaunchBehaviour.PostLaunchBehaviour_KEEPOPEN),
        new(PostLaunchBehaviour.PostLaunchBehaviour_MINIMISE),
        new(PostLaunchBehaviour.PostLaunchBehaviour_CLOSE)
    ];
    
    public SettingsViewModel(SettingsService settingsService, LocalisationService localisationService) {
        _settingsService = settingsService;
        _localisationService = localisationService;
        
        ResetToSaved();
    }

    public SettingsViewModel() : this(new SettingsService(), new LocalisationService(new SettingsService())) { }

    partial void OnDefaultInstallLocationChanged(string value) {
        CheckDirtyState();
    }
    
    partial void OnSelectedPostLaunchOptionChanged(PostLaunchOption value) => CheckDirtyState();

    partial void OnSelectedLocaleChanged(LocalisationEntry.LanguageMetadata? value) {
        if (value != null && _localisationService != null) {
            _localisationService.CurrentLocale = value.LocaleKey;
            
            OnPropertyChanged(nameof(PostLaunchOptions));
            OnPropertyChanged(nameof(Locales));
        }
        CheckDirtyState();
    }

    private void CheckDirtyState() {
        if (_settingsService?.Settings == null) return;

        bool isLocationDirty = DefaultInstallLocation != _settingsService.Settings.DefaultInstallLocation;
        bool isBehaviorDirty = SelectedPostLaunchOption?.Value != _settingsService.Settings.PostLaunchBehaviour;
        bool isLocaleDirty = _selectedLocale?.LocaleKey != _settingsService.Settings.CurrentLocale;
        
        IsDirty = isLocationDirty || isBehaviorDirty || isLocaleDirty;
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
        var savedLocaleKey = _settingsService.Settings.CurrentLocale;
        
        DefaultInstallLocation = _settingsService.Settings.DefaultInstallLocation;
        
        SelectedPostLaunchOption = PostLaunchOptions.FirstOrDefault(o => o.Value == savedBehaviour) 
                                   ?? PostLaunchOptions[0];

        SelectedLocale = Locales.FirstOrDefault(l => l.LocaleKey == savedLocaleKey) 
                         ?? Locales.FirstOrDefault(l => l.LocaleKey == "en-GB");
    
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
            PostLaunchBehaviour = SelectedPostLaunchOption.Value,
            CurrentLocale = _selectedLocale?.LocaleKey ?? "en-GB"
        };
        
        await _settingsService.SaveSettings(updatedSettings);
        IsDirty = false;
    }
}