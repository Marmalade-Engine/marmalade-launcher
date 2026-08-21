using System;
using System.IO;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Models;

/// <summary>
///     Object used to define an installment of the engine that has been downloaded
/// </summary>
public partial class EngineInstallation : ObservableObject {
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMissing))]
    [NotifyPropertyChangedFor(nameof(IsExecutableValid))]
    private string _executablePath = string.Empty;

    public long InstallSize { get; set; }
    public DateTime DateAdded { get; set; }
    public string Branch { get; set; } = string.Empty;
    public string? Arguments { get; set; }

    /// <summary>
    /// Indicates whether the target executable or directory cannot be found within the filesystem
    /// </summary>
    [JsonIgnore]
    public bool IsMissing {
        get {
            if (string.IsNullOrWhiteSpace(ExecutablePath)) return true;

            string resolvedPath = GetResolvedExecutablePath(null);
            return !File.Exists(resolvedPath) && !Directory.Exists(resolvedPath);
        }
    }

    /// <summary>
    /// Indicates whether the executable exists and is valid
    /// </summary>
    public bool IsExecutableValid => !IsMissing;

    /// <summary>
    /// Returns a human-readable formatted string <see cref="InstallSize"/>
    /// </summary>
    public string FormattedInstallSize => ByteFormatter.FormatSize(InstallSize);

    /// <summary>
    /// Forces manual UI refresh for <see cref="IsMissing"/> and <see cref="IsExecutableValid"/>
    /// </summary>
    public void RefreshValidation() {
        OnPropertyChanged(nameof(IsMissing));
        OnPropertyChanged(nameof(IsExecutableValid));
    }

    /// <summary>
    /// Resolves the abs path to the engines exec file
    /// </summary>
    /// <param name="defaultInstallLocation"></param>
    /// <returns></returns>
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

        // Replicate Mac logic for the final resolution fallback (just in case)
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