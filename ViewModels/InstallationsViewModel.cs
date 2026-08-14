using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Linq;
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

namespace MarmaladeLauncher.ViewModels;

public partial class InstallationsViewModel : ViewModelBase {
    private readonly InstallationService _installService;

    [ObservableProperty] private ObservableCollection<EngineInstallation> _installations = new();
    
    [ObservableProperty]
    private bool _hasInstallations;
    
    public InstallationsViewModel(InstallationService installService) {
        _installService = installService;
        _ = LoadData();
    }

    public InstallationsViewModel() : this(new InstallationService()) { }        
    
    private async Task LoadData() {
        var list = await _installService.LoadInstallations();
        Installations = new ObservableCollection<EngineInstallation>(list);
        UpdateState();
    }

    private void UpdateState() {
        HasInstallations = Installations.Count > 0;
    }

    [RelayCommand]
    private async Task LocateExistingInstallation() {
        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var topLevel = TopLevel.GetTopLevel(desktop.MainWindow);
            if (topLevel?.StorageProvider == null) return;

            var fileTypes = new List<FilePickerFileType>();

            // I am having difficulty with having the system allow the user to select the *.app file and it go from there
            // currently you need to browse to the marmalade executable inside the .app file inside finder and drag and
            // drop it into the file picker window
            if (OperatingSystem.IsMacOS()) {
                fileTypes.Add(new FilePickerFileType("macOS Applications") {
                    AppleUniformTypeIdentifiers = new[] { "com.apple.application-bundle", "com.apple.executable" },
                    Patterns = new[] { "*.app" }
                });
            } else {
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
            DateAdded = DateTime.Now
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
    private void LaunchInstallation(EngineInstallation item) {
        if (item == null || string.IsNullOrWhiteSpace(item.ExecutablePath))
            return;

        try {
            var startInfo = new ProcessStartInfo {
                FileName = item.ExecutablePath,
                Arguments = item.Arguments ?? string.Empty,
                UseShellExecute = true,
            };
            
            Process.Start(startInfo);
            Console.WriteLine($"[Launch] Started '{item.Name}' with args: '{item.Arguments}'");
        }
        catch (Exception e) {
            Console.WriteLine($"[Launch] Error launching executable: {e.Message}");
        }
    }

    [RelayCommand]
    private async Task OpenSettings(EngineInstallation item) {
        if (item == null) return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop) {
            var dialog = new InstallationSettingsWindow {
                DataContext = item
            };

            await dialog.ShowDialog(desktop.MainWindow);

            await _installService.SaveInstallations(Installations);
        }
    }
}