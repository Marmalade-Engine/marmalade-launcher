using System;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class SettingsService {
    public LauncherSettings Settings { get; set; } = new();

    public static string UserAppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "marmalade-launcher"
    );
    
    public static string SysAppDataDir => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
        "marmalade-launcher"
    );
    
    public static string SettingsFilePath => Path.Combine(UserAppDataDir, "settings.json");
    public static string DefaultUserDirectory => Path.Combine(UserAppDataDir, "installations");
    
    public static string DefaultSysDirectory => Path.Combine(SysAppDataDir, "installations");

    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true };
    
    public SettingsService() {
        LoadSettings();
        EnsureUserDirectoriesExist();
    }

    public void EnsureUserDirectoriesExist() {
        if (!Directory.Exists(UserAppDataDir)) {
            Directory.CreateDirectory(UserAppDataDir);
        }

        if (!string.IsNullOrWhiteSpace(Settings.DefaultUserInstallLocation)) {
            if (!Directory.Exists(Settings.DefaultUserInstallLocation)) {
                Console.WriteLine($"Launcher directory does not exist, creating it at: {Settings.DefaultUserInstallLocation}");
                Directory.CreateDirectory(Settings.DefaultUserInstallLocation);
            }
        }
    }

    public void EnsureSysDirectoriesExist() {
        if (!Directory.Exists(SysAppDataDir)) {
            Directory.CreateDirectory(SysAppDataDir);
        }
        
        if (!string.IsNullOrWhiteSpace(Settings.DefaultUserInstallLocation)) {}
    }

    public void LoadSettings() {
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
        
        if (!string.IsNullOrWhiteSpace(Settings.DefaultUserInstallLocation) && !Directory.Exists(Settings.DefaultUserInstallLocation)) {
            Directory.CreateDirectory(Settings.DefaultUserInstallLocation);
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