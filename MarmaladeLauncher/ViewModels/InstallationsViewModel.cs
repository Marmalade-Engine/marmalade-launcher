using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Platform.Storage;
using Common.Assets.Localisation;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;
using MarmaladeLauncher.Services.ResourceManagement.Common;
using MarmaladeLauncher.Services.ResourceManagement.Linux;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace MarmaladeLauncher.ViewModels;

public partial class InstallationsViewModel : ViewModelBase {
    private readonly InstallationRegistryService _installationRegistryService;
    private readonly SettingsService _settingsService;
    public LauncherSettings Settings => _settingsService.Settings;
    private readonly LaunchService _launchService;
    private readonly EngineInstallerService _engineInstallerService;

    private readonly Dictionary<LocalEngineInstallation, RemoteBuildEntry> _versionToEntryMap = new();

    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private double _downloadProgress;

    private List<LocalEngineInstallation> _allAvailableEngineInstallations = new();

    public bool IsLinux => OperatingSystem.IsLinux();
    public bool IsMacOS => OperatingSystem.IsMacOS();

    [ObservableProperty] private ObservableCollection<LocalEngineInstallation> _installations = new();
    [ObservableProperty] private ObservableCollection<LocalEngineInstallation> _engineInstallations = new();
    [ObservableProperty] private bool _hasInstallations;
    [ObservableProperty] private bool _engineVersionsAvailable = true;
    [ObservableProperty] private bool _isInstallModalOpen;
    [ObservableProperty] private bool _isSettingsModalOpen;
    [ObservableProperty] private LocalEngineInstallation? _selectedInstallation;
    [ObservableProperty] private LocalEngineInstallation? _selectedEngineToInstall;
    [ObservableProperty] private bool _allowDevBuilds;
    [ObservableProperty] private bool _showDevBuilds = true;
    [ObservableProperty] private InstallScope _selectedInstallScope = InstallScope.InstallScope_USER;
    [ObservableProperty] private string? _customInstallDirectory;

    public IReadOnlyList<LinuxPackageType> LinuxPackageTypes { get; } =
        Enum.GetValues<LinuxPackageType>();

    public IReadOnlyList<MacPackageType> MacPackageTypes { get; } =
        Enum.GetValues<MacPackageType>();

    public int PreferredLinuxPackageIndex {
        get => (int)Settings.PreferredLinuxPackageType;
        set {
            if (value >= 0) {
                Settings.PreferredLinuxPackageType = (LinuxPackageType)value;
                _settingsService.SaveSettings(Settings);
                OnPropertyChanged();
            }
        }
    }

    public int PreferredMacPackageIndex {
        get => (int)Settings.PreferredMacPackageType;
        set {
            if (value >= 0) {
                Settings.PreferredMacPackageType = (MacPackageType)value;
                OnPropertyChanged();
            }
        }
    }

    public InstallationsViewModel(
        InstallationRegistryService installationRegistryService,
        SettingsService settingsService,
        LaunchService launchService,
        EngineInstallerService engineInstallerService) {
        _installationRegistryService = installationRegistryService;
        _settingsService = settingsService;
        _launchService = launchService;
        _engineInstallerService = engineInstallerService;

        _settingsService.LoadSettings();
        AllowDevBuilds = _settingsService.Settings.EnableDevBuilds;

        _ = LoadData();
    }

    public InstallationsViewModel() : this(
        CreateAndLoadInstallationService(),
        CreateAndLoadSettingsService(),
        CreateLaunchService(),
        CreateEngineInstallerService()) {
    }

    private static SettingsService CreateAndLoadSettingsService() {
        var service = new SettingsService();
        service.LoadSettings();
        return service;
    }

    private static InstallationRegistryService CreateAndLoadInstallationService() {
        return new InstallationRegistryService();
    }

    public int SelectedInstallScopeIndex {
        get => (int)SelectedInstallScope;
        set => SelectedInstallScope = (InstallScope)value;
    }

    partial void OnSelectedInstallScopeChanged(InstallScope value) {
        OnPropertyChanged(nameof(SelectedInstallScopeIndex));
    }

    public bool HasSelectedEngineFeatures =>
        SelectedEngineToInstall?.Features != null && SelectedEngineToInstall.Features.Count > 0;

    public bool HasSelectedEngineChangelog =>
        SelectedEngineToInstall?.Changelog != null && SelectedEngineToInstall.Changelog.Count > 0;

    partial void OnSelectedEngineToInstallChanged(LocalEngineInstallation? value) {
        OnPropertyChanged(nameof(HasSelectedEngineFeatures));
        OnPropertyChanged(nameof(HasSelectedEngineChangelog));
    }

