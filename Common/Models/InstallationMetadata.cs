namespace MarmaladeLauncher.Models;

public class InstallationMetadata {
    public string Branch { get; set; } = string.Empty;
    public string Version { get; set; } = string.Empty;
    public int BuildId { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string? Checksum { get; set; }
    public DateTime InstalledAt { get; set; }
}