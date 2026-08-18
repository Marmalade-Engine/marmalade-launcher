using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using System.Threading.Tasks;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using MarmaladeLauncher.Models;
using MarmaladeLauncher.Services;
using MarmaladeLauncher.Views.Dialogs;
using MsBox.Avalonia;
using MsBox.Avalonia.Enums;

namespace MarmaladeLauncher.ViewModels;

public partial class InstallationsViewModel : ViewModelBase {
    private readonly InstallationService _installationService;
    private readonly SettingsService _settingsService;
    private readonly LaunchService _launchService;
    private readonly InstallService _installService;

    private readonly Dictionary<EngineInstallation, InstallationEntry> _versionToEntryMap = new();

    [ObservableProperty] private bool _isInstalling;
    [ObservableProperty] private double _downloadProgress;

    private List<EngineInstallation> _allAvailableEngineInstallations = new();

    [ObservableProperty] private ObservableCollection<EngineInstallation> _installations = new();

    [ObservableProperty] private ObservableCollection<EngineInstallation> _engineInstallations = new();

    [ObservableProperty] private bool _hasInstallations;

    [ObservableProperty] private bool _engineVersionsAvailable = true;

    [ObservableProperty] private bool _isInstallModalOpen;

    [ObservableProperty] private bool _isSettingsModalOpen;
    [ObservableProperty] private EngineInstallation? _selectedInstallation;

    [ObservableProperty] private EngineInstallation? _selectedEngineToInstall;

    [ObservableProperty] private bool _allowDevBuilds;

    [ObservableProperty] private bool _showDevBuilds = true;

    private string EngineDownloadsURI =>
        $"https://www.ryanbester.com/download?product=marmalade-engine&branch=dev&platform={GetCurrentPlatform()}&list";

    public InstallationsViewModel(InstallationService installationService, SettingsService settingsService,
        LaunchService launchService, InstallService installService) {
        _installationService = installationService;
        _settingsService = settingsService;
        _launchService = launchService;
        _installService = installService;

        _settingsService.LoadSettings();
        AllowDevBuilds = _settingsService.Settings.EnableDevBuilds;

        _ = LoadData();
    }

    public InstallationsViewModel() : this(
        CreateAndLoadInstallationService(),
        CreateAndLoadSettingsService(),
        new LaunchService(CreateAndLoadSettingsService()),
        new InstallService(CreateAndLoadInstallationService(), CreateAndLoadSettingsService())) { }

    private static SettingsService CreateAndLoadSettingsService() {
        var service = new SettingsService();
        service.LoadSettings();
        return service;
    }

    private static InstallationService CreateAndLoadInstallationService() {
        return new InstallationService();
    }

    private async Task LoadData() {
        var list = await _installationService.LoadInstallations();

        foreach (var installation in list) {
            installation.RefreshValidation();
        }

        Installations = new ObservableCollection<EngineInstallation>(list);
        _allAvailableEngineInstallations = await FetchEngineVersions();

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

    /// <summary>
    /// Downloads and installs the currently selected engine
    /// </summary>
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
            var newInstall = await _installService.InstallEngine(entry, targetDirectory, progress);
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

    /// <summary>
    /// Prompts user to select an existing engine installation with native file picker
    /// </summary>
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
                Title = "Locate existing installation",
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

    /// <summary>
    /// Validates, formats, and registers the local installation into the collection
    /// </summary>
    /// <param name="fullPath"></param>
    private async Task RegisterInstallation(string fullPath) {
        string executablePath = fullPath;

        // on macos resolve the target path to the exec binary
        if (OperatingSystem.IsMacOS() && fullPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) {
            string bundleName = Path.GetFileNameWithoutExtension(fullPath);
            string innerBinary = Path.Combine(fullPath, "Contents", "MacOS", bundleName);

            if (File.Exists(innerBinary)) {
                executablePath = innerBinary;
            }
        }

        // prevent duplicate entries
        bool isAlreadyRegistered = Installations.Any(i => 
            i.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase));

        if (isAlreadyRegistered) {
            return;
        }

        string displayName = Path.GetFileNameWithoutExtension(fullPath);

        var newEngine = new EngineInstallation {
            Name = displayName,
            ExecutablePath = executablePath,
            DateAdded = DateTime.Now,
        };

        Installations.Add(newEngine);
        UpdateState();

        await _installationService.SaveInstallations(Installations);
    }

    /// <summary>
    /// Removes local engine installation entry in collection
    /// </summary>
    /// <param name="item"></param>
    [RelayCommand]
    private async Task RemoveInstallation(EngineInstallation item) {
        if (item == null) return;

        Installations.Remove(item);
        UpdateState();

        await _installationService.SaveInstallations(Installations);
    }
    