    [RelayCommand]
    private async Task BrowseCustomInstallDirectory() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider == null) return;

            var options = new FolderPickerOpenOptions {
                Title = "Select Custom Installation Directory",
                AllowMultiple = false
            };

            var folders = await topLevel.StorageProvider.OpenFolderPickerAsync(options);
            if (folders.Count > 0) {
                CustomInstallDirectory = folders[0].Path.LocalPath;
            }
        }
    }

    private static PlatformEngineResolver CreatePlatformEngineResolver() {
        var installEngines = new List<IInstallEngine> { new InstallEngineLinux() };
        var uninstallEngines = new List<IUninstallEngine> { new UninstallEngineLinux() };
        var launchEngines = new List<ILaunchEngine> { new LaunchEngineLinux() };

        return new PlatformEngineResolver(installEngines, uninstallEngines, launchEngines);
    }

    private static LaunchService CreateLaunchService() {
        return new LaunchService(CreateAndLoadSettingsService(), CreatePlatformEngineResolver());
    }

    private static EngineInstallerService CreateEngineInstallerService() {
        var installEngines = new List<IInstallEngine> {
            new InstallEngineLinux()
        };

        var uninstallEngines = new List<IUninstallEngine> {
            new UninstallEngineLinux()
        };

        var launchEngines = new List<ILaunchEngine>() {
            new LaunchEngineLinux()
        };

        var resolver = new PlatformEngineResolver(installEngines, uninstallEngines, launchEngines);
        var downloader = new FileDownloader();

        return new EngineInstallerService(
            CreateAndLoadInstallationService(),
            CreateAndLoadSettingsService(),
            downloader,
            resolver
        );
    }

    private async Task LoadData() {
        var list = await _installationRegistryService.LoadInstallations();

        foreach (var installation in list) {
            installation.RefreshValidation();
        }

        Installations = new ObservableCollection<LocalEngineInstallation>(list);

        var (remoteInstallations, map) =
            await _installationRegistryService.FetchEngineVersionsAsync(_settingsService.Settings);
        _allAvailableEngineInstallations = remoteInstallations;

        _versionToEntryMap.Clear();
        foreach (var kvp in map) {
            _versionToEntryMap[kvp.Key] = kvp.Value;
        }

        ApplyEngineFilter();
        UpdateState();
    }

    partial void OnShowDevBuildsChanged(bool value) {
        ApplyEngineFilter();
    }

    private void UpdateState() {
        HasInstallations = Installations.Count > 0;
        EngineVersionsAvailable = EngineInstallations.Count > 0;
        AllowDevBuilds = _settingsService.Settings.EnableDevBuilds;
    }

    [RelayCommand]
    private async Task InstallEngine() {
        if (SelectedEngineToInstall == null ||
            !_versionToEntryMap.TryGetValue(SelectedEngineToInstall, out var entry)) {
            return;
        }

        IsInstalling = true;
        var progress = new Progress<double>(val => DownloadProgress = val);

        string targetDirectory = !string.IsNullOrWhiteSpace(_settingsService.Settings.DefaultInstallLocation)
            ? _settingsService.Settings.DefaultInstallLocation
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                SettingsService.DefaultBaseDirectory,
                "installations");

        try {
            var newInstall = await _engineInstallerService.InstallEngine(entry, targetDirectory, progress);
            if (newInstall != null) {
                Installations.Add(newInstall);
                UpdateState();
            }
        }
        catch (Exception e) {
            Debug.WriteLine($"Installation failed: {e.Message}");
        }
        finally {
            IsInstalling = false;
            IsInstallModalOpen = false;
            SelectedEngineToInstall = null;
        }
    }

    [RelayCommand]
    private async Task LocateExistingInstallation() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider == null) return;

            var fileTypes = new List<FilePickerFileType>();

            if (OperatingSystem.IsMacOS()) {
                fileTypes.Add(new FilePickerFileType("macOS Applications") {
                    AppleUniformTypeIdentifiers = new[] { "com.apple.application-bundle", "com.apple.executable" }
                });
            }
            else {
                fileTypes.Add(new FilePickerFileType("Executables") {
                    Patterns = new[] { "*.exe", "*.AppImage", "*.appimage" },
                    MimeTypes = new[] { "application/x-executable", "application/x-appimage" }
                });
            }

            fileTypes.Add(FilePickerFileTypes.All);

            var options = new FilePickerOpenOptions {
                Title = Resources.Installation_Locate_FilePicker,
                AllowMultiple = false,
                FileTypeFilter = fileTypes
            };

            var result = await topLevel.StorageProvider.OpenFilePickerAsync(options);

            if (result.Count > 0) {
                string fullPath = result[0].Path.LocalPath;
                await RegisterInstallation(fullPath);
            }
        }
    }

    private async Task RegisterInstallation(string fullPath) {
        string executablePath = fullPath;

        if (OperatingSystem.IsMacOS() && fullPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) {
            string bundleName = Path.GetFileNameWithoutExtension(fullPath);
            string innerBinary = Path.Combine(fullPath, "Contents", "MacOS", bundleName);

            if (File.Exists(innerBinary)) {
                executablePath = innerBinary;
            }
        }

        bool isAlreadyRegistered = Installations.Any(i =>
            i.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase));

        if (isAlreadyRegistered) {
            return;
        }

        string displayName = Path.GetFileNameWithoutExtension(fullPath);

        var newEngine = new LocalEngineInstallation {
            Name = displayName,
            ExecutablePath = executablePath,
            DateAdded = DateTime.Now,
        };

        Installations.Add(newEngine);
        UpdateState();

        await _installationRegistryService.SaveInstallations(Installations);
    }

    [RelayCommand]
    private async Task RemoveInstallation(LocalEngineInstallation item) {
        if (item == null) return;

        Installations.Remove(item);
        UpdateState();

        await _installationRegistryService.SaveInstallations(Installations);
    }

    [RelayCommand]
    private async Task LaunchInstallation(LocalEngineInstallation item) {
        if (item == null) return;

        item.RefreshValidation();
        if (!item.IsExecutableValid) {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null) {
                string resolvedPath = item.GetResolvedExecutablePath(_settingsService.Settings.DefaultInstallLocation);
                var box = MessageBoxManager.GetMessageBoxStandard(
                    Resources.Installations_MissingExecutable_Prompt_Title,
                    string.Format(Resources.Installations_MissingExecutable_Prompt_Body, item.Name, resolvedPath),
                    ButtonEnum.Ok);
                await box.ShowWindowDialogAsync(desktop.MainWindow);
            }

            return;
        }

        await _launchService.LaunchAsync(item, onPostLaunch: ExecutePostLaunchBehaviorAsync);
    }

    private async Task ExecutePostLaunchBehaviorAsync() {
        _settingsService.LoadSettings();

        var behavior = _settingsService.Settings.PostLaunchBehaviour;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            switch (behavior) {
                case PostLaunchBehaviour.PostLaunchBehaviour_MINIMISE:
                    if (desktop.MainWindow != null) {
                        desktop.MainWindow.WindowState = WindowState.Minimized;
                    }

                    break;

                case PostLaunchBehaviour.PostLaunchBehaviour_CLOSE:
                    await Task.Delay(200);
                    desktop.Shutdown();
                    break;

                case PostLaunchBehaviour.PostLaunchBehaviour_KEEPOPEN:
                default:
                    break;
            }
        }
    }

    [RelayCommand]
    private void OpenSettings(LocalEngineInstallation item) {
        if (item == null) return;
        SelectedInstallation = item;
        IsSettingsModalOpen = true;
    }

    [RelayCommand]
    private async Task CloseSettings() {
        IsSettingsModalOpen = false;
        SelectedInstallation = null;
        await _installationRegistryService.SaveInstallations(Installations);
    }

    [RelayCommand]
    private void OpenInstall() {
        _settingsService.LoadSettings();
        AllowDevBuilds = _settingsService.Settings.EnableDevBuilds;
        ApplyEngineFilter();

        IsInstallModalOpen = true;
    }

    [RelayCommand]
    private void CloseInstall() {
        IsInstallModalOpen = false;
        SelectedEngineToInstall = null;
    }

    [RelayCommand]
    private async Task UninstallConfirmation(LocalEngineInstallation? item) {
        if (item == null) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null) {
            bool confirmed = await ShowConfirmationDialog(
                desktop.MainWindow,
                Resources.Modal_Installation_Uninstall_Prompt_Title,
                string.Format(Resources.Modal_Installation_Uninstall_Prompt_Body, item.Name)
            );

            if (confirmed) {
                IsSettingsModalOpen = false;

                await _engineInstallerService.UninstallEngine(item);

                Installations.Remove(item);
                UpdateState();
            }
        }
    }

    private async Task<bool> ShowConfirmationDialog(Window parent, string title, string message) {
        var dialog = MessageBoxManager.GetMessageBoxStandard(title, message, ButtonEnum.YesNo);
        var result = await dialog.ShowWindowDialogAsync(parent);
        return result == ButtonResult.Yes;
    }

    private void ApplyEngineFilter() {
        var filteredList = _allAvailableEngineInstallations
            .Where(x => {
                bool isDev = x.Branch.Equals("dev", StringComparison.OrdinalIgnoreCase);
                return (AllowDevBuilds && ShowDevBuilds) || !isDev;
            })
            .ToList();

        EngineInstallations = new ObservableCollection<LocalEngineInstallation>(filteredList);
        EngineVersionsAvailable = EngineInstallations.Count > 0;

        if (SelectedEngineToInstall != null && !EngineInstallations.Contains(SelectedEngineToInstall)) {
            SelectedEngineToInstall = null;
        }
    }
}