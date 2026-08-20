using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using MarmaladeLauncher.Models;

namespace MarmaladeLauncher.Services;

public class InstallationService {
    private const string BaseDownloadUri = "https://www.ryanbester.com/download";

    private static readonly HttpClient HttpClient = new() {
        Timeout = TimeSpan.FromSeconds(10)
    };

    public static string InstallationsFilePath =>
        Path.Combine(SettingsService.AppDataDir, "installations.json");

    static InstallationService() {
        HttpClient.DefaultRequestHeaders.UserAgent.ParseAdd("MarmaladeLauncher/1.0");
    }

    public async Task<List<EngineInstallation>> LoadInstallations() {
        try {
            if (!File.Exists(InstallationsFilePath)) return new List<EngineInstallation>();
            string json = await File.ReadAllTextAsync(InstallationsFilePath);
            return JsonSerializer.Deserialize<List<EngineInstallation>>(json) ?? new List<EngineInstallation>();
        }
        catch (Exception e) {
            Console.WriteLine($"Failed to load installations: {e.Message}");
            return new List<EngineInstallation>();
        }
    }

    public async Task SaveInstallations(IEnumerable<EngineInstallation> installations) {
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
        Task<(List<EngineInstallation> Installations, Dictionary<EngineInstallation, InstallationEntry> EntryMap)>
        FetchEngineVersionsAsync(
            string? platform = null,
            string branch = "dev") {
        platform ??= GetCurrentPlatform();
        string requestUri = $"{BaseDownloadUri}?product=marmalade-engine&branch={branch}&platform={platform}&list";

        var installations = new List<EngineInstallation>();
        var entryMap = new Dictionary<EngineInstallation, InstallationEntry>();

        try {
            string jsonResponse = await HttpClient.GetStringAsync(requestUri);
            var jsonNode = JsonNode.Parse(jsonResponse);

            if (jsonNode?["builds"] is JsonArray buildsArray) {
                var serializerOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                var entries = buildsArray.Deserialize<List<InstallationEntry>>(serializerOptions) ?? new();

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
                    
                    var installation = new EngineInstallation {
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