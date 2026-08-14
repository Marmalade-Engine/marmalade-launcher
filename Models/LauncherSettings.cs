using System;
using System.IO;
using MarmaladeLauncher.Services;

namespace MarmaladeLauncher.Models;

public class LauncherSettings {
    public string DefaultInstallLocation { get; set; } = SettingsService.DefaultBaseDirectory;
}