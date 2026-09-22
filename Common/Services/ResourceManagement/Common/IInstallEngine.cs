using System.Runtime.InteropServices;
using System.Text.Json;
using System.Web;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services.ResourceManagement.Common;

public interface IInstallEngine {
    OSPlatform Platform { get; }
    
    Task<string> ProcessDownloadedFile(string filePath, string targetDirectory, RemoteBuildEntry entry);

    void SetPlatformPermissions(string filePath);

    internal static async Task WriteInstallInfo(
        string installDir,
        RemoteBuildEntry entry,
        string execPath,
        string? checksum = null) {
        var info = new InstallationMetadata {
            Branch = GetQueryParam(entry.url, "branch", "main"),
            Platform = GetQueryParam(entry.url, "platform", RuntimeInformation.OSDescription),
            Version = entry.ResolvedVersion,
            BuildId = entry.id,
            ExecutablePath = execPath,
            DownloadUrl = entry.url,
            Checksum = checksum,
            InstalledAt = DateTime.Now
        };

        string jsonPath = Path.Combine(installDir, "install_info.json");
        var options = new JsonSerializerOptions { WriteIndented = true };

        await File.WriteAllTextAsync(jsonPath, JsonSerializer.Serialize(info, options));
    }
    
    private static string GetQueryParam(string url, string paramName, string fallback = "unknown") {
        try {
            var uri = new Uri(url);
            var query = HttpUtility.ParseQueryString(uri.Query);
            return query[paramName] ?? fallback;
        }
        catch {
            return fallback;
        }
    }
}