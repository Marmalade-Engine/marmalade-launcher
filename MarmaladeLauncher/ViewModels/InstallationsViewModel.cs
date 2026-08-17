using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net.Http;
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

namespace MarmaladeLauncher.ViewModels;

public partial class InstallationsViewModel : ViewModelBase {
    private readonly InstallationService _installService;
    private readonly SettingsService _settingsService;

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

    private string _engineDownloadsURI =
        "https://www.ryanbester.com/download?product=marmalade-engine&branch=dev&platform=windows&list";

    public InstallationsViewModel(InstallationService installService, SettingsService settingsService) {
        _installService = installService;
        _settingsService = settingsService;
    
        _settingsService.LoadSettings();
        AllowDevBuilds = _settingsService.Settings.EnableDevBuilds;
    
        _ = LoadData();
    }

    public InstallationsViewModel() : this(new InstallationService(), CreateAndLoadSettingsService()) { }

    private static SettingsService CreateAndLoadSettingsService() {
        var service = new SettingsService();
        service.LoadSettings();
        return service;
    }

    private async Task LoadData() {
        var list = await _installService.LoadInstallations();
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

    [RelayCommand]
    private async Task LocateExistingInstallation() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider == null) return;

            var fileTypes = new List<FilePickerFileType>();

            if (OperatingSystem.IsMacOS()) {
                fileTypes.Add(new FilePickerFileType("macOS Applications") {
                    AppleUniformTypeIdentifiers = new[] { "com.apple.application-bundle", "com.apple.executable" },
                    Patterns = new[] { "*.app" }
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

    private async Task RegisterInstallation(string fullPath) {
        string executablePath = fullPath;

        if (OperatingSystem.IsMacOS() && fullPath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) {
            string bundleName = Path.GetFileNameWithoutExtension(fullPath);
            string innerBinary = Path.Combine(fullPath, "Contents", "MacOS", bundleName);

            if (File.Exists(innerBinary)) {
                executablePath = innerBinary;
            }
        }

        if (Installations.Any(i => i.ExecutablePath.Equals(executablePath, StringComparison.OrdinalIgnoreCase)))
            return;

        string displayName = Path.GetFileNameWithoutExtension(fullPath);

        var newEngine = new EngineInstallation {
            Name = displayName,
            ExecutablePath = executablePath,
            DateAdded = DateTime.Now,
        };

        Installations.Add(newEngine);
        UpdateState();

        await _installService.SaveInstallations(Installations);
    }

    [RelayCommand]
    private async Task RemoveInstallation(EngineInstallation item) {
        if (item == null) return;

        Installations.Remove(item);
        UpdateState();

        await _installService.SaveInstallations(Installations);
    }

    [RelayCommand]
    private async Task LaunchInstallation(EngineInstallation item) {
        if (item == null || string.IsNullOrWhiteSpace(item.ExecutablePath))
            return;

        try {
            ProcessStartInfo startInfo;

            if (OperatingSystem.IsMacOS()) {
                startInfo = CreateMacDetachedStartInfo(item);
            }
            else if (OperatingSystem.IsLinux()) {
                startInfo = CreateLinuxDetachedStartInfo(item);
            }
            else {
                startInfo = new ProcessStartInfo {
                    FileName = item.ExecutablePath,
                    Arguments = item.Arguments ?? string.Empty,
                    UseShellExecute = true,
                    WorkingDirectory = Path.GetDirectoryName(item.ExecutablePath) ?? string.Empty
                };
            }

            using (var process = Process.Start(startInfo)) {
                Console.WriteLine($"[Launch] Detached process started for '{item.Name}' with args: '{item.Arguments}'");
            }

            await ExecutePostLaunchBehaviorAsync();
        }
        catch (Exception e) {
            Console.WriteLine($"[Launch] Error launching executable: {e.Message}");
        }
    }

    private ProcessStartInfo CreateMacDetachedStartInfo(EngineInstallation item) {
        string workingDir = Path.GetDirectoryName(item.ExecutablePath) ?? string.Empty;
        string appBundlePath = GetMacAppBundlePath(item.ExecutablePath);

        if (!string.IsNullOrEmpty(appBundlePath)) {
            var args = $"-n \"{appBundlePath}\"";
            if (!string.IsNullOrWhiteSpace(item.Arguments)) {
                args += $" --args {item.Arguments}";
            }

            return new ProcessStartInfo {
                FileName = "/usr/bin/open",
                Arguments = args,
                UseShellExecute = false,
                CreateNoWindow = true,
                WorkingDirectory = workingDir
            };
        }

        string binaryArgs = string.IsNullOrWhiteSpace(item.Arguments) ? "" : $" {item.Arguments}";
        return new ProcessStartInfo {
            FileName = "/bin/zsh",
            Arguments = $"-c \"nohup '{item.ExecutablePath}'{binaryArgs} > /dev/null 2>&1 &\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };
    }

    private ProcessStartInfo CreateLinuxDetachedStartInfo(EngineInstallation item) {
        string workingDir = Path.GetDirectoryName(item.ExecutablePath) ?? string.Empty;
        string binaryArgs = string.IsNullOrWhiteSpace(item.Arguments) ? "" : $" {item.Arguments}";

        return new ProcessStartInfo {
            FileName = "/bin/bash",
            Arguments = $"-c \"nohup '{item.ExecutablePath}'{binaryArgs} > /dev/null 2>&1 &\"",
            UseShellExecute = false,
            CreateNoWindow = true,
            WorkingDirectory = workingDir
        };
    }

    private string GetMacAppBundlePath(string executablePath) {
        if (executablePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase) && Directory.Exists(executablePath)) {
            return executablePath;
        }

        int appIndex = executablePath.IndexOf(".app", StringComparison.OrdinalIgnoreCase);
        if (appIndex != -1) {
            string bundlePath = executablePath.Substring(0, appIndex + 4);
            if (Directory.Exists(bundlePath)) {
                return bundlePath;
            }
        }

        return string.Empty;
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
    private void OpenSettings(EngineInstallation item) {
        if (item == null) return;
        SelectedInstallation = item;
        IsSettingsModalOpen = true;
    }

    [RelayCommand]
    private async Task CloseSettings() {
        IsSettingsModalOpen = false;
        SelectedInstallation = null;
        await _installService.SaveInstallations(Installations);
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
    }

    private async Task<List<EngineInstallation>> FetchEngineVersions() {
        try {
            using (var client = new HttpClient()) {
                client.Timeout = TimeSpan.FromSeconds(10);

                string response = await client.GetStringAsync(_engineDownloadsURI);
                var installations = new List<EngineInstallation>();
                var data = JsonNode.Parse(response);

                if (data is JsonObject jsonObject && jsonObject["builds"] is JsonArray buildsArray) {
                    foreach (var item in buildsArray) {
                        string url = item["url"]?.ToString() ?? string.Empty;

                        string branch = "release";
                        if (url.Contains("branch=dev", StringComparison.OrdinalIgnoreCase)) {
                            branch = "dev";
                        }

                        var installation = new EngineInstallation() {
                            Name = item["id"]?.ToString() ?? "Unknown",
                            Version = item["id"]?.ToString() ?? "0.0.0",
                            InstallSize = int.Parse(item["size"]?.ToString() ?? "0"),
                            Branch = branch
                        };
                        installations.Add(installation);
                    }
                }

                return installations;
            }
        }
        catch (Exception) {
            return new List<EngineInstallation>();
        }
    }

    private void ApplyEngineFilter() {
        var filteredList = _allAvailableEngineInstallations
            .Where(x => (AllowDevBuilds && ShowDevBuilds) || !x.Branch.Equals("dev", StringComparison.OrdinalIgnoreCase))
            .ToList();

        EngineInstallations = new ObservableCollection<EngineInstallation>(filteredList);
        EngineVersionsAvailable = EngineInstallations.Count > 0;
    }
}