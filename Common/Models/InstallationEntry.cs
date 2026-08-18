using MarmaladeLauncher.Utils;

using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Models;

public class InstallationEntry {
    public int id { get; set; }
    public string? name { get; set; }
    public string url { get; set; } = string.Empty; // download url
    public string date { get; set; } = string.Empty;
    public long size { get; set; } = 0;
    public string? version { get; set; }

    public string ResolvedVersion => !string.IsNullOrWhiteSpace(version) 
        ? version 
        : $"Build {id}";
}