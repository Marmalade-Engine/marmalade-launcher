using System;

namespace MarmaladeLauncher.Models;

public class EngineInstallation
{
    public string Id { get; set; } = Guid.NewGuid().ToString();
    public string Name { get; set; } = "Marmalade Engine";
    public string Version { get; set; } = "Unknown";
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public DateTime DateAdded { get; set; } = DateTime.Now;
}