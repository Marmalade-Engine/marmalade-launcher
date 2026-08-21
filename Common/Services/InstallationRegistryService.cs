using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class InstallationRegistryService {
    private const string BaseDownloadUri = "https://www.ryanbester.com/download";

    private static readonly HttpClient HttpClient = new() {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static string InstallationsFilePath =>
        Path.Combine(SettingsService.AppDataDir, "installations.json");

    static InstallationRegistryService() {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarmaladeLauncher/1.0");
    }

    public async Task<List<LocalEngineInstallation>> LoadInstallations() {
        try {
            if (!File.Exists(InstallationsFilePath)) return new List<LocalEngineInstallation>();
            string json = await File.ReadAllTextAsync(InstallationsFilePath);
            return JsonSerializer.Deserialize<List<LocalEngineInstallation>>(json) ?? new List<LocalEngineInstallation>();
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to load installations: {e.Message}");
            return new List<LocalEngineInstallation>();
        }
    }

    public async Task SaveInstallations(IEnumerable<LocalEngineInstallation> installations) {
        try {
            string json = JsonSerializer.Serialize(installations, new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(InstallationsFilePath, json);
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to save installations: {e.Message}");
        }
    }

    /// <summary>
    /// Fetches remote engine releases for a specific platform and branch.
    /// </summary>
    public async
        Task<(List<LocalEngineInstallation> Installations, Dictionary<LocalEngineInstallation, RemoteBuildEntry> EntryMap)>
        FetchEngineVersionsAsync(
            string? platform = null,
            string branch = "dev") {
        platform ??= GetCurrentPlatform();
        string requestUri = $"{BaseDownloadUri}?product=marmalade-engine&branch={branch}&platform={platform}&list";

        var installations = new List<LocalEngineInstallation>();
        var entryMap = new Dictionary<LocalEngineInstallation, RemoteBuildEntry>();

        try {
            string jsonResponse = await HttpClient.GetStringAsync(requestUri);
            var jsonNode = JsonNode.Parse(jsonResponse);

            if (jsonNode?["builds"] is JsonArray buildsArray) {
                var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entries = buildsArray.Deserialize<List<RemoteBuildEntry>>(serializerOptions) ?? new();

                foreach (var entry in entries) {
                    if (!string.IsNullOrEmpty(entry.url) &&
                        !entry.url.Contains($"platform={platform}", StringComparison.OrdinalIgnoreCase)) {
                        continue;
                    }

                    string resolvedBranch = (!string.IsNullOrEmpty(entry.url) &&
                                             entry.url.Contains("branch=dev", StringComparison.OrdinalIgnoreCase))
                        ? "dev"
                        : "release";
                    
                    string resolvedVersion = string.IsNullOrWhiteSpace(entry.version)
                        ? entry.id.ToString()
                        : entry.version;

                    DateTime parsedDate = DateTime.TryParseExact(
                        entry.date,
                        "r",
                        System.Globalization.CultureInfo.InvariantCulture,
                        System.Globalization.DateTimeStyles.None,
                        out var result)
                        ? result
                        : DateTime.MinValue;
                    
                    var installation = new LocalEngineInstallation {
                        Name = resolvedVersion,
                        Version = resolvedVersion,
                        InstallSize = entry.size,
                        Branch = resolvedBranch,
                        DateAdded =  parsedDate,
                    };

                    installations.Add(installation);
                    entryMap[installation] = entry;
                }
            }
        }
        catch (Exception ex) {
            Console.WriteLine($"Error fetching versions: {ex.Message}");
        }

        return (installations, entryMap);
    }

    public static string GetCurrentPlatform() {
        if (OperatingSystem.IsWindows()) return "windows";
        if (OperatingSystem.IsMacOS()) return "macos-arm";
        if (OperatingSystem.IsLinux()) return "linux";
        return "windows";
    }
}