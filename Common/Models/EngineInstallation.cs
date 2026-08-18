using System;
using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Models;

public class EngineInstallation {
    public string Name { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public long InstallSize { get; set; } // Changed from int to long
    public string Branch { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string? Arguments { get; set; }
    public DateTime DateAdded { get; set; }

    public string FormattedInstallSize => ByteFormatter.FormatSize(InstallSize);
}