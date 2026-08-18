using MarmaladeLauncher.Utils;

namespace MarmaladeLauncher.Models;

public class InstallationEntry {
    public string id { get; set; } = string.Empty;
    public string url { get; set; } = string.Empty; // download url
    public string date { get; set; } = string.Empty;
    public long size { get; set; } = 0;
}