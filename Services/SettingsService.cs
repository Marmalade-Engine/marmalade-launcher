using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class SettingsService {
    public LauncherSettings Settings { get; set; } = new();

    public static string AppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "marmalade-launcher"
    );
    
    public static string SettingsFilePath => Path.Combine(AppDataDir, "settings.json");
    public static string DefaultBaseDirectory => Path.Combine(AppDataDir, "installations");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    
    public SettingsService() {
        LoadSettings();
        EnsureDirectoriesExist();
    }

    public void EnsureDirectoriesExist() {
        if (!Directory.Exists(AppDataDir)) {
            Directory.CreateDirectory(AppDataDir);
        }

        if (!string.IsNullOrWhiteSpace(Settings.DefaultInstallLocation)) {
            if (!Directory.Exists(Settings.DefaultInstallLocation)) {
                Console.WriteLine($"Launcher directory does not exist, creating it at: {Settings.DefaultInstallLocation}");
                Directory.CreateDirectory(Settings.DefaultInstallLocation);
            }
        }
    }

    private void LoadSettings() {
        try {
            if (File.Exists(SettingsFilePath)) {
                string json = File.ReadAllText(SettingsFilePath);
                Settings = JsonSerializer.Deserialize<LauncherSettings>(json) ?? new LauncherSettings();
            } else {
                SyncSettings(new LauncherSettings());
            }
        }
        catch (Exception e) {
            Console.WriteLine($"Error loading settings: {e.Message}");
            Settings = new LauncherSettings();
        }
    }

    public async Task SaveSettings(LauncherSettings settings) {
        Settings = settings;
        
        if (!string.IsNullOrWhiteSpace(Settings.DefaultInstallLocation) && !Directory.Exists(Settings.DefaultInstallLocation)) {
            Directory.CreateDirectory(Settings.DefaultInstallLocation);
        }

        string json = JsonSerializer.Serialize(Settings, JsonOptions);
        await File.WriteAllTextAsync(SettingsFilePath, json);
        Console.WriteLine($"Saved settings to: {SettingsFilePath}");
    }

    private void SyncSettings(LauncherSettings settings) {
        Settings = settings;
        string json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(SettingsFilePath, json);
    }
}