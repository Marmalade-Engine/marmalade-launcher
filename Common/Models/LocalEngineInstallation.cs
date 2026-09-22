using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Models;

public partial class LocalEngineInstallation : ObservableObject {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    [NotifyPropertyChangedFor(nameof(IsExecutableValid))]
    private string _executablePath = string.Empty;

    public long InstallSize { get; set; }
    public DateTime DateAdded { get; set; }
    public string Branch { get; set; } = "main";
    public string? Arguments { get; set; }

    public ObservableCollection<string> Features { get; set; } = new();
    public ObservableCollection<string> Changelog { get; set; } = new();

    [JsonIgnore]
    public bool HasFeatures => Features != null && Features.Any();

    [JsonIgnore]
    public bool HasChangelog => Changelog != null && Changelog.Any();

    [JsonIgnore]
    public bool IsMissing {
        get {
            if (string.IsNullOrWhiteSpace(ExecutablePath)) return true;

            string resolvedPath = GetResolvedExecutablePath(null);
            return !File.Exists(resolvedPath) && !Directory.Exists(resolvedPath);
        }
    }

    public bool IsExecutableValid => !IsMissing;

    public string FormattedInstallSize => ByteFormatter.FormatSize(InstallSize);

    public void RefreshValidation() {
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsExecutableValid));
    }

    public string GetResolvedExecutablePath(string? defaultInstallLocation = null) {
        if (OperatingSystem.IsMacOS() && ExecutablePath != string.Empty && !Path.IsPathRooted(ExecutablePath)) {
            string potentialAppBundlePath = Path.Combine(defaultInstallLocation ?? "", ExecutablePath);

            if (Directory.Exists(potentialAppBundlePath) &&
                potentialAppBundlePath.EndsWith(".app", StringComparison.OrdinalIgnoreCase)) {
                string macOsDir = Path.Combine(potentialAppBundlePath, "Contents", "MacOS");
                if (Directory.Exists(macOsDir)) {
                    var files = Directory.GetFiles(macOsDir);
                    if (files.Length > 0) {
                        return Path.Combine(macOsDir, files[0]);
                    }
                }
            }
        }

        if (Path.IsPathRooted(ExecutablePath)) {
            return ExecutablePath;
        }

        string baseDir = !string.IsNullOrWhiteSpace(defaultInstallLocation)
            ? defaultInstallLocation
            : Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "marmalade-launcher",
                "installations");

        string fullPath = Path.Combine(baseDir, ExecutablePath);

        if (OperatingSystem.IsMacOS() && !File.Exists(fullPath)) {
            string macOsDir = Path.Combine(fullPath, "Contents", "MacOS");
            if (Directory.Exists(macOsDir)) {
                var files = Directory.GetFiles(macOsDir);
                if (files.Length > 0) {
                    return Path.Combine(macOsDir, files[0]);
                }
            }
        }

        return fullPath;
    }
}