    /// <summary>
    /// Validates and launches a target engine installation
    /// </summary>
    /// <param name="item"></param>
    [RelayCommand]
    private async Task LaunchInstallation(EngineInstallation item) {
        if (item == null) return;

        item.RefreshValidation();
        if (!item.IsExecutableValid) {
            if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
                && desktop.MainWindow != null) {
                string resolvedPath = item.GetResolvedExecutablePath(_settingsService.Settings.DefaultInstallLocation);
                var box = MessageBoxManager.GetMessageBoxStandard(
                    "Executable Missing",
                    $"Cannot launch {item.Name}. The executable was moved or deleted:\n{resolvedPath}",
                    ButtonEnum.Ok);
                await box.ShowWindowDialogAsync(desktop.MainWindow);
            }

            return;
        }

        await _launchService.LaunchAsync(item, onPostLaunch: ExecutePostLaunchBehaviorAsync);
    }

    /// <summary>
    /// Processes the post-engine-launch behaviour of the engine 
    /// </summary>
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
    private void OpenSettings(EngineInstallation item) {
        if (item == null) return;
        SelectedInstallation = item;
        IsSettingsModalOpen = true;
    }

    [RelayCommand]
    private async Task CloseSettings() {
        IsSettingsModalOpen = false;
        SelectedInstallation = null;
        await _installationService.SaveInstallations(Installations);
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
    private async Task UninstallConfirmation(EngineInstallation? item) {
        if (item == null) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow != null) {
            bool confirmed = await ShowConfirmationDialog(
                desktop.MainWindow,
                "Confirm Uninstallation",
                $"Are you sure you want to uninstall {item.Name}?"
            );

            if (confirmed) {
                IsSettingsModalOpen = false;
                await _installService.UninstallEngineAsync(item);

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

    private async Task<List<EngineInstallation>> FetchEngineVersions() {
        string currentPlatform = GetCurrentPlatform();
        string requestUri = EngineDownloadsURI;

        try {
            using (var client = new HttpClient()) {
                client.Timeout = TimeSpan.FromSeconds(10);
                client.DefaultRequestHeaders.UserAgent.ParseAdd("MarmaladeLauncher/1.0");

                string jsonResponse = await client.GetStringAsync(requestUri);
                var installations = new List<EngineInstallation>();
                var jsonNode = JsonNode.Parse(jsonResponse);

                if (jsonNode?["builds"] is JsonArray buildsArray) {
                    var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                    var entries = buildsArray.Deserialize<List<InstallationEntry>>(serializerOptions) ?? new();

                    foreach (var entry in entries) {
                        if (!string.IsNullOrEmpty(entry.url) &&
                            !entry.url.Contains($"platform={currentPlatform}", StringComparison.OrdinalIgnoreCase)) {
                            continue;
                        }

                        string branch = (!string.IsNullOrEmpty(entry.url) &&
                                         entry.url.Contains("branch=dev", StringComparison.OrdinalIgnoreCase))
                            ? "dev"
                            : "release";

                        string resolvedVersion = string.IsNullOrWhiteSpace(entry.version)
                            ? entry.id.ToString()
                            : entry.version;

                        var installation = new EngineInstallation() {
                            Name = resolvedVersion,
                            Version = resolvedVersion,
                            InstallSize = entry.size,
                            Branch = branch
                        };

                        installations.Add(installation);
                        _versionToEntryMap[installation] = entry;
                    }
                }

                return installations;
            }
        }
        catch (Exception ex) {
            Debug.WriteLine($"Error fetching versions: {ex.Message}");
            return new List<EngineInstallation>();
        }
    }

    /// <summary>
    /// Filter list of engine entries
    /// </summary>
    private void ApplyEngineFilter() {
        var filteredList = _allAvailableEngineInstallations
            .Where(x => {
                bool isDev = x.Branch.Equals("dev", StringComparison.OrdinalIgnoreCase);
                return (AllowDevBuilds && ShowDevBuilds) || !isDev;
            })
            .ToList();

        EngineInstallations = new ObservableCollection<EngineInstallation>(filteredList);
        EngineVersionsAvailable = EngineInstallations.Count > 0;

        if (SelectedEngineToInstall != null && !EngineInstallations.Contains(SelectedEngineToInstall)) {
            SelectedEngineToInstall = null;
        }
    }

    /// <summary>
    /// Returns engine download suffix depending on current operating system
    /// </summary>
    /// <returns></returns>
    private static string GetCurrentPlatform() {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos-arm";
        if (OperatingSystem.IsLinux()) return "linux";
        return "windows";
    }
